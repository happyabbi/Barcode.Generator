using Barcode.Generator;
using Barcode.Generator.Common;
using Barcode.Generator.Rendering;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/generate", (string text) =>
{
    var barcodeWriter = new BarcodeWriterPixelData
    {
        Format = BarcodeFormat.QR_CODE,
        Options = new EncodingOptions
        {
            Width = 300,
            Height = 300,
            Margin = 10,
            PureBarcode = true
        }
    };

    PixelData barcodeImage = barcodeWriter.Write(text);
    byte[] bmpBytes = BitmapConverter.FromPixelData(barcodeImage);
    return Results.File(bmpBytes, "image/bmp");
});

app.Run();
