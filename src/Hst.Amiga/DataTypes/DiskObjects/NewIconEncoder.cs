namespace Hst.Amiga.DataTypes.DiskObjects
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Imaging;

    /// <summary>
    /// encode image to new icon
    /// </summary>
    public static class NewIconEncoder
    {
        private const int MaxNewIconColors = 255;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public static NewIcon Encode(Image image)
        {
            var newIcon = new NewIcon
            {
                Width = (short)image.Width,
                Height = (short)image.Height,
                Depth = InfoHelper.CalculateDepth(image.Palette.Colors.Count),
            };

            var pixelData = new byte[image.Width * image.Height];

            if (image.BitsPerPixel <= 8)
            {
                Array.Copy(image.PixelData, 0, pixelData, 0, image.PixelData.Length);
                newIcon.Image = new Image(image.Width, image.Height, 8, image.IsTransparent, image.TransparentColor,
                    new Palette(image.Palette.Colors.Select(x => new Color((byte)x.R, (byte)x.G, (byte)x.B, (byte)x.A)),
                        image.IsTransparent),
                    pixelData);
                newIcon.Transparent = image.Palette.Colors[0].A == 0;
                return newIcon;
            }

            var colors = new List<Color>();
            var colorsIndex = new Dictionary<uint, int>();

            var transparentColor = -1;

            var pixelDataIterator = new ImagePixelDataIterator(image);

            var offset = 0;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    if (!pixelDataIterator.Next())
                    {
                        throw new InvalidOperationException();
                    }

                    var pixel = pixelDataIterator.Current;

                    var colorId = (uint)pixel.R << 24 | (uint)pixel.G << 16 | (uint)pixel.B << 8 | (uint)pixel.A;
                    if (!colorsIndex.ContainsKey(colorId))
                    {
                        colors.Add(new Color((byte)pixel.R, (byte)pixel.G, (byte)pixel.B, (byte)pixel.A));
                        colorsIndex[colorId] = colors.Count - 1;
                    }

                    var color = colorsIndex[colorId];

                    if (transparentColor == -1 && pixel.R == 0 && pixel.G == 0 && pixel.B == 0 && pixel.A == 0)
                    {
                        transparentColor = color;
                    }

                    pixelData[offset++] = (byte)color;
                }
            }

            if (colors.Count > MaxNewIconColors)
            {
                throw new ArgumentException(
                    $"Image has {colors.Count} colors and NewIcon only allows max {MaxNewIconColors} colors",
                    nameof(image));
            }

            // switch transparent color to first palette entry, if present and higher than 0
            if (transparentColor > 0)
            {
                // get transparent color
                var transparentColorR = colors[transparentColor].R;
                var transparentColorG = colors[transparentColor].G;
                var transparentColorB = colors[transparentColor].B;
                var transparentColorA = colors[transparentColor].A;

                // move first palette entry to transparent color entry
                colors[transparentColor] = new Color(colors[0].R, colors[0].G, colors[0].B, colors[0].A);

                // set first palette entry to transparent color
                colors[0] = new Color(transparentColorR, transparentColorG, transparentColorB, transparentColorA);
            }

            var isTransparent = transparentColor >= 0;
            newIcon.Image = new Image(newIcon.Width, newIcon.Height, newIcon.Depth, isTransparent, colors[0], 
                new Palette(colors, isTransparent), pixelData);
            newIcon.Transparent = isTransparent;

            return newIcon;
        }
    }
}