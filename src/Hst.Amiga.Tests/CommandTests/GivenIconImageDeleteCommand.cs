using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.ConsoleApp;
using Hst.Amiga.ConsoleApp.Commands;
using Hst.Amiga.DataTypes.DiskObjects;
using Hst.Amiga.DataTypes.DiskObjects.ColorIcons;
using Hst.Amiga.DataTypes.DiskObjects.NewIcons;
using Hst.Imaging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Amiga.Tests.CommandTests;

public class GivenIconImageDeleteCommand
{
    [Fact]
    public async Task When_DeletingPlanarIconImages_Then_PlanarIconImagesAreDeleted()
    {
        // arrange - create paths
        var iconPath = $"{Guid.NewGuid()}.info";
        var image1Path = $"{Guid.NewGuid()}.png";

        var diskObject = DiskObjectHelper.CreateProjectInfo();

        try
        {
            // arrange - create image
            var image = new Image(1, 1, 8);
            image.Palette.AddColor(0, 0, 0);
            
            // arrange - write disk object to icon path
            using (var diskObjectStream = File.OpenWrite(iconPath))
            {
                await DiskObjectWriter.Write(diskObject, diskObjectStream);
            }
            
            // arrange - create icon image delete command
            var command = new IconImageDelete(new NullLogger<IconImageDelete>(), iconPath, ImageType.Planar);

            // act - execute icon image delete command
            var result = await command.Execute(CancellationToken.None);

            // assert - result is successful
            Assert.True(result.IsSuccess);

            // assert - icon path contains a disk object
            await using var iconStream = File.OpenRead(iconPath);
            var actualDiskObject = await DiskObjectReader.Read(iconStream);
            Assert.NotNull(actualDiskObject);
            
            // assert - icon stream doesn't contain more than disk object
            Assert.Equal(iconStream.Length, iconStream.Position);
            
            // assert - disk object planar icon width and height are 1 and depth is 1 (2 colors)
            Assert.NotNull(actualDiskObject.FirstImageData);
            Assert.Equal(1, actualDiskObject.FirstImageData.Width);
            Assert.Equal(1, actualDiskObject.FirstImageData.Height);
            Assert.Equal(1, actualDiskObject.FirstImageData.Depth);
        }
        finally
        {
            TestHelper.DeletePaths(iconPath, image1Path);
        }
    }

    [Fact]
    public async Task When_DeletingNewIconImages_Then_NewIconImagesAreDeleted()
    {
        // arrange - create paths
        var iconPath = $"{Guid.NewGuid()}.info";
        var image1Path = $"{Guid.NewGuid()}.png";

        var diskObject = DiskObjectHelper.CreateProjectInfo();

        try
        {
            // arrange - create image
            var image = new Image(1, 1, 8);
            image.Palette.AddColor(0, 0, 0);

            // arrange - set new icon image 1
            var newIcon = NewIconConverter.ToNewIcon(image);
            NewIconHelper.SetFirstImage(diskObject, newIcon);
            
            // arrange - write disk object to icon path
            using (var diskObjectStream = File.OpenWrite(iconPath))
            {
                await DiskObjectWriter.Write(diskObject, diskObjectStream);
            }
            
            // arrange - create icon image delete command
            var command = new IconImageDelete(new NullLogger<IconImageDelete>(), iconPath, ImageType.NewIcon);

            // act - execute icon image delete command
            var result = await command.Execute(CancellationToken.None);

            // assert - result is successful
            Assert.True(result.IsSuccess);

            // assert - icon path contains a disk object
            await using var iconStream = File.OpenRead(iconPath);
            var actualDiskObject = await DiskObjectReader.Read(iconStream);
            Assert.NotNull(actualDiskObject);
            
            // assert - icon stream doesn't contain more than disk object
            Assert.Equal(iconStream.Length, iconStream.Position);
            
            // assert - disk object planar icon width and height are 1 and depth is 1 (2 colors)
            Assert.NotNull(actualDiskObject.FirstImageData);
            Assert.Equal(1, actualDiskObject.FirstImageData.Width);
            Assert.Equal(1, actualDiskObject.FirstImageData.Height);
            Assert.Equal(1, actualDiskObject.FirstImageData.Depth);

            // assert - no tool types are present. new icons are encoded as tool types.
            Assert.NotNull(actualDiskObject.ToolTypes);
            Assert.Empty(actualDiskObject.ToolTypes.TextDatas);
        }
        finally
        {
            TestHelper.DeletePaths(iconPath, image1Path);
        }
    }

    [Fact]
    public async Task When_DeletingColorIconImages_Then_ColorIconImagesAreDeleted()
    {
        // arrange - create paths
        var iconPath = $"{Guid.NewGuid()}.info";
        var image1Path = $"{Guid.NewGuid()}.png";

        var diskObject = DiskObjectHelper.CreateProjectInfo();

        try
        {
            // arrange - create image
            var image = new Image(1, 1, 8);
            image.Palette.AddColor(0, 0, 0);

            // arrange - create color icon
            var colorIcon = new ColorIcon();
            ColorIconHelper.SetFirstImage(colorIcon, image);

            // arrange - write disk object and color icon to icon path
            using (var diskObjectStream = File.OpenWrite(iconPath))
            {
                await DiskObjectWriter.Write(diskObject, diskObjectStream);
                await ColorIconWriter.Write(diskObjectStream, colorIcon, false, false);
            }
            
            // arrange - create icon image delete command
            var command = new IconImageDelete(new NullLogger<IconImageDelete>(), iconPath, ImageType.ColorIcon);

            // act - execute icon image delete command
            var result = await command.Execute(CancellationToken.None);

            // assert - result is successful
            Assert.True(result.IsSuccess);

            // assert - icon path contains a disk object
            await using var iconStream = File.OpenRead(iconPath);
            var actualDiskObject = await DiskObjectReader.Read(iconStream);
            Assert.NotNull(actualDiskObject);
            
            // assert - icon stream doesn't any color icon
            Assert.Equal(iconStream.Length, iconStream.Position);
            
            // assert - disk object planar icon width and height are 1 and depth is 1 (2 colors)
            Assert.NotNull(actualDiskObject.FirstImageData);
            Assert.Equal(1, actualDiskObject.FirstImageData.Width);
            Assert.Equal(1, actualDiskObject.FirstImageData.Height);
            Assert.Equal(1, actualDiskObject.FirstImageData.Depth);
        }
        finally
        {
            TestHelper.DeletePaths(iconPath, image1Path);
        }
    }
    
    [Fact]
    public async Task When_DeletingTrueColorIconImages_Then_TrueColorIconImagesAreDeleted()
    {
        // arrange - create paths
        var trueColorIconPath = Path.Combine("TestData", "DiskObjects", "AmigaPngIcon.info");
        var iconPath = $"{Guid.NewGuid()}.info";

        try
        {
            // arrange - copy the true color icon to icon path
            File.Copy(trueColorIconPath, iconPath, true);

            // arrange - create icon image delete command
            var command = new IconImageDelete(new NullLogger<IconImageDelete>(), iconPath, ImageType.TrueColorIcon);

            // act - execute icon image delete command
            var result = await command.Execute(CancellationToken.None);

            // assert - result is successful
            Assert.True(result.IsSuccess);

            // assert - icon path contains a disk object.
            // true color icon is converted to a disk object
            await using var iconStream = File.OpenRead(iconPath);
            var diskObject = await DiskObjectReader.Read(iconStream);
            Assert.NotNull(diskObject);
            
            // assert - icon stream doesn't any color icon
            Assert.Equal(iconStream.Length, iconStream.Position);
            
            // assert - disk object planar icon width and height are 1 and depth is 1 (2 colors)
            Assert.NotNull(diskObject.FirstImageData);
            Assert.Equal(1, diskObject.FirstImageData.Width);
            Assert.Equal(1, diskObject.FirstImageData.Height);
            Assert.Equal(1, diskObject.FirstImageData.Depth);
        }
        finally
        {
            TestHelper.DeletePaths(iconPath);
        }
    }
}