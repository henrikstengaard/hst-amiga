using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.ConsoleApp;
using Hst.Amiga.ConsoleApp.Commands;
using Hst.Amiga.DataTypes.DiskObjects;
using Hst.Amiga.DataTypes.DiskObjects.NewIcons;
using Hst.Imaging;
using Hst.Imaging.Pngcs;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Amiga.Tests.CommandTests;

public class GivenIconImageImportCommand
{
    [Fact]
    public async Task When_ImportingNewIconImage1_Then_ImageIsImported()
    {
        // arrange - create paths
        var iconPath = $"{Guid.NewGuid()}.info";
        var image1Path = $"{Guid.NewGuid()}.png";

        var diskObject = DiskObjectHelper.CreateProjectInfo();
        
        try
        {
            using (var diskObjectStream = File.OpenWrite(iconPath))
            {
                await DiskObjectWriter.Write(diskObject, diskObjectStream);
            }
            
            // arrange - create image
            var image = new Image(1, 1, 8);
            image.Palette.AddColor(0, 0, 0);
            using (var imageStream = File.OpenWrite(image1Path))
            {
                PngWriter.Write(imageStream, image);
            }
            
            // arrange - create icon image import command
            var command = new IconImageImport(new NullLogger<IconImageImport>(), iconPath, ImageType.NewIcon,
                image1Path, null, false);

            // act - execute icon image import command
            var result = await command.Execute(CancellationToken.None);

            // assert - result is successful
            Assert.True(result.IsSuccess);
            
            // assert - icon file contains a true color icon
            await using var iconStream = File.OpenRead(iconPath);
            var amigaIcon = await AmigaIconHelper.ReadAmigaIcon(iconStream);
            Assert.NotNull(amigaIcon);

            // assert - disk object planar icon width and height are 1 and depth is 1 (2 colors)
            Assert.NotNull(amigaIcon.DiskObject.FirstImageData);
            Assert.Equal(1, amigaIcon.DiskObject.FirstImageData.Width);
            Assert.Equal(1, amigaIcon.DiskObject.FirstImageData.Height);
            Assert.Equal(1, amigaIcon.DiskObject.FirstImageData.Depth);
            
            // assert - disk object has tool types
            Assert.NotNull(amigaIcon.DiskObject.ToolTypes);
            
            // assert - tool types has new icon 1 with width and height of 1 and depth of 1 (2 colors)
            var newIconToolTypesDecoder = new NewIconToolTypesDecoder(amigaIcon.DiskObject.ToolTypes.TextDatas);
            var newIcon1 = newIconToolTypesDecoder.Decode(1);
            Assert.Equal(1, newIcon1.Width);
            Assert.Equal(1, newIcon1.Height);
            Assert.Equal(1, newIcon1.Depth);
        }
        finally
        {
            TestHelper.DeletePaths(iconPath, image1Path);
        }
    }

    [Fact]
    public async Task When_ImportingColorIconImage1_Then_ImageIsImported()
    {
        // arrange - create paths
        var iconPath = $"{Guid.NewGuid()}.info";
        var image1Path = $"{Guid.NewGuid()}.png";

        var diskObject = DiskObjectHelper.CreateProjectInfo();
        
        try
        {
            using (var diskObjectStream = File.OpenWrite(iconPath))
            {
                await DiskObjectWriter.Write(diskObject, diskObjectStream);
            }
            
            // arrange - create image
            var image = new Image(1, 1, 8);
            image.Palette.AddColor(0, 0, 0);
            using (var imageStream = File.OpenWrite(image1Path))
            {
                PngWriter.Write(imageStream, image);
            }
            
            // arrange - create icon image import command
            var command = new IconImageImport(new NullLogger<IconImageImport>(), iconPath, ImageType.ColorIcon,
                image1Path, null, false);

            // act - execute icon image import command
            var result = await command.Execute(CancellationToken.None);

            // assert - result is successful
            Assert.True(result.IsSuccess);
            
            // assert - icon file contains a true color icon
            await using var iconStream = File.OpenRead(iconPath);
            var amigaIcon = await AmigaIconHelper.ReadAmigaIcon(iconStream);
            Assert.NotNull(amigaIcon);

            // assert - disk object planar icon width and height are 1 and depth is 1 (2 colors)
            Assert.NotNull(amigaIcon.DiskObject.FirstImageData);
            Assert.Equal(1, amigaIcon.DiskObject.FirstImageData.Width);
            Assert.Equal(1, amigaIcon.DiskObject.FirstImageData.Height);
            Assert.Equal(1, amigaIcon.DiskObject.FirstImageData.Depth);

            // assert - color icon is present with width and height of 1 and depth of 1 (2 colors)
            Assert.NotNull(amigaIcon.ColorIcon);
            Assert.Single(amigaIcon.ColorIcon.Images);
            Assert.Equal(1, amigaIcon.ColorIcon.Images[0].Image.Width);
            Assert.Equal(1, amigaIcon.ColorIcon.Images[0].Image.Height);
            Assert.Equal(1, amigaIcon.ColorIcon.Images[0].Depth);
            
            // assert - disk object doesn't have any tool types
            Assert.Null(amigaIcon.DiskObject.ToolTypes);
            
            // assert - true color icons are not present
            Assert.Null(amigaIcon.TrueColorIcons);
        }
        finally
        {
            TestHelper.DeletePaths(iconPath, image1Path);
        }
    }

    [Fact]
    public async Task When_ImportingTrueColorIconImage1_Then_ImageIsImported()
    {
        // arrange - create paths
        var trueColorIconPath = Path.Combine("TestData", "DiskObjects", "AmigaPngIcon.info");
        var iconPath = $"{Guid.NewGuid()}.info";
        var image1Path = $"{Guid.NewGuid()}.png";

        try
        {
            // arrange - copy the true color icon to icon path
            File.Copy(trueColorIconPath, iconPath, true);

            var image = new Image(1, 1, 32);
            using (var imageStream = File.OpenWrite(image1Path))
            {
                PngWriter.Write(imageStream, image);
            }
            
            // arrange - create icon image import command
            var command = new IconImageImport(new NullLogger<IconImageImport>(), iconPath, ImageType.TrueColorIcon,
                image1Path, null, false);

            // act - execute icon image import command
            var result = await command.Execute(CancellationToken.None);

            // assert - result is successful
            Assert.True(result.IsSuccess);
        }
        finally
        {
            TestHelper.DeletePaths(iconPath, image1Path);
        }
    }
}