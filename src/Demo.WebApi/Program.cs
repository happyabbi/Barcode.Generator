using System;
using Barcode.Generator;
using Barcode.Generator.Common;
using Barcode.Generator.Rendering;
using Demo.WebApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/generate", (string text, int? width, int? height, ILoggerFactory loggerFactory) =>
{
    var validationErrors = GenerateRequestValidation.Validate(text, width, height);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    var logger = loggerFactory.CreateLogger("GenerateEndpoint");

    var barcodeWriter = new BarcodeWriterPixelData
    {
        Format = BarcodeFormat.QR_CODE,
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
});

app.Run();

public partial class Program { }
