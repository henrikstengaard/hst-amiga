using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.ConsoleApp;
using Hst.Amiga.ConsoleApp.Commands;
using Hst.Amiga.DataTypes.DiskObjects;
using Hst.Amiga.DataTypes.DiskObjects.TrueColorIcons;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Constants = Hst.Amiga.DataTypes.DiskObjects.TrueColorIcons.Constants;

namespace Hst.Amiga.Tests.CommandTests;

public class GivenIconImageCreateCommand
{
    [Fact]
    public async Task When_CreatingPlanarIcon_Then_PlanarIconIsCreated()
    {
        // arrange - paths
        var iconPath = $"{Guid.NewGuid()}.info";
        
        try
        {
            // arrange - create icon create command
            var iconCreateCommand = new IconCreateCommand(
                new NullLogger<IconCreateCommand>(),
                iconPath,
                IconType.Project,
                10,
                10,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                ImageType.Planar,
                null,
                null);
            
            // act - execute icon create command
            var result = await iconCreateCommand.Execute(CancellationToken.None);

            // assert - result is successful
            Assert.True(result.IsSuccess);
            
            // assert - icon file contains a true color icon
            await using var iconStream = File.OpenRead(iconPath);
            var amigaIcon = await AmigaIconHelper.ReadAmigaIcon(iconStream);
            Assert.NotNull(amigaIcon);
            
            // assert - disk object contains icon position, stack size and type
            Assert.Equal(10, amigaIcon.DiskObject.CurrentX);
            Assert.Equal(10, amigaIcon.DiskObject.CurrentY);
            Assert.Equal(4096, amigaIcon.DiskObject.StackSize);
            Assert.Equal(4, amigaIcon.DiskObject.Type);
            
            // assert - disk object planar icon width and height are 1 and depth is 1 (2 colors)
            Assert.NotNull(amigaIcon.DiskObject.FirstImageData);
            Assert.Equal(1, amigaIcon.DiskObject.FirstImageData.Width);
            Assert.Equal(1, amigaIcon.DiskObject.FirstImageData.Height);
            Assert.Equal(1, amigaIcon.DiskObject.FirstImageData.Depth);
            
            // assert - new icon is not present, new icons are part of tool types
            Assert.Null(amigaIcon.DiskObject.ToolTypes);
            
            // assert - color icon is not present
            Assert.Null(amigaIcon.ColorIcon);
            
            // assert - true color icons are not present
            Assert.Null(amigaIcon.TrueColorIcons);
        }
        finally
        {
            TestHelper.DeletePaths(iconPath);
        }
    }

    [Fact]
    public async Task When_CreatingNewIcon_Then_IconWithNewIconIsCreated()
    {
        // arrange - paths
        var iconPath = $"{Guid.NewGuid()}.info";
        
        try
        {
            // arrange - create icon create command
            var iconCreateCommand = new IconCreateCommand(
                new NullLogger<IconCreateCommand>(),
                iconPath,
                IconType.Project,
                10,
                10,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                ImageType.NewIcon,
                null,
                null);
            
            // act - execute icon create command
            var result = await iconCreateCommand.Execute(CancellationToken.None);

            // assert - result is successful
            Assert.True(result.IsSuccess);
            
            // assert - icon file contains a true color icon
            await using var iconStream = File.OpenRead(iconPath);
            var amigaIcon = await AmigaIconHelper.ReadAmigaIcon(iconStream);
            Assert.NotNull(amigaIcon);
            
            // assert - disk object contains icon position, stack size and type
            Assert.Equal(10, amigaIcon.DiskObject.CurrentX);
            Assert.Equal(10, amigaIcon.DiskObject.CurrentY);
            Assert.Equal(4096, amigaIcon.DiskObject.StackSize);
            Assert.Equal(4, amigaIcon.DiskObject.Type);
            
            // assert - new icon is not present, new icons are part of tool types
            Assert.Null(amigaIcon.DiskObject.ToolTypes);
            
            // assert - color icon is not present
            Assert.Null(amigaIcon.ColorIcon);
            
            // assert - true color icons are not present
            Assert.Null(amigaIcon.TrueColorIcons);
        }
        finally
        {
            TestHelper.DeletePaths(iconPath);
        }
    }

    [Fact]
    public async Task When_CreatingColorIcon_Then_IconWithColorIconIsCreated()
    {
        // arrange - paths
        var iconPath = $"{Guid.NewGuid()}.info";
        
        try
        {
            // arrange - create icon create command
            var iconCreateCommand = new IconCreateCommand(
                new NullLogger<IconCreateCommand>(),
                iconPath,
                IconType.Project,
                10,
                10,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                ImageType.ColorIcon,
                null,
                null);
            
            // act - execute icon create command
            var result = await iconCreateCommand.Execute(CancellationToken.None);

            // assert - result is successful
            Assert.True(result.IsSuccess);
            
            // assert - icon file contains a true color icon
            await using var iconStream = File.OpenRead(iconPath);
            var amigaIcon = await AmigaIconHelper.ReadAmigaIcon(iconStream);
            Assert.NotNull(amigaIcon);
            
            // assert - disk object contains icon position, stack size and type
            Assert.Equal(10, amigaIcon.DiskObject.CurrentX);
            Assert.Equal(10, amigaIcon.DiskObject.CurrentY);
            Assert.Equal(4096, amigaIcon.DiskObject.StackSize);
            Assert.Equal(4, amigaIcon.DiskObject.Type);
            
            // assert - color icon is not present
            Assert.Null(amigaIcon.ColorIcon);
            
            // assert - true color icons are not present
            Assert.Null(amigaIcon.TrueColorIcons);
        }
        finally
        {
            TestHelper.DeletePaths(iconPath);
        }
    }

    [Fact]
    public async Task When_CreatingTrueColorIcon_Then_TrueColorIconIsCreated()
    {
        // arrange - paths
        var iconPath = $"{Guid.NewGuid()}.info";
        
        try
        {
            // arrange - create icon create command
            var iconCreateCommand = new IconCreateCommand(
                new NullLogger<IconCreateCommand>(),
                iconPath,
                IconType.Project,
                10,
                10,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                ImageType.TrueColorIcon,
                null,
                null);
            
            // act - execute icon create command
            var result = await iconCreateCommand.Execute(CancellationToken.None);

            // assert - result is successful
            Assert.True(result.IsSuccess);
            
            // assert - icon file contains a true color icon
            await using var iconStream = File.OpenRead(iconPath);
            var amigaIcon = await AmigaIconHelper.ReadAmigaIcon(iconStream);
            Assert.NotNull(amigaIcon);
            Assert.NotNull(amigaIcon.TrueColorIcons);
            var trueColorIcons = amigaIcon.TrueColorIcons.ToList();
            Assert.Single(trueColorIcons);
            
            // assert - true color icon has a icon chunk
            var iconChunk = trueColorIcons[0].Chunks.FirstOrDefault(chunk => chunk.Type.SequenceEqual(
                Constants.PngChunkTypes.Icon));
            Assert.NotNull(iconChunk);
            
            // act - read icon data from true color icon
            var iconData = IconChunkReader.ReadIconChunkData(iconChunk.Data);
            Assert.NotNull(iconData);

            // assert - icon data contain 4 icon attribute tags
            Assert.Equal(4, iconData.IconTags.Count);

            // assert - iconx, icony, stacksize and type tags are present in icon data with expected values
            Assert.Equal(Constants.IconAttributeTags.ATTR_ICONX, iconData.IconTags[0].Tag);
            Assert.Equal(10U, iconData.IconTags[0].Value);
            Assert.Equal(Constants.IconAttributeTags.ATTR_ICONY, iconData.IconTags[1].Tag);
            Assert.Equal(10U, iconData.IconTags[1].Value);
            Assert.Equal(Constants.IconAttributeTags.ATTR_STACKSIZE, iconData.IconTags[2].Tag);
            Assert.Equal(4096U, iconData.IconTags[2].Value);
            Assert.Equal(Constants.IconAttributeTags.ATTR_TYPE, iconData.IconTags[3].Tag);
            Assert.Equal(4U, iconData.IconTags[3].Value);

            // assert - true color icon has width and height 1 and bpp is 32
            Assert.Equal(1, trueColorIcons[0].Image.Width);
            Assert.Equal(1, trueColorIcons[0].Image.Height);
            Assert.Equal(32, trueColorIcons[0].Image.BitsPerPixel);
        }
        finally
        {
            TestHelper.DeletePaths(iconPath);
        }
    }
}