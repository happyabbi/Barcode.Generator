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
        public void BitmapConverter_FromPixelData_WritesBmpSignatureAndOffset()
        {
            var pixelData = CreatePixelData(1, 1, new byte[] { 0x11, 0x22, 0x33, 0x44 });

            var bmpBytes = BitmapConverter.FromPixelData(pixelData);

            Assert.Equal((byte)'B', bmpBytes[0]);
            Assert.Equal((byte)'M', bmpBytes[1]);
            Assert.Equal(54, BitConverter.ToInt32(bmpBytes, 10));
        }

        [Fact]
        public void BitmapConverter_FromPixelData_WritesRowsBottomUp()
        {
            var pixels = new byte[]
            {
                // Top row (y = 0)
                1, 2, 3, 4,
                5, 6, 7, 8,
                // Bottom row (y = 1)
                9, 10, 11, 12,
                13, 14, 15, 16
            };

            var pixelData = CreatePixelData(2, 2, pixels);
            var bmpBytes = BitmapConverter.FromPixelData(pixelData);

            const int pixelArrayOffset = 54;

            // BMP first row should be bottom row from source.
            Assert.Equal(9, bmpBytes[pixelArrayOffset + 0]);
            Assert.Equal(10, bmpBytes[pixelArrayOffset + 1]);
            Assert.Equal(11, bmpBytes[pixelArrayOffset + 2]);
            Assert.Equal(12, bmpBytes[pixelArrayOffset + 3]);
            Assert.Equal(13, bmpBytes[pixelArrayOffset + 4]);
            Assert.Equal(14, bmpBytes[pixelArrayOffset + 5]);
            Assert.Equal(15, bmpBytes[pixelArrayOffset + 6]);
            Assert.Equal(16, bmpBytes[pixelArrayOffset + 7]);

            // Followed by top row.
            Assert.Equal(1, bmpBytes[pixelArrayOffset + 8]);
            Assert.Equal(2, bmpBytes[pixelArrayOffset + 9]);
            Assert.Equal(3, bmpBytes[pixelArrayOffset + 10]);
            Assert.Equal(4, bmpBytes[pixelArrayOffset + 11]);
            Assert.Equal(5, bmpBytes[pixelArrayOffset + 12]);
            Assert.Equal(6, bmpBytes[pixelArrayOffset + 13]);
            Assert.Equal(7, bmpBytes[pixelArrayOffset + 14]);
            Assert.Equal(8, bmpBytes[pixelArrayOffset + 15]);
        }

        [Fact]
        public void BitmapConverter_FromPixelData_Throws_OnNull()
        {
            Assert.Throws<ArgumentNullException>(() => BitmapConverter.FromPixelData(null));
        }

        [Fact]
        public void BitmapConverter_FromPixelData_Throws_OnInvalidPixelBufferLength()
        {
            var invalidPixelData = CreatePixelData(2, 2, new byte[8]);

            Assert.Throws<ArgumentException>(() => BitmapConverter.FromPixelData(invalidPixelData));
        }

        private static PixelData CreatePixelData(int width, int height, byte[] pixels)
        {
            var ctor = typeof(PixelData).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(int), typeof(int), typeof(byte[]) },
                modifiers: null);

            return (PixelData)ctor.Invoke(new object[] { width, height, pixels });
        }
    }
}
