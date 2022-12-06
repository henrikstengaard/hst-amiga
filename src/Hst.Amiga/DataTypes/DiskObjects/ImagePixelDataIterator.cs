namespace HstWbInstaller.Core.IO.Info
{
    using System;
    using System.Linq;
    using Hst.Imaging;

    public class ImagePixelDataIterator
    {
        private readonly Image image;
        private readonly bool hasPalette;
        private Pixel pixel;
        private int pixelOffset;

        public ImagePixelDataIterator(Image image)
        {
            this.image = image;
            this.hasPalette = this.image.Palette.Colors.Any();
            this.pixelOffset = 0;
            this.pixel = null;
        }

        public Pixel Current => pixel;

        public bool Next()
        {
            if (this.pixelOffset >= this.image.PixelData.Length)
            {
                return false;
            }

            this.pixel = new Pixel();

            switch (this.image.BitsPerPixel)
            {
                case 8:
                    var paletteColor = this.image.PixelData[this.pixelOffset];

                    if (paletteColor >= this.image.Palette.Colors.Count)
                    {
                        throw new IndexOutOfRangeException($"Palette color {paletteColor} is out of range");
                    }

                    this.pixel.PaletteColor = paletteColor;
                    var color = this.image.Palette.Colors[this.pixel.PaletteColor];
                    this.pixel.R = color.R;
                    this.pixel.G = color.G;
                    this.pixel.B = color.B;
                    this.pixel.A = color.A;
                    break;
                case 24:
                case 32:
                    this.pixel.R = this.image.PixelData[this.pixelOffset];
                    this.pixel.G = this.image.PixelData[++this.pixelOffset];
                    this.pixel.B = this.image.PixelData[++this.pixelOffset];
                    this.pixel.A = this.image.BitsPerPixel == 32 ? this.image.PixelData[++this.pixelOffset] : 255;
                    this.pixel.PaletteColor = 0;
                    break;
            }

            this.pixelOffset++;

            return true;
        }
    }
}