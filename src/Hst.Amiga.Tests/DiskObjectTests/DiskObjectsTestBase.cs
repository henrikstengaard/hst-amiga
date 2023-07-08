namespace Hst.Amiga.Tests.DiskObjectTests
{
    using System;
    using Imaging;
    using Xunit;

    public abstract class DiskObjectsTestBase
    {
        protected static void AssertEqual(Image source, Image destination)
        {
            Assert.Equal(source.Width, destination.Width);
            Assert.Equal(source.Height, destination.Height);

            // assert palette, if both source and destination is 8 bpp or less and has colors in palette
            if (source.BitsPerPixel <= 8 && destination.BitsPerPixel <= 8 && 
                source.Palette.Colors.Count > 0 && destination.Palette.Colors.Count > 0)
            {
                for (var i = 0; i < Math.Min(source.Palette.Colors.Count, destination.Palette.Colors.Count); i++)
                {
                    Assert.Equal(source.Palette.Colors[i].R, destination.Palette.Colors[i].R);
                    Assert.Equal(source.Palette.Colors[i].G, destination.Palette.Colors[i].G);
                    Assert.Equal(source.Palette.Colors[i].B, destination.Palette.Colors[i].B);

                    if (source.BitsPerPixel == 32 && source.BitsPerPixel == destination.BitsPerPixel)
                    {
                        Assert.Equal(source.Palette.Colors[i].A, destination.Palette.Colors[i].A);
                    }
                }
            }
            
            var sourcePixelIterator = new ImagePixelDataIterator(source);
            var destPixelIterator = new ImagePixelDataIterator(destination);

            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    Assert.True(sourcePixelIterator.Next());
                    Assert.True(destPixelIterator.Next());
                    
                    var sourcePixel = sourcePixelIterator.Current;
                    var destPixel = destPixelIterator.Current;
                    
                    Assert.Equal(sourcePixel.R, destPixel.R);
                    Assert.Equal(sourcePixel.G, destPixel.G);
                    Assert.Equal(sourcePixel.B, destPixel.B);
                    
                    if (source.BitsPerPixel == 32 && source.BitsPerPixel == destination.BitsPerPixel)
                    {
                        Assert.Equal(sourcePixel.A, destPixel.A);
                    }
                }
            }
        }
    }
}