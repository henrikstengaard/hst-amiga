namespace Hst.Amiga.DataTypes.DiskObjects.NewIcons
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Hst.Imaging;

    public static class NewIconConverter
    {
        /// <summary>
        /// Convert new icon to image
        /// </summary>
        /// <param name="newIcon"></param>
        /// <returns></returns>
        public static Image ToImage(NewIcon newIcon)
        {
            var palette = new Palette(newIcon.Palette);
            if (newIcon.Transparent)
            {
                palette.TransparentColor = 0;
            }

            var pixelData = new byte[newIcon.Data.Length];
            Array.Copy(newIcon.Data, 0, pixelData, 0, newIcon.Data.Length);

            return new Image(newIcon.Width, newIcon.Height, 8, palette, pixelData);
        }
        
        /// <summary>
        /// Convert image to new icon
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public static NewIcon ToNewIcon(Image image)
        {
            return image.BitsPerPixel <= 8 && image.Palette.Colors.Count > 0
                ? CreateFromIndexedImage(image)
                : CreateFromImage(image); 
        }

        private static NewIcon CreateFromIndexedImage(Image image)
        {
            var colors = image.Palette.Colors.Select(x => new Color((byte)x.R, (byte)x.G, (byte)x.B, (byte)x.A)).ToArray();

            SwapTransparentColor(colors, image.Palette.TransparentColor);
            
            var pixelData = new byte[image.PixelData.Length];
            Array.Copy(image.PixelData, 0, pixelData, 0, image.PixelData.Length);
            
            return new NewIcon
            {
                Width = (short)image.Width,
                Height = (short)image.Height,
                Depth = DiskObjectHelper.CalculateDepth(image.Palette.Colors.Count),
                Palette = colors,
                Data = pixelData,
                Transparent = image.Palette.IsTransparent
            };
        }

        private static NewIcon CreateFromImage(Image image)
        {
            var pixelData = new byte[image.Width * image.Height];
            
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

                    // first transparent color is used as transparent color
                    if (transparentColor == -1 && pixel.A == 0)
                    {
                        transparentColor = color;
                    }

                    pixelData[offset++] = (byte)color;
                }
            }

            if (colors.Count > Constants.NewIcon.MaxNewIconColors)
            {
                throw new ArgumentException(
                    $"Image has {colors.Count} colors and NewIcon only allows max {Constants.NewIcon.MaxNewIconColors} colors",
                    nameof(image));
            }

            var colorsArray = colors.ToArray();
            SwapTransparentColor(colorsArray, transparentColor);
            
            return new NewIcon
            {
                Width = (short)image.Width,
                Height = (short)image.Height,
                Depth = DiskObjectHelper.CalculateDepth(image.Palette.Colors.Count),
                Palette = colorsArray,
                Data = pixelData,
                Transparent = image.Palette.IsTransparent
            };
        }

        private static void SwapTransparentColor(Color[] colors, int transparentColor)
        {
            // return, if transparent color is not set or is first color
            if (transparentColor <= 0)
            {
                return;
            }
            
            // swap transparent color and color zero via deconstruction
            (colors[transparentColor], colors[0]) = (colors[0], colors[transparentColor]);
        }
    }
}