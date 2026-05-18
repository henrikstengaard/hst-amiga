using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.ConsoleApp;
using Hst.Amiga.ConsoleApp.Commands;
using Hst.Amiga.DataTypes.DiskObjects;
using Hst.Amiga.DataTypes.DiskObjects.ColorIcons;
using Hst.Imaging.Pngcs;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Amiga.Tests.CommandTests;

public class GivenIconImageConvertCommand
{
    [Fact]
    public async Task When_ConvertingColorIconToTrueColorIcon_Then_IconIsTrueColorIcon()
    {
        // arrange - paths
        var iconPath = $"{Guid.NewGuid()}.info";
        const ImageType srcType = ImageType.ColorIcon;
        const ImageType destType = ImageType.TrueColorIcon;
        var palettePath = string.Empty;
        const bool deleteIcons = true;

        try
        {
            // arrange - create color icon
            var image = AmigaIconHelper.CreateDefault8BppImage();
            var diskObject = DiskObjectHelper.CreateProjectInfo();
            var colorIcon = new ColorIcon();
            ColorIconHelper.SetFirstImage(colorIcon, image);
            using (var iconStream = File.OpenWrite(iconPath))
            {
                await AmigaIconHelper.WriteAmigaIcon(new AmigaIcon
                {
                    DiskObject = diskObject,
                    ColorIcon = colorIcon
                }, iconStream);
            }
            
            // arrange - create icon convert command
            var iconConvertCommand = new IconImageConvert(
                new NullLogger<IconImageConvert>(),
                iconPath,
                srcType,
                destType,
                palettePath,
                deleteIcons);

            // act - execute icon convert command
            var result = await iconConvertCommand.Execute(CancellationToken.None);

            // assert - result is successful
            Assert.True(result.IsSuccess);
            
            // assert - icon file contains a amiga icon
            AmigaIcon amigaIcon;
            using (var iconStream = File.OpenRead(iconPath))
            {
                amigaIcon = await AmigaIconHelper.ReadAmigaIcon(iconStream);
            }
            Assert.NotNull(amigaIcon);

            // assert - new icon is not present, new icons are part of tool types
            Assert.Null(amigaIcon.DiskObject.ToolTypes);

            // assert - no color icons are present
            Assert.Null(amigaIcon.ColorIcon);
            
            // assert - 1 true color icon is present
            Assert.Single(amigaIcon.TrueColorIcons);
            var trueColorIcon = amigaIcon.TrueColorIcons[0];
            Assert.Equal(1, trueColorIcon.Image.Width);
            Assert.Equal(1, trueColorIcon.Image.Height);
            Assert.Equal(32, trueColorIcon.Image.BitsPerPixel);
        }
        finally
        {
            TestHelper.DeletePaths(iconPath);
        }
    }

    [Fact]
    public async Task When_ConvertingTrueColorIconToColorIcon_Then_IconIsColorIcon()
    {
        // arrange - paths
        var iconPath = $"{Guid.NewGuid()}.info";
        const ImageType srcType = ImageType.TrueColorIcon;
        const ImageType destType = ImageType.ColorIcon;
        var palettePath = string.Empty;
        const bool deleteIcons = true;

        try
        {
            // arrange - create true color icon
            var image = AmigaIconHelper.CreateDefault32BppImage();
            using (var iconStream = File.OpenWrite(iconPath))
            {
                PngWriter.Write(iconStream, image);
            }
            
            // arrange - create icon convert command
            var iconConvertCommand = new IconImageConvert(
                new NullLogger<IconImageConvert>(),
                iconPath,
                srcType,
                destType,
                palettePath,
                deleteIcons);

            // act - execute icon convert command
            var result = await iconConvertCommand.Execute(CancellationToken.None);

            // assert - result is successful
            Assert.True(result.IsSuccess);
            
            // assert - icon file contains a color icon
            AmigaIcon amigaIcon;
            using (var iconStream = File.OpenRead(iconPath))
            {
                amigaIcon = await AmigaIconHelper.ReadAmigaIcon(iconStream);
            }
            Assert.NotNull(amigaIcon);

            // assert - new icon is not present, new icons are part of tool types
            Assert.Null(amigaIcon.DiskObject.ToolTypes);

            // assert - no true color icons are present
            Assert.Empty(amigaIcon.TrueColorIcons);
            
            // assert - 1 color icon is present
            Assert.Single(amigaIcon.ColorIcon.Images);
            var colorIcon = amigaIcon.ColorIcon.Images[0];
            Assert.Equal(1, colorIcon.Image.Width);
            Assert.Equal(1, colorIcon.Image.Height);
            Assert.Equal(8, colorIcon.Image.BitsPerPixel);
        }
        finally
        {
            TestHelper.DeletePaths(iconPath);
        }
    }

    [Fact]
    public async Task When_ConvertingTrueColorIconToColorIconWithoutTrueColorIcon_Then_ErrorIsReturned()
    {
        // arrange - paths
        var iconPath = $"{Guid.NewGuid()}.info";
        const ImageType srcType = ImageType.TrueColorIcon;
        const ImageType destType = ImageType.ColorIcon;
        var palettePath = string.Empty;
        const bool deleteIcons = true;

        try
        {
            // arrange - create planar icon
            var diskObject = DiskObjectHelper.CreateProjectInfo();
            DiskObjectHelper.SetFirstImage(diskObject, ImageDataEncoder.Encode(AmigaIconHelper.CreateDefault32BppImage()));
            using (var iconStream = File.OpenWrite(iconPath))
            {
                await DiskObjectWriter.Write(diskObject, iconStream);
            }
            
            // arrange - create icon convert command
            var iconConvertCommand = new IconImageConvert(
                new NullLogger<IconImageConvert>(),
                iconPath,
                srcType,
                destType,
                palettePath,
                deleteIcons);

            // act - execute icon convert command
            var result = await iconConvertCommand.Execute(CancellationToken.None);

            // assert - result is faulted
            Assert.True(result.IsFaulted);
        }
        finally
        {
            TestHelper.DeletePaths(iconPath);
        }
    }
}