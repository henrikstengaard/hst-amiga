namespace Hst.Amiga.Tests.DiskObjectTests;

using System.IO;
using System.Threading.Tasks;
using Core.Extensions;
using DataTypes.DiskObjects;
using DataTypes.DiskObjects.ColorIcons;
using Xunit;

public class GivenColorIconReaderAndColorIconWriter
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task WhenWriteAndReadColorIconThenColorIconImagesMatches(bool compressImage, bool compressPalette)
    {
        // arrange - read color icon
        await using var colorIconStream = File.OpenRead(Path.Combine("TestData", "DiskObjects", "ColorIcons", "AF-OS35-Icons1.readme.info"));
        var diskObject = await DiskObjectReader.Read(colorIconStream);
        var colorIcon = await ColorIconReader.Read(colorIconStream);

        // act - write disk object and color icon
        var stream = new MemoryStream();
        await DiskObjectWriter.Write(diskObject, stream);
        await ColorIconWriter.Write(stream, colorIcon, compressImage, compressPalette);

        // act - read disk object and color icon
        stream.Position = 0;
        var actualDiskObject = await DiskObjectReader.Read(stream);
        var actualColorIcon = await ColorIconReader.Read(stream);
            
        // assert - color icon matches
        Assert.Equal(colorIcon.Width, actualColorIcon.Width);
        Assert.Equal(colorIcon.Height, actualColorIcon.Height);
        Assert.Equal(colorIcon.Aspect, actualColorIcon.Aspect);
        Assert.Equal(colorIcon.Flags, actualColorIcon.Flags);
        Assert.Equal(colorIcon.Images.Length, actualColorIcon.Images.Length);

        // assert - color icon images
        for (var i = 0; i < colorIcon.Images.Length; i++)
        {
            // assert - color icon image matches
            Assert.Equal(colorIcon.Images[i].Image.Width, actualColorIcon.Images[i].Image.Width);
            Assert.Equal(colorIcon.Images[i].Image.Height, actualColorIcon.Images[i].Image.Height);
            Assert.Equal(colorIcon.Images[i].Image.BitsPerPixel, actualColorIcon.Images[i].Image.BitsPerPixel);
                
            // assert - color icon image palette matches
            Assert.Equal(colorIcon.Images[i].Image.Palette.Colors.Count, actualColorIcon.Images[i].Image.Palette.Colors.Count);
            for (var p = 0; p < colorIcon.Images[i].Image.Palette.Colors.Count; p++)
            {
                Assert.Equal(colorIcon.Images[i].Image.Palette.Colors[p].R, actualColorIcon.Images[i].Image.Palette.Colors[p].R);
                Assert.Equal(colorIcon.Images[i].Image.Palette.Colors[p].G, actualColorIcon.Images[i].Image.Palette.Colors[p].G);
                Assert.Equal(colorIcon.Images[i].Image.Palette.Colors[p].B, actualColorIcon.Images[i].Image.Palette.Colors[p].B);
            }

            // assert - color icon image pixel data matches
            Assert.Equal(colorIcon.Images[i].Image.PixelData.Length, actualColorIcon.Images[i].Image.PixelData.Length);
            Assert.Equal(colorIcon.Images[i].Image.PixelData, actualColorIcon.Images[i].Image.PixelData);
        }
    }
}