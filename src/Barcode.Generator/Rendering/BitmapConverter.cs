/*
 * Copyright 2019 Nicolò Carandini
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;

namespace Barcode.Generator.Rendering
{
    public static class BitmapConverter
    {
        private const int BmpHeaderLength = 14 + 40; // Header + DIB Header (BITMAPINFOHEADER)
        private const short BmpHeaderField = 19778; // Little endian: 0x42 0x4D ("BM")

        public static byte[] FromPixelData(PixelData pixelData)
        {
            if (pixelData == null)
            {
                throw new ArgumentNullException(nameof(pixelData), "PixelData can't be null");
            }

            var expectedPixelLength = pixelData.Width * pixelData.Height * 4;
            if (pixelData.Pixels == null || pixelData.Pixels.Length != expectedPixelLength)
            {
                throw new ArgumentException(
                    $"Invalid pixel buffer length. Expected {expectedPixelLength} bytes, got {pixelData.Pixels?.Length ?? 0}.",
                    nameof(pixelData));
            }

            byte[] bmpBytes = new byte[pixelData.Pixels.Length + BmpHeaderLength];
            int writePointer = 0;

            // == HEADER ==
            writePointer = WriteInt16LittleEndian(bmpBytes, writePointer, BmpHeaderField);
            writePointer = WriteInt32LittleEndian(bmpBytes, writePointer, bmpBytes.Length);
            writePointer = WriteInt32LittleEndian(bmpBytes, writePointer, 0);
            writePointer = WriteInt32LittleEndian(bmpBytes, writePointer, BmpHeaderLength);

            // == DIB header (BITMAPINFOHEADER) ==
            writePointer = WriteInt32LittleEndian(bmpBytes, writePointer, 40);
            writePointer = WriteInt32LittleEndian(bmpBytes, writePointer, pixelData.Width);
            writePointer = WriteInt32LittleEndian(bmpBytes, writePointer, pixelData.Height);
            writePointer = WriteInt16LittleEndian(bmpBytes, writePointer, 1);
            writePointer = WriteInt16LittleEndian(bmpBytes, writePointer, 32);
            writePointer = WriteInt32LittleEndian(bmpBytes, writePointer, 0);
            writePointer = WriteInt32LittleEndian(bmpBytes, writePointer, 0);
            writePointer = WriteInt32LittleEndian(bmpBytes, writePointer, 3780);
            writePointer = WriteInt32LittleEndian(bmpBytes, writePointer, 3780);
            writePointer = WriteInt32LittleEndian(bmpBytes, writePointer, 0);
            writePointer = WriteInt32LittleEndian(bmpBytes, writePointer, 0);

            // == PIXEL ARRAY ==
            // BMP stores rows from bottom to top.
            var rowStride = pixelData.Width * 4;
            for (int rowIndex = pixelData.Height - 1; rowIndex >= 0; rowIndex--)
            {
                var sourceOffset = rowIndex * rowStride;
                Buffer.BlockCopy(pixelData.Pixels, sourceOffset, bmpBytes, writePointer, rowStride);
                writePointer += rowStride;
            }

            return bmpBytes;
        }

        private static int WriteInt16LittleEndian(byte[] byteArray, int index, int value)
        {
            byteArray[index] = (byte)value;
            byteArray[index + 1] = (byte)(value >> 8);
            return index + 2;
        }

        private static int WriteInt32LittleEndian(byte[] byteArray, int index, int value)
        {
            byteArray[index] = (byte)value;
            byteArray[index + 1] = (byte)(value >> 8);
            byteArray[index + 2] = (byte)(value >> 16);
            byteArray[index + 3] = (byte)(value >> 24);
            return index + 4;
        }
    }
}
