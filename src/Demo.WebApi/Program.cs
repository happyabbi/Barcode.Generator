using System;
using System.Linq;
using System.Threading.RateLimiting;
using Barcode.Generator;
using Barcode.Generator.Common;
using Barcode.Generator.Rendering;
using Demo.WebApi;
using Demo.WebApi.Pos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

var configuredOrigins = (builder.Configuration["ALLOWED_ORIGINS"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(origin => origin.Trim().TrimEnd('/'))
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .ToHashSet(StringComparer.OrdinalIgnoreCase);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .SetIsOriginAllowed(origin =>
            {
                var normalized = origin.Trim().TrimEnd('/');
                return normalized.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase) ||
                       normalized.StartsWith("http://127.0.0.1:", StringComparison.OrdinalIgnoreCase) ||
                       configuredOrigins.Contains(normalized);
            }));
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var conn = builder.Configuration.GetConnectionString("PosLite") ?? "Data Source=poslite.db";
    options.UseSqlite(conn);
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "Too many requests. Please try again shortly."
        }, cancellationToken);
    };

    options.AddFixedWindowLimiter("generate", limiterOptions =>
    {
        limiterOptions.PermitLimit = 60;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});
app.UseCors();
app.UseRateLimiter();

app.MapGet("/generate", (string text, int? width, int? height, string? format, ILoggerFactory loggerFactory) =>
    GenerateBarcode(text, width, height, format, loggerFactory))
    .RequireRateLimiting("generate");

app.MapPost("/generate", (GenerateRequest request, ILoggerFactory loggerFactory) =>
    GenerateBarcode(request.Text ?? string.Empty, request.Width, request.Height, request.Format, loggerFactory))
    .RequireRateLimiting("generate");

var api = app.MapGroup("/api");

api.MapPost("/products", async (CreateProductRequest request, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Sku) || string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new { error = "sku and name are required." });
    }

    var exists = await db.Products.AnyAsync(p => p.Sku == request.Sku);
    if (exists)
    {
        return Results.BadRequest(new { error = "sku already exists." });
    }

    var product = new Product
    {
        Sku = request.Sku.Trim(),
        Name = request.Name.Trim(),
        Category = request.Category?.Trim(),
        Price = request.Price,
        Cost = request.Cost,
        IsActive = true,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    db.Products.Add(product);
    db.InventoryLevels.Add(new InventoryLevel
    {
        ProductId = product.Id,
        QtyOnHand = request.InitialQty,
        ReorderLevel = request.ReorderLevel
    });

    if (request.InitialQty > 0)
    {
        db.InventoryMovements.Add(new InventoryMovement
        {
            ProductId = product.Id,
            MovementType = "IN",
            Qty = request.InitialQty,
            Reason = "Initial stock"
        });
    }

    await db.SaveChangesAsync();

    return Results.Created($"/api/products/{product.Id}", new
    {
        product.Id,
        product.Sku,
        product.Name,
        product.Category,
        product.Price,
        product.Cost,
        QtyOnHand = request.InitialQty,
        ReorderLevel = request.ReorderLevel
    });
});

api.MapGet("/products", async (string? keyword, int page, int pageSize, AppDbContext db) =>
{
    page = page <= 0 ? 1 : page;
    pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

    var query = db.Products
        .AsNoTracking()
        .Include(p => p.InventoryLevel)
        .Where(p => p.IsActive);

    if (!string.IsNullOrWhiteSpace(keyword))
    {
        var k = keyword.Trim();
        query = query.Where(p => p.Sku.Contains(k) || p.Name.Contains(k));
    }

    var total = await query.CountAsync();
    var items = await query
        .OrderByDescending(p => p.UpdatedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(p => new
        {
            p.Id,
            p.Sku,
            p.Name,
            p.Category,
            p.Price,
            p.Cost,
            QtyOnHand = p.InventoryLevel != null ? p.InventoryLevel.QtyOnHand : 0,
            ReorderLevel = p.InventoryLevel != null ? p.InventoryLevel.ReorderLevel : 10
        })
        .ToListAsync();

    return Results.Ok(new { page, pageSize, total, items });
});

api.MapPut("/products/{id:guid}", async (Guid id, UpdateProductRequest request, AppDbContext db) =>
{
    var product = await db.Products.Include(p => p.InventoryLevel).FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
    if (product == null)
    {
        return Results.NotFound(new { error = "product not found." });
    }

    product.Name = string.IsNullOrWhiteSpace(request.Name) ? product.Name : request.Name.Trim();
    product.Category = request.Category?.Trim();
    product.Price = request.Price ?? product.Price;
    product.Cost = request.Cost ?? product.Cost;
    product.UpdatedAt = DateTimeOffset.UtcNow;

    if (product.InventoryLevel != null && request.ReorderLevel.HasValue)
    {
        product.InventoryLevel.ReorderLevel = request.ReorderLevel.Value;
        product.InventoryLevel.UpdatedAt = DateTimeOffset.UtcNow;
    }

    await db.SaveChangesAsync();
    return Results.Ok(new { message = "updated" });
});

api.MapPost("/products/{id:guid}/barcodes", async (Guid id, AddBarcodeRequest request, AppDbContext db) =>
{
    var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
    if (product == null)
    {
        return Results.NotFound(new { error = "product not found." });
    }

    if (!GenerateRequestValidation.SupportedFormats.Select(x => x.ToString()).Contains(request.Format))
    {
        return Results.BadRequest(new { error = "unsupported format." });
    }

    var duplicated = await db.Barcodes.AnyAsync(b => b.Format == request.Format && b.CodeValue == request.CodeValue);
    if (duplicated)
    {
        return Results.BadRequest(new { error = "barcode already exists." });
    }

    if (request.IsPrimary)
    {
        var existingPrimary = await db.Barcodes.Where(b => b.ProductId == id && b.IsPrimary).ToListAsync();
        foreach (var item in existingPrimary)
        {
            item.IsPrimary = false;
        }
    }

    var barcode = new BarcodeEntry
    {
        ProductId = id,
        Format = request.Format,
        CodeValue = request.CodeValue,
        IsPrimary = request.IsPrimary
    };

    db.Barcodes.Add(barcode);
    await db.SaveChangesAsync();

    return Results.Created($"/api/products/{id}/barcodes/{barcode.Id}", new
    {
        barcode.Id,
        barcode.ProductId,
        barcode.Format,
        barcode.CodeValue,
        barcode.IsPrimary
    });
});

api.MapGet("/barcodes/{codeValue}", async (string codeValue, AppDbContext db) =>
{
    var match = await db.Barcodes
        .AsNoTracking()
        .Include(b => b.Product)
        .ThenInclude(p => p.InventoryLevel)
        .Where(b => b.CodeValue == codeValue && b.Product.IsActive)
        .OrderByDescending(b => b.IsPrimary)
        .FirstOrDefaultAsync();

    if (match == null)
    {
        return Results.NotFound(new { error = "barcode not found." });
    }

    return Results.Ok(new
    {
        productId = match.ProductId,
        sku = match.Product.Sku,
        name = match.Product.Name,
        price = match.Product.Price,
        qtyOnHand = match.Product.InventoryLevel != null ? match.Product.InventoryLevel.QtyOnHand : 0,
        barcode = new
        {
            format = match.Format,
            codeValue = match.CodeValue
        }
    });
});

api.MapPost("/inventory/in", async (StockInRequest request, AppDbContext db) =>
{
    if (request.Qty <= 0)
    {
        return Results.BadRequest(new { error = "qty must be > 0" });
    }

    var level = await db.InventoryLevels.FirstOrDefaultAsync(x => x.ProductId == request.ProductId);
    if (level == null)
    {
        return Results.NotFound(new { error = "inventory not found." });
    }

    level.QtyOnHand += request.Qty;
    level.UpdatedAt = DateTimeOffset.UtcNow;

    db.InventoryMovements.Add(new InventoryMovement
    {
        ProductId = request.ProductId,
        MovementType = "IN",
        Qty = request.Qty,
        Reason = request.Reason
    });

    await db.SaveChangesAsync();
    return Results.Ok(new { productId = request.ProductId, qtyOnHand = level.QtyOnHand });
});

api.MapGet("/inventory/low-stock", async (AppDbContext db) =>
{
    var items = await db.InventoryLevels
        .AsNoTracking()
        .Include(i => i.Product)
        .Where(i => i.Product.IsActive && i.QtyOnHand <= i.ReorderLevel)
        .OrderBy(i => i.QtyOnHand)
        .Select(i => new
        {
            i.ProductId,
            i.Product.Sku,
            i.Product.Name,
            i.QtyOnHand,
            i.ReorderLevel
        })
        .ToListAsync();

    return Results.Ok(new { total = items.Count, items });
});

app.Run();

static IResult GenerateBarcode(string text, int? width, int? height, string? format, ILoggerFactory loggerFactory)
{
    var validationErrors = GenerateRequestValidation.Validate(text, width, height, format);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    var logger = loggerFactory.CreateLogger("GenerateEndpoint");

    var barcodeWriter = new BarcodeWriterPixelData
    {
        Format = GenerateRequestValidation.ResolveFormat(format),
        Options = new EncodingOptions
        {
            Width = GenerateRequestValidation.ResolveDimension(width),
            Height = GenerateRequestValidation.ResolveDimension(height),
            Margin = 10,
            PureBarcode = true
        }
    };

    try
    {
        PixelData barcodeImage = barcodeWriter.Write(text);
        byte[] bmpBytes = BitmapConverter.FromPixelData(barcodeImage);
        return Results.File(bmpBytes, "image/bmp");
    }
    catch (WriterException ex)
    {
        logger.LogWarning(ex, "Barcode generation failed for the provided input.");
        return Results.BadRequest(new { error = "Unable to generate barcode with the provided input." });
    }
    catch (ArgumentException ex)
    {
        logger.LogWarning(ex, "Invalid barcode generation parameters.");
        return Results.BadRequest(new { error = "Invalid request parameters." });
    }
    catch (InvalidOperationException ex)
    {
        logger.LogError(ex, "Unexpected failure while rendering barcode image.");
        return Results.Problem(
            title: "Barcode generation failed",
            detail: "An unexpected server error occurred while generating the barcode.",
            statusCode: StatusCodes.Status500InternalServerError);
    }
}

public record GenerateRequest(string? Text, int? Width, int? Height, string? Format);
public record CreateProductRequest(string Sku, string Name, string? Category, decimal Price, decimal Cost, int InitialQty = 0, int ReorderLevel = 10);
public record UpdateProductRequest(string? Name, string? Category, decimal? Price, decimal? Cost, int? ReorderLevel);
public record AddBarcodeRequest(string Format, string CodeValue, bool IsPrimary = false);
public record StockInRequest(Guid ProductId, int Qty, string? Reason);

public partial class Program { }
