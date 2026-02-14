using System;
using Barcode.Generator;
using Barcode.Generator.Common;
using Barcode.Generator.Rendering;
using Demo.WebApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.RateLimiting;

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
app.UseCors();
app.UseRateLimiter();

app.MapGet("/generate", (string text, int? width, int? height, string? format, ILoggerFactory loggerFactory) =>
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
        return Results.BadRequest(new
        {
            error = "Unable to generate barcode with the provided input."
        });
    }
    catch (ArgumentException ex)
    {
        logger.LogWarning(ex, "Invalid barcode generation parameters.");
        return Results.BadRequest(new
        {
            error = "Invalid request parameters."
        });
    }
    catch (InvalidOperationException ex)
    {
        logger.LogError(ex, "Unexpected failure while rendering barcode image.");
        return Results.Problem(
            title: "Barcode generation failed",
            detail: "An unexpected server error occurred while generating the barcode.",
            statusCode: StatusCodes.Status500InternalServerError);
    }
}).RequireRateLimiting("generate");

app.Run();

public partial class Program { }
