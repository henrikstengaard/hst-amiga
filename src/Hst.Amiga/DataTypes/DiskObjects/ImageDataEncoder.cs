namespace Hst.Amiga.DataTypes.DiskObjects
{
    using System;
    using System.Collections.Generic;
    using Hst.Imaging;

    public static class ImageDataEncoder
    {
        public static ImageData Encode(Image image, int depth = 3)
        {
            var maxColors = Math.Pow(2, depth);
            var paletteIndex = new Dictionary<uint, int>();
            for (var i = 0; i < image.Palette.Colors.Count; i++)
            {
                var color = image.Palette.Colors[i];
                var colorId = (uint)color.R << 24 | (uint)color.G << 16 | (uint)color.B << 8 | (uint)color.A;
                paletteIndex[colorId] = i;
            }

            var highestColorUsed = 0;

            const int bitsPerByte = 8;
            var bytesPerRow = (image.Width + 15) / 16 * 2;

            var data = new byte[bytesPerRow * image.Height * depth];

            var imagePixelDataIterator = new ImagePixelDataIterator(image);
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    if (!imagePixelDataIterator.Next())
                    {
                        throw new InvalidOperationException();
                    }

                    // get a reference to the pixel at position x, y
                    var pixel = imagePixelDataIterator.Current;

                    var colorId = (uint)pixel.R << 24 | (uint)pixel.G << 16 | (uint)pixel.B << 8 | (uint)pixel.A;
                    if (!paletteIndex.ContainsKey(colorId))
                    {
                        paletteIndex[colorId] = paletteIndex.Count;
                    }
                    
                    var color = paletteIndex[colorId];

                    if (color > highestColorUsed)
                    {
                        highestColorUsed = color;
                    }

                    for (var plane = 0; plane < depth; plane++)
                    {
                        var bit = 7 - (x % bitsPerByte);
                        var offset = (bytesPerRow * image.Height * plane) + (y * bytesPerRow) + (x / bitsPerByte);

                        var setBitPlane = (color & (1 << plane)) != 0;
                        if (setBitPlane)
                        {
                            data[offset] |= (byte)(1 << bit);
                        }
                    }
                }
            }
            
            if (highestColorUsed > maxColors)
            {
                throw new ArgumentException(
                    $"Image uses {(highestColorUsed + 1)} colors, but depth {depth} only allows max {maxColors} colors",
                    nameof(depth));
            }

            return new ImageData
            {
                TopEdge = 0,
                LeftEdge = 0,
                Width = (short)image.Width,
                Height = (short)image.Height,
                Depth = (byte)Math.Min(depth, highestColorUsed),
                Data = data,
                ImageDataPointer = 1,
                PlanePick = (byte)(maxColors - 1)
            };
        }

        // /// <summary>
        // /// encodes image into image data. colors not present in palette are ignored/skipped
        // /// </summary>
        // /// <param name="image"></param>
        // /// <param name="palette"></param>
        // /// <param name="depth"></param>
        // /// <returns></returns>
        // /// <exception cref="ArgumentException"></exception>
        // public static ImageData Encode(Image image, Palette palette, int depth = 3)
        // {
        //     var maxColors = Math.Pow(2, depth);
        //
        //     if (palette.Colors.Count > maxColors)
        //     {
        //         throw new ArgumentException(
        //             $"Image has {palette.Colors.Count} colors, but depth {depth} only allows max {maxColors} colors",
        //             nameof(depth));
        //     }
        //
        //     var paletteIndex = new Dictionary<uint, int>();
        //     for (var i = 0; i < palette.Colors.Count; i++)
        //     {
        //         var color = palette.Colors[i];
        //         var colorId = (uint)color.R << 24 | (uint)color.G << 16 | (uint)color.B << 8 | (uint)color.A;
        //         paletteIndex[colorId] = i;
        //     }
        //
        //     const int bitsPerByte = 8;
        //     var bytesPerRow = (image.Width + 15) / 16 * 2;
        //
        //     var data = new byte[bytesPerRow * image.Height * depth];
        //
        //     image.ProcessPixelRows(accessor =>
        //     {
        //         // Color is pixel-agnostic, but it's implicitly convertible to the Rgba32 pixel type
        //         //Rgba32 transparent = Color.Transparent;
        //
        //         for (int y = 0; y < accessor.Height; y++)
        //         {
        //             Span<Rgba32> pixelRow = accessor.GetRowSpan(y);
        //
        //             // pixelRow.Length has the same value as accessor.Width,
        //             // but using pixelRow.Length allows the JIT to optimize away bounds checks:
        //             for (int x = 0; x < pixelRow.Length; x++)
        //             {
        //                 // get a reference to the pixel at position x
        //                 ref Rgba32 pixel = ref pixelRow[x];
        //
        //                 var colorId = pixel.ToHex().ToLower();
        //                 if (!paletteIndex.ContainsKey(colorId))
        //                 {
        //                     continue;
        //                 }
        //
        //                 var color = paletteIndex[colorId];
        //
        //                 for (var bitPlane = 0; bitPlane < depth; bitPlane++)
        //                 {
        //                     var colorBit = color & (1 << bitPlane);
        //                     if (colorBit == 0)
        //                     {
        //                         continue;
        //                     }
        //                     
        //                     var bitOffset = 7 - (x % bitsPerByte);
        //                     var imageDataOffset = (bytesPerRow * image.Height * bitPlane) + (y * bytesPerRow) + (x / bitsPerByte);
        //                     data[imageDataOffset] |= (byte)(1 << bitOffset);
        //                 }
        //             }
        //         }
        //     });
        //
        //     return new ImageData
        //     {
        //         TopEdge = 0,
        //         LeftEdge = 0,
        //         Width = (short)image.Width,
        //         Height = (short)image.Height,
        //         Depth = (byte)depth,
        //         ImageDataPointer = 1,
        //         PlanePick = (byte)(maxColors - 1),
        //         PlaneOnOff = 0,
        //         NextPointer = 0,
        //         Data = data,
        //     };
        // }
    }
}