namespace Hst.Amiga.DataTypes.DiskObjects.ColorIcons
{
    using System;
    using System.Linq;
    using Imaging;

    public static class ColorIconHelper
    {
        /// <summary>
        /// Set first color icon image. Second color icon image is removed, if not width and height are equal 
        /// </summary>
        /// <param name="colorIcon"></param>
        /// <param name="image"></param>
        public static void SetFirstImage(ColorIcon colorIcon, Image image)
        {
            colorIcon.Width = image.Width;
            colorIcon.Height = image.Height;
            colorIcon.Flags = 1; // borderless
            colorIcon.Aspect = 0;
            colorIcon.MaxPalBytes = 0;
            colorIcon.Images = new[]
            {
                new ColorIconImage
                {
                    Image = image,
                    Depth = DiskObjectHelper.CalculateDepth(image.Palette.Colors.Count)
                }
            }.Concat(colorIcon.Images.Skip(1).Where(x => x.Image.Width == image.Width && x.Image.Height == image.Height)
                .Take(1)).ToArray();
        }

        /// <summary>
        /// Set second color icon image
        /// </summary>
        /// <param name="colorIcon"></param>
        /// <param name="image"></param>
        /// <exception cref="ArgumentException"></exception>
        public static void SetSecondImage(ColorIcon colorIcon, Image image)
        {
            if (image.Width != colorIcon.Width)
            {
                throw new ArgumentException(
                    $"Image width {image.Width} is not equal to color icon width {colorIcon.Width}", nameof(image));
            }

            if (image.Height != colorIcon.Height)
            {
                throw new ArgumentException(
                    $"Image height {image.Height} is not equal to color icon height {colorIcon.Height}", nameof(image));
            }

            if (!colorIcon.Images.Any())
            {
                throw new ArgumentException($"Color icon doesn't have first image", nameof(colorIcon));
            }

            colorIcon.Images = colorIcon.Images.Take(1).Concat(new[]
            {
                new ColorIconImage
                {
                    Image = image,
                    Depth = DiskObjectHelper.CalculateDepth(image.Palette.Colors.Count)
                }
            }).ToArray();
        }
    }
}