using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;
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
    var defaultDbPath = Path.Combine(builder.Environment.ContentRootPath, "poslite.db");
    var conn = builder.Configuration.GetConnectionString("PosLite") ?? $"Data Source={defaultDbPath}";
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
    SeedDemoData(db);
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

app.MapGet("/generate", (string text, int? width, int? height, string? format, string? imageFormat, ILoggerFactory loggerFactory) =>
    GenerateBarcode(text, width, height, format, imageFormat, loggerFactory))
    .RequireRateLimiting("generate");

app.MapPost("/generate", (GenerateRequest request, ILoggerFactory loggerFactory) =>
    GenerateBarcode(request.Text ?? string.Empty, request.Width, request.Height, request.Format, request.ImageFormat, loggerFactory))
    .RequireRateLimiting("generate");

var api = app.MapGroup("/api");

api.MapPost("/products", async (HttpContext http, CreateProductRequest request, AppDbContext db) =>
{
    var denied = EnsureRole(http, "admin", "manager");
    if (denied != null) return denied;

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

api.MapGet("/products", async (HttpContext http, string? keyword, int page, int pageSize, AppDbContext db) =>
{
    var denied = EnsureRole(http, "admin", "manager", "cashier");
    if (denied != null) return denied;

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
        .OrderBy(p => p.Sku)
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

api.MapPut("/products/{id:guid}", async (HttpContext http, Guid id, UpdateProductRequest request, AppDbContext db) =>
{
    var denied = EnsureRole(http, "admin", "manager");
    if (denied != null) return denied;

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

api.MapPost("/products/{id:guid}/barcodes", async (HttpContext http, Guid id, AddBarcodeRequest request, AppDbContext db) =>
{
    var denied = EnsureRole(http, "admin", "manager");
    if (denied != null) return denied;

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

api.MapGet("/products/{id:guid}/barcodes", async (HttpContext http, Guid id, AppDbContext db) =>
{
    var denied = EnsureRole(http, "admin", "manager", "cashier");
    if (denied != null) return denied;

    var items = await db.Barcodes
        .AsNoTracking()
        .Where(b => b.ProductId == id)
        .OrderByDescending(b => b.IsPrimary)
        .ThenBy(b => b.CodeValue)
        .Select(b => new
        {
            b.Id,
            b.ProductId,
            b.Format,
            b.CodeValue,
            b.IsPrimary
        })
        .ToListAsync();

    return Results.Ok(new { items });
});

api.MapGet("/barcodes/{codeValue}", async (HttpContext http, string codeValue, AppDbContext db) =>
{
    var denied = EnsureRole(http, "admin", "manager", "cashier");
    if (denied != null) return denied;

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

api.MapPost("/inventory/in", async (HttpContext http, StockInRequest request, AppDbContext db) =>
{
    var denied = EnsureRole(http, "admin", "manager");
    if (denied != null) return denied;

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

api.MapGet("/inventory/low-stock", async (HttpContext http, AppDbContext db) =>
{
    var denied = EnsureRole(http, "admin", "manager", "cashier");
    if (denied != null) return denied;

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

api.MapGet("/orders", async (HttpContext http, int page, int pageSize, AppDbContext db) =>
{
    var denied = EnsureRole(http, "admin", "manager", "cashier");
    if (denied != null) return denied;

    page = page <= 0 ? 1 : page;
    pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

    var query = db.SalesOrders.AsNoTracking().OrderByDescending(o => o.Id);
    var total = await query.CountAsync();

    var items = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(o => new
        {
            o.Id,
            o.OrderNo,
            o.PaymentMethod,
            o.Subtotal,
            o.Discount,
            o.Total,
            o.PaidAmount,
            o.ChangeAmount,
            o.CreatedAt,
            ItemCount = o.Items.Sum(i => i.Qty)
        })
        .ToListAsync();

    return Results.Ok(new { page, pageSize, total, items });
});

api.MapGet("/orders/{id:guid}", async (HttpContext http, Guid id, AppDbContext db) =>
{
    var denied = EnsureRole(http, "admin", "manager", "cashier");
    if (denied != null) return denied;

    var order = await db.SalesOrders
        .AsNoTracking()
        .Where(o => o.Id == id)
        .Select(o => new
        {
            o.Id,
            o.OrderNo,
            o.PaymentMethod,
            o.Subtotal,
            o.Discount,
            o.Total,
            o.PaidAmount,
            o.ChangeAmount,
            o.Note,
            o.CreatedAt,
            items = o.Items.Select(i => new
            {
                i.Id,
                i.ProductId,
                i.SkuSnapshot,
                i.NameSnapshot,
                i.UnitPrice,
                i.Qty,
                i.LineTotal
            })
        })
        .FirstOrDefaultAsync();

    return order == null ? Results.NotFound(new { error = "order not found." }) : Results.Ok(order);
});

api.MapPost("/checkout", async (HttpContext http, CheckoutRequest request, AppDbContext db) =>
{
    var denied = EnsureRole(http, "admin", "manager", "cashier");
    if (denied != null) return denied;

    if (request.Items == null || request.Items.Count == 0)
    {
        return Results.BadRequest(new { error = "checkout items are required." });
    }

    var paymentMethod = (request.PaymentMethod ?? string.Empty).Trim().ToUpperInvariant();
    if (paymentMethod != "CASH" && paymentMethod != "CARD")
    {
        return Results.BadRequest(new { error = "payment method must be CASH or CARD." });
    }

    if (request.Items.Any(i => i.Qty <= 0))
    {
        return Results.BadRequest(new { error = "qty must be > 0." });
    }

    var grouped = request.Items
        .GroupBy(i => i.ProductId)
        .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Qty) })
        .ToList();

    await using var tx = await db.Database.BeginTransactionAsync();

    var productIds = grouped.Select(x => x.ProductId).ToList();
    var products = await db.Products
        .Include(p => p.InventoryLevel)
        .Where(p => p.IsActive && productIds.Contains(p.Id))
        .ToListAsync();

    if (products.Count != productIds.Count)
    {
        return Results.BadRequest(new { error = "one or more products not found." });
    }

    var subtotal = 0m;
    var orderItems = new List<SalesOrderItem>();

    foreach (var item in grouped)
    {
        var product = products.First(p => p.Id == item.ProductId);
        var level = product.InventoryLevel;
        if (level == null)
        {
            return Results.BadRequest(new { error = $"inventory missing for {product.Sku}." });
        }

        if (level.QtyOnHand < item.Qty)
        {
            return Results.BadRequest(new
            {
                error = $"insufficient stock for {product.Sku}.",
                productId = product.Id,
                sku = product.Sku,
                qtyOnHand = level.QtyOnHand,
                requestedQty = item.Qty
            });
        }

        var lineTotal = Math.Round(product.Price * item.Qty, 2, MidpointRounding.AwayFromZero);
        subtotal += lineTotal;

        orderItems.Add(new SalesOrderItem
        {
            ProductId = product.Id,
            SkuSnapshot = product.Sku,
            NameSnapshot = product.Name,
            UnitPrice = product.Price,
            Qty = item.Qty,
            LineTotal = lineTotal
        });
    }

    var discount = Math.Round(request.Discount ?? 0m, 2, MidpointRounding.AwayFromZero);
    if (discount < 0)
    {
        return Results.BadRequest(new { error = "discount must be >= 0." });
    }

    var total = Math.Round(subtotal - discount, 2, MidpointRounding.AwayFromZero);
    if (total < 0)
    {
        return Results.BadRequest(new { error = "discount cannot exceed subtotal." });
    }

    var paidAmount = Math.Round(request.PaidAmount, 2, MidpointRounding.AwayFromZero);
    if (paidAmount < total)
    {
        return Results.BadRequest(new { error = "paid amount is insufficient." });
    }

    var changeAmount = paymentMethod == "CASH"
        ? Math.Round(paidAmount - total, 2, MidpointRounding.AwayFromZero)
        : 0m;

    if (paymentMethod == "CARD" && paidAmount != total)
    {
        return Results.BadRequest(new { error = "for CARD payment, paid amount must equal total." });
    }

    var now = DateTimeOffset.UtcNow;
    var orderNo = $"SO{now:yyyyMMddHHmmssfff}";

    var order = new SalesOrder
    {
        OrderNo = orderNo,
        PaymentMethod = paymentMethod,
        Subtotal = subtotal,
        Discount = discount,
        Total = total,
        PaidAmount = paidAmount,
        ChangeAmount = changeAmount,
        Note = request.Note,
        CreatedAt = now,
        Items = orderItems
    };

    db.SalesOrders.Add(order);

    foreach (var item in grouped)
    {
        var product = products.First(p => p.Id == item.ProductId);
        var level = product.InventoryLevel!;
        level.QtyOnHand -= item.Qty;
        level.UpdatedAt = now;

        db.InventoryMovements.Add(new InventoryMovement
        {
            ProductId = product.Id,
            MovementType = "OUT",
            Qty = item.Qty,
            Reason = $"Checkout {orderNo}"
        });
    }

    await db.SaveChangesAsync();
    await tx.CommitAsync();

    return Results.Ok(new
    {
        order.Id,
        order.OrderNo,
        order.PaymentMethod,
        order.Subtotal,
        order.Discount,
        order.Total,
        order.PaidAmount,
        order.ChangeAmount,
        itemCount = order.Items.Sum(i => i.Qty),
        items = order.Items.Select(i => new
        {
            i.ProductId,
            i.SkuSnapshot,
            i.NameSnapshot,
            i.UnitPrice,
            i.Qty,
            i.LineTotal
        })
    });
});

app.Run();

static void SeedDemoData(AppDbContext db)
{
    if (db.Products.Any())
    {
        return;
    }

    var seeds = new[]
    {
        new { Sku = "SKU-COFFEE-001", Name = "Arabica Coffee Beans 1kg", Category = "Beverage", Price = 489m, Cost = 320m, Qty = 24, Reorder = 10, Format = "CODE_128", Code = "COFFEE001" },
        new { Sku = "SKU-TOILET-001", Name = "Toilet Paper 24 Rolls", Category = "Daily", Price = 1159m, Cost = 860m, Qty = 8, Reorder = 12, Format = "EAN_13", Code = "471234567890" },
        new { Sku = "SKU-SNACK-001", Name = "Mixed Nuts 500g", Category = "Snack", Price = 329m, Cost = 220m, Qty = 18, Reorder = 10, Format = "EAN_13", Code = "471234567891" },
        new { Sku = "SKU-DRINK-001", Name = "Sparkling Water 24pk", Category = "Beverage", Price = 235m, Cost = 150m, Qty = 6, Reorder = 8, Format = "UPC_A", Code = "123456789012" },
        new { Sku = "SKU-CLEAN-001", Name = "Laundry Detergent 3L", Category = "Cleaning", Price = 569m, Cost = 410m, Qty = 14, Reorder = 9, Format = "CODE_128", Code = "CLEAN001" }
    };

    foreach (var item in seeds)
    {
        var product = new Product
        {
            Sku = item.Sku,
            Name = item.Name,
            Category = item.Category,
            Price = item.Price,
            Cost = item.Cost,
            IsActive = true,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Products.Add(product);
        db.InventoryLevels.Add(new InventoryLevel
        {
            ProductId = product.Id,
            QtyOnHand = item.Qty,
            ReorderLevel = item.Reorder
        });

        db.Barcodes.Add(new BarcodeEntry
        {
            ProductId = product.Id,
            Format = item.Format,
            CodeValue = item.Code,
            IsPrimary = true
        });

        db.InventoryMovements.Add(new InventoryMovement
        {
            ProductId = product.Id,
            MovementType = "IN",
            Qty = item.Qty,
            Reason = "Seed demo data"
        });
    }

    db.SaveChanges();
}

static IResult GenerateBarcode(string text, int? width, int? height, string? format, string? imageFormat, ILoggerFactory loggerFactory)
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
        var output = ResolveImageFormat(imageFormat);

        if (output == "svg")
        {
            var svg = ToSvg(barcodeImage);
            return Results.Text(svg, "image/svg+xml", Encoding.UTF8);
        }

        if (output == "png")
        {
            var pngBytes = ToPng(barcodeImage);
            return Results.File(pngBytes, "image/png");
        }

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

static IResult? EnsureRole(HttpContext http, params string[] allowed)
{
    var role = (http.Request.Headers["X-Role"].FirstOrDefault() ?? "admin").Trim().ToLowerInvariant();
    if (allowed.Any(x => x.Equals(role, StringComparison.OrdinalIgnoreCase)))
    {
        return null;
    }

    return Results.StatusCode(StatusCodes.Status403Forbidden);
}

static string ResolveImageFormat(string? imageFormat)
{
    var value = (imageFormat ?? "bmp").Trim().ToLowerInvariant();
    return value is "bmp" or "png" or "svg" ? value : "bmp";
}

static byte[] ToPng(PixelData pixelData)
{
    using var image = Image.LoadPixelData<Rgba32>(pixelData.Pixels, pixelData.Width, pixelData.Height);
    using var ms = new MemoryStream();
    image.Save(ms, new PngEncoder());
    return ms.ToArray();
}

static string ToSvg(PixelData pixelData)
{
    var sb = new StringBuilder();
    sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {pixelData.Width} {pixelData.Height}\" shape-rendering=\"crispEdges\">\n");
    sb.Append($"<rect width=\"{pixelData.Width}\" height=\"{pixelData.Height}\" fill=\"white\"/>\n");

    for (var y = 0; y < pixelData.Height; y++)
    {
        for (var x = 0; x < pixelData.Width; x++)
        {
            var idx = (y * pixelData.Width + x) * 4;
            var r = pixelData.Pixels[idx];
            var g = pixelData.Pixels[idx + 1];
            var b = pixelData.Pixels[idx + 2];
            if (r < 128 || g < 128 || b < 128)
            {
                sb.Append($"<rect x=\"{x}\" y=\"{y}\" width=\"1\" height=\"1\" fill=\"black\"/>\n");
            }
        }
    }

    sb.Append("</svg>");
    return sb.ToString();
}

public record GenerateRequest(string? Text, int? Width, int? Height, string? Format, string? ImageFormat);
public record CreateProductRequest(string Sku, string Name, string? Category, decimal Price, decimal Cost, int InitialQty = 0, int ReorderLevel = 10);
public record UpdateProductRequest(string? Name, string? Category, decimal? Price, decimal? Cost, int? ReorderLevel);
public record AddBarcodeRequest(string Format, string CodeValue, bool IsPrimary = false);
public record StockInRequest(Guid ProductId, int Qty, string? Reason);
public record CheckoutRequest(List<CheckoutItemRequest> Items, string PaymentMethod, decimal PaidAmount, decimal? Discount = null, string? Note = null);
public record CheckoutItemRequest(Guid ProductId, int Qty);

public partial class Program { }
