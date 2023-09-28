namespace Hst.Amiga.DataTypes.DiskObjects
{
    using System.Linq;
    using Imaging;

    public static class ImageDataDecoder
    {
        /// <summary>
        /// decode image data using default amiga os 3.1 4 color palette
        /// </summary>
        /// <param name="imageData"></param>
        /// <returns></returns>
        public static Image Decode(ImageData imageData)
        {
            return Decode(imageData, AmigaOsPalette.FourColors(), true);
        }

        /// <summary>
        /// decode image data using palette
        /// </summary>
        /// <param name="imageData"></param>
        /// <param name="palette"></param>
        /// <param name="transparent"></param>
        /// <returns></returns>
        public static Image Decode(ImageData imageData, Palette palette, bool transparent)
        {
            if (transparent)
            {
                var firstColor = palette.Colors[0];
                var transparentColor = new Color(firstColor.R, firstColor.G, firstColor.B, 0);
                palette = new Palette(new[] { transparentColor }.Concat(palette.Colors.Skip(1)), 0);
            }
            
            var bitsPerByte = 8;
            var bytesPerRow = (imageData.Width + 15) / 16 * 2;

            var image = new byte[imageData.Width * imageData.Height];

            var plane = 0;

            var xOffset = 0;
            var y = 0;
            for (var i = 0; i < imageData.Data.Length; i++)
            {
                // loop each byte
                // each byte represent 8 pixels horizontally
                for (var bit = 0; bit < bitsPerByte; bit++)
                {
                    var x = xOffset + bit;
                    var color = ((imageData.Data[i] >> (7 - bit)) & 1) << plane;

                    if (x < imageData.Width)
                    {
                        image[y * imageData.Width + x] |= (byte)color;
                    }
                }

                xOffset += 8;

                if (xOffset >= bytesPerRow * 8)
                {
                    y++;
                    xOffset = 0;
                }

                if (y >= imageData.Height)
                {
                    y = 0;
                    plane++;
                }
            }

            var imageRgbaData = new byte[imageData.Width * imageData.Height];

            var tx = 0;
            var ty = 0;
            for (var i = 0; i < image.Length; i++)
            {
                var srcOffset = ty * imageData.Width + tx;
                //var destOffset = srcOffset * 4;

                // var color = image[i];
                // if (color >= palette.Colors.Count)
                // {
                //     color = 0;
                // }

                imageRgbaData[srcOffset] = image[i];
                // imageRgbaData[destOffset + 1] = (byte)palette.Colors[color].B;
                // imageRgbaData[destOffset + 2] = (byte)palette.Colors[color].G;
                // imageRgbaData[destOffset + 3] = (byte)palette.Colors[color].A;

                tx++;
                if (tx < imageData.Width)
                {
                    continue;
                }

                tx = 0;
                ty++;
            }

            return new Image(imageData.Width, imageData.Height, 8, palette, imageRgbaData);
        }
    }
}