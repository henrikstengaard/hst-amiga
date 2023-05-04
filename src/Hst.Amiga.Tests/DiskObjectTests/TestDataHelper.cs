namespace Hst.Amiga.Tests.DiskObjectTests
{
    using System;
    using System.Linq;
    using DataTypes.DiskObjects;
    using Imaging;

    public static class TestDataHelper
    {
        public const int RgbaColorSize = 4;
        public const int BitsPerPixel = 32;
        public const int Depth = 2; // depth of 2 is used to represent 4 colors ([Math]::Pow(2, depth) = max colors)

        public static byte[] CreateImageData(int width, int height, int depth)
        {
            var bytesPerRow = (width + 15) / 16 * 2;
            return new byte[bytesPerRow * height * depth];
        }

        public static void SetImageDataPixel(byte[] imageData, int width, int height, int depth, int x, int y, int color)
        {
            var bytesPerRow = (width + 15) / 16 * 2;
            
            for (var bitPlane = 0; bitPlane < depth; bitPlane++)
            {
                var colorBit = color & (1 << bitPlane);
                if (colorBit == 0)
                {
                    continue;
                }
                            
                var bitOffset = 7 - (x % Constants.BITS_PER_BYTE);
                var imageDataOffset = (bytesPerRow * height * bitPlane) + (y * bytesPerRow) + (x / Constants.BITS_PER_BYTE);
                imageData[imageDataOffset] |= (byte)(1 << bitOffset);
            }
        }
        
        public static byte[] CreatePixelData(int width, int height)
        {
            return new byte[width * height];
        }
        
        public static Image CreateImage(Palette palette)
        {
            var width = 12;
            var height = 12;
            var pixelData = new byte[width * height * RgbaColorSize];

            var colors = palette.Colors;
            
            for (var y = 0; y < 6; y++)
            {
                for (var x = 0; x < 6; x++)
                {
                    SetPixelDataPixel(pixelData, width, x, y, (byte)colors[0].R, (byte)colors[0].G, (byte)colors[0].B,
                        (byte)colors[0].A);
                    SetPixelDataPixel(pixelData, width, 6 + x, y, (byte)colors[1].R, (byte)colors[1].G, 
                        (byte)colors[1].B, (byte)colors[1].A);
                    SetPixelDataPixel(pixelData, width, x, 6 + y, (byte)colors[2].R, (byte)colors[2].G, 
                        (byte)colors[2].B, (byte)colors[2].A);
                    SetPixelDataPixel(pixelData, width, 6 + x, 6 + y, (byte)colors[3].R, (byte)colors[3].G, 
                        (byte)colors[3].B, (byte)colors[3].A);
                }
            }

            return new Image(width, height, 32, new Palette(), pixelData);
        }

        public static ImageData CreateImageData()
        {
            var width = 12;
            var height = 12;
            var depth = 2;
            var imageData = CreateImageData(width, height, depth);

            for (var y = 0; y < 6; y++)
            {
                for (var x = 0; x < 6; x++)
                {
                    SetImageDataPixel(imageData, width, height, depth, x, y, 0);
                    SetImageDataPixel(imageData, width, height, depth, 6 + x, y, 1);
                    SetImageDataPixel(imageData, width, height, depth, x, 6 + y, 2);
                    SetImageDataPixel(imageData, width, height, depth, 6 + x, 6 + y, 3);
                }
            }

            return new ImageData
            {
                Width = (short)width,
                Height = (short)height,
                Depth = (short)depth,
                TopEdge = 0,
                LeftEdge = 0,
                NextPointer = 0,
                PlanePick = (byte)(Math.Pow(2, depth) - 1),
                PlaneOnOff = 0,
                ImageDataPointer = 1,
                Data = imageData
            };
        }
        
        public static Image CreateFirstImage()
        {
            return CreateImage(AmigaOsPalette.FourColors());
        }
        
        public static Image CreateSecondImage()
        {
            var palette = new Palette();

            foreach (var color in AmigaOsPalette.FourColors().Colors.Reverse())
            {
                palette.AddColor(color);
            }
            
            return CreateImage(palette);
        }

        public static void SetPixelDataPixel(byte[] pixelData, int width, int x, int y, byte r, byte g, byte b, byte a)
        {
            var offset = ((y * width) + x) * RgbaColorSize;
            pixelData[offset] = r;
            pixelData[offset + 1] = g;
            pixelData[offset + 2] = b;
            pixelData[offset + 3] = a;
        }
    }
}