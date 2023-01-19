namespace Hst.Amiga.DataTypes.DiskObjects
{
    using System;
    using System.Linq;
    using Imaging;

    public static class NewIconDecoder
    {
        public static Image Decode(NewIcon newIcon)
        {
            // var pixelData = new byte[newIcon.Width * newIcon.Height * 4];
            //
            // var pixelDataOffset = 0;
            // var imagePixelsOffset = 0;
            // for (var y = 0; y < newIcon.Height; y++)
            // {
            //     for (var x = 0; x < newIcon.Width; x++)
            //     {
            //         var pixel = newIcon.ImagePixels[imagePixelsOffset++];
            //         var color = newIcon.Palette[pixel];
            //
            //         pixelData[pixelDataOffset] = color[0]; // r
            //         pixelData[pixelDataOffset + 1] = color[1]; // g
            //         pixelData[pixelDataOffset + 2] = color[2]; // b
            //         pixelData[pixelDataOffset + 3] = color[3]; // a
            //
            //         pixelDataOffset += 4;
            //     }
            // }
            //
            var pixelData = new byte[newIcon.Image.PixelData.Length];
            Array.Copy(newIcon.Image.PixelData, 0, pixelData, 0, newIcon.Image.PixelData.Length);
            var colors = newIcon.Image.Palette.Colors.Select(x => new Color(x.R, x.G, x.B, x.A)).ToList();

            return new Image(newIcon.Width, newIcon.Height, 8, true, colors[0], new Palette(colors), pixelData);
        }
        
        // public static BitmapImage DecodeToBitmap(NewIcon newIcon)
        // {
        //     var bitsPerPixel = 8;
        //     var scanline = ((bitsPerPixel * newIcon.Width + 31) / 32) * 4;
        //
        //     var palette = new Color[256];
        //     for (var i = 0; i < palette.Length; i++)
        //     {
        //         palette[i] = new Color();
        //         if (i >= newIcon.Palette.Length)
        //         {
        //             continue;
        //         }
        //
        //         palette[i].R = newIcon.Palette[i][0];
        //         palette[i].G = newIcon.Palette[i][1];
        //         palette[i].B = newIcon.Palette[i][2];
        //         palette[i].A = newIcon.Palette[i][3];
        //     }
        //     
        //     var pixelData = new byte[scanline * newIcon.Height];
        //
        //     var imagePixelsOffset = 0;
        //     for (var y = 0; y < newIcon.Height; y++)
        //     {
        //         for (var x = 0; x < newIcon.Width; x++)
        //         {
        //             var paletteColor = newIcon.ImagePixels[imagePixelsOffset++];
        //             pixelData[scanline * (newIcon.Height - y - 1) + x] = paletteColor;
        //         }
        //     }
        //
        //     return new BitmapImage(newIcon.Width, newIcon.Height, bitsPerPixel, palette, pixelData);
        // }
    }
}