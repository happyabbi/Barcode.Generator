using System;
using System.Reflection;
using Barcode.Generator;
using Barcode.Generator.Rendering;
using Xunit;

namespace Barcode.Generator.Tests
{
    public class BarcodeGeneratorTests
    {
        [Fact]
        public void BarcodeWriterPixelData_Produces_PixelData()
        {
            var writer = new BarcodeWriterPixelData { Format = BarcodeFormat.QR_CODE };
            var pixelData = writer.Write("Hello World");

            Assert.NotNull(pixelData);
            Assert.NotEmpty(pixelData.Pixels);
            Assert.True(pixelData.Width > 0);
            Assert.True(pixelData.Height > 0);
        }

        [Fact]
        public void BitmapConverter_FromPixelData_CorrectLength()
        {
            var writer = new BarcodeWriterPixelData { Format = BarcodeFormat.QR_CODE };
            var pixelData = writer.Write("LengthTest");

            var bmpBytes = BitmapConverter.FromPixelData(pixelData);

            var expectedLength = 14 + 40 + pixelData.Width * pixelData.Height * 4;
            Assert.Equal(expectedLength, bmpBytes.Length);
        }

        [Fact]
        public void BitmapConverter_FromPixelData_Throws_OnNull()
        {
            Assert.Throws<System.ArgumentNullException>(() => BitmapConverter.FromPixelData(null));
        }

        [Fact]
        public void BitmapConverter_FromPixelData_Throws_OnInvalidPixelBufferLength()
        {
            var ctor = typeof(PixelData).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(int), typeof(int), typeof(byte[]) },
                modifiers: null);

            var invalidPixelData = (PixelData)ctor.Invoke(new object[] { 2, 2, new byte[8] });

            Assert.Throws<ArgumentException>(() => BitmapConverter.FromPixelData(invalidPixelData));
        }
    }
}
