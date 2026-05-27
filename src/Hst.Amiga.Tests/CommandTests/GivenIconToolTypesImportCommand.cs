using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.ConsoleApp.Commands;
using Hst.Amiga.DataTypes.DiskObjects;
using Hst.Amiga.DataTypes.DiskObjects.NewIcons;
using Hst.Amiga.DataTypes.DiskObjects.TrueColorIcons;
using Hst.Amiga.Tests.DiskObjectTests;
using Hst.Core.Extensions;
using Hst.Imaging.Pngcs;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Amiga.Tests.CommandTests;

public class GivenIconToolTypesImportCommand
{
    [Fact]
    public async Task When_ImportingToolTypesToDiskObject_Then_ToolTypesAreImported()
    {
        // arrange - paths
        var iconPath = $"{Guid.NewGuid()}.info";
        var toolTypesPath = $"{Guid.NewGuid()}.txt";
        
        // arrange - tool types to import
        var toolTypes = string.Join("\n", "TOOL1=TRUE", "TOOL2=TRUE");
        
        try
        {
            // arrange - create disk object
            var diskObject = DiskObjectHelper.CreateProjectInfo();
            
            // arrange - write icon
            var amigaIcon = new AmigaIcon
            {
                Kind = AmigaIcon.IconKind.Normal,
                DiskObject = diskObject,
                TrueColorIcons = new List<TrueColorIcon>()
            };
            await using (var iconStream = File.OpenWrite(iconPath))
            {
                await AmigaIconHelper.WriteAmigaIcon(amigaIcon, iconStream);
            }

            // arrange - write tool types
            await File.WriteAllTextAsync(toolTypesPath, toolTypes);

            // arrange - create icon tool types import command
            var command = new IconToolTypesImport(new NullLogger<IconToolTypesImport>(), iconPath, toolTypesPath, 
                true);
            
            // act - execute icon tool types import command
            var result = await command.Execute(CancellationToken.None);
            
            // assert - result is successful
            Assert.True(result.IsSuccess);

            // assert - read icon
            await using (var iconStream = File.OpenRead(iconPath))
            {
                amigaIcon = await AmigaIconHelper.ReadAmigaIcon(iconStream);
            }
            
            // assert - disk object has tool types imported
            Assert.NotNull(amigaIcon.DiskObject.ToolTypes);
            Assert.NotEmpty(amigaIcon.DiskObject.ToolTypes.TextDatas);
            var textDatas = amigaIcon.DiskObject.ToolTypes.TextDatas.ToList();
            Assert.Equal(2, textDatas.Count);
            Assert.Equal("TOOL1=TRUE", AmigaTextHelper.GetString(textDatas[0].Data, 0,
                (int)textDatas[0].Size - 1));
            Assert.Equal("TOOL2=TRUE", AmigaTextHelper.GetString(textDatas[1].Data, 0,
                (int)textDatas[1].Size - 1));
        }
        finally
        {
            TestHelper.DeletePaths(iconPath, toolTypesPath);
        }
    }

    [Fact]
    public async Task When_ImportingToolTypesToNewIcon_Then_ToolTypesAreImported()
    {
        // arrange - paths
        var iconPath = $"{Guid.NewGuid()}.info";
        var toolTypesPath = $"{Guid.NewGuid()}.txt";
        
        // arrange - tool types to import
        var toolTypes = string.Join("\n", "TOOL1=TRUE", "TOOL2=TRUE");

        try
        {
            // arrange - create disk object
            var diskObject = DiskObjectHelper.CreateProjectInfo();

            // arrange - create new icon
            var image = AmigaIconHelper.CreateDefault8BppImage();
            var newIcon = NewIconConverter.ToNewIcon(image);
            NewIconHelper.SetFirstImage(diskObject, newIcon);
            
            // arrange - write icon
            var amigaIcon = new AmigaIcon
            {
                Kind = AmigaIcon.IconKind.Normal,
                DiskObject = diskObject,
                TrueColorIcons = new List<TrueColorIcon>()
            };
            await using (var iconStream = File.OpenWrite(iconPath))
            {
                await AmigaIconHelper.WriteAmigaIcon(amigaIcon, iconStream);
            }

            // arrange - write tool types
            await File.WriteAllTextAsync(toolTypesPath, toolTypes);

            // arrange - create icon tool types import command
            var command = new IconToolTypesImport(new NullLogger<IconToolTypesImport>(), iconPath, toolTypesPath, 
                true);
            
            // act - execute icon tool types import command
            var result = await command.Execute(CancellationToken.None);
            
            // assert - result is successful
            Assert.True(result.IsSuccess);

            // assert - read icon
            await using (var iconStream = File.OpenRead(iconPath))
            {
                amigaIcon = await AmigaIconHelper.ReadAmigaIcon(iconStream);
            }
            
            // assert - disk object has tool types imported
            Assert.NotNull(amigaIcon.DiskObject.ToolTypes);
            Assert.NotEmpty(amigaIcon.DiskObject.ToolTypes.TextDatas);
            var textDatas = amigaIcon.DiskObject.ToolTypes.TextDatas.ToList();
            Assert.True(textDatas.Count >= 2);
            Assert.Equal("TOOL1=TRUE", AmigaTextHelper.GetString(textDatas[0].Data, 0,
                (int)textDatas[0].Size - 1));
            Assert.Equal("TOOL2=TRUE", AmigaTextHelper.GetString(textDatas[1].Data, 0,
                (int)textDatas[1].Size - 1));
        }
        finally
        {
            TestHelper.DeletePaths(iconPath, toolTypesPath);
        }
    }

    [Fact]
    public async Task When_ImportingToolTypesToColorIcon_Then_ToolTypesAreImported()
    {
        // arrange - paths
        var iconPath = $"{Guid.NewGuid()}.info";
        var toolTypesPath = $"{Guid.NewGuid()}.txt";
        
        // arrange - tool types to import
        var toolTypes = string.Join("\n", "TOOL1=TRUE", "TOOL2=TRUE");

        // arrange - create disk object
        var diskObject = DiskObjectHelper.CreateDiskInfo();
        
        // arrange - create color icon data with odd size chunks to verify that color icon data is preserved after importing tool types
        var colorIconData = TestDataHelper.CreateColorIconDataWithOddSizeChunks();

        // arrange - create icon data with disk object and color icon data
        byte[] iconData;
        using (var iconStream = new MemoryStream())
        {
            await DiskObjectWriter.Write(diskObject, iconStream);
            iconStream.Write(colorIconData, 0, colorIconData.Length);
            iconData = iconStream.ToArray();
        }

        try
        {
            // arrange - write icon data
            await File.WriteAllBytesAsync(iconPath, iconData);
            
            // arrange - write tool types
            await File.WriteAllTextAsync(toolTypesPath, toolTypes);

            // arrange - create icon tool types import command
            var command = new IconToolTypesImport(new NullLogger<IconToolTypesImport>(), iconPath, toolTypesPath, 
                true);
            
            // act - execute icon tool types import command
            var result = await command.Execute(CancellationToken.None);
            
            // assert - result is successful
            Assert.True(result.IsSuccess);
            
            // assert - disk object has tool types imported
            await using var iconStream = File.OpenRead(iconPath);
            var actualDiskObject = await DiskObjectReader.Read(iconStream);
            Assert.NotNull(actualDiskObject);
            Assert.NotNull(actualDiskObject.ToolTypes);
            var textDatas = actualDiskObject.ToolTypes.TextDatas.ToList();
            Assert.Equal(2, textDatas.Count);
            Assert.Equal("TOOL1=TRUE", AmigaTextHelper.GetString(textDatas[0].Data, 0,
                (int)textDatas[0].Size - 1));
            Assert.Equal("TOOL2=TRUE", AmigaTextHelper.GetString(textDatas[1].Data, 0,
                (int)textDatas[1].Size - 1));
            
            // assert - color icon data is preserved
            var actualColorIconData = iconStream.Position < iconStream.Length
                ? await iconStream.ReadBytes((int)(iconStream.Length - iconStream.Position))
                : Array.Empty<byte>();
            Assert.Equal(colorIconData.Length, actualColorIconData.Length);
            Assert.Equal(colorIconData, actualColorIconData);
        }
        finally
        {
            if (File.Exists(iconPath))
            {
                File.Delete(iconPath);
            }

            if (File.Exists(toolTypesPath))
            {
                File.Delete(toolTypesPath);
            }
        }
    }

    [Fact]
    public async Task When_ImportingToolTypesToTrueColorIcon_Then_ToolTypesAreImported()
    {
        // arrange - paths
        var iconPath = $"{Guid.NewGuid()}.info";
        var toolTypesPath = $"{Guid.NewGuid()}.txt";
        
        // arrange - tool types to import
        var toolTypes = string.Join("\n", "TOOL1=TRUE", "TOOL2=TRUE");
        
        try
        {
            // arrange - create true color icon
            var image = AmigaIconHelper.CreateDefault32BppImage();
            await using (var iconStream = File.OpenWrite(iconPath))
            {
                PngWriter.Write(iconStream, image);
            }

            // arrange - write tool types
            await File.WriteAllTextAsync(toolTypesPath, toolTypes);

            // arrange - create icon tool types import command
            var command = new IconToolTypesImport(new NullLogger<IconToolTypesImport>(), iconPath, toolTypesPath, 
                true);
            
            // act - execute icon tool types import command
            var result = await command.Execute(CancellationToken.None);
            
            // assert - result is successful
            Assert.True(result.IsSuccess);

            // assert - read icon
            AmigaIcon amigaIcon;
            await using (var iconStream = File.OpenRead(iconPath))
            {
                amigaIcon = await AmigaIconHelper.ReadAmigaIcon(iconStream);
            }
            
            // assert - disk object has tool types imported
            Assert.NotNull(amigaIcon.DiskObject.ToolTypes);
            Assert.NotEmpty(amigaIcon.DiskObject.ToolTypes.TextDatas);
            var textDatas = amigaIcon.DiskObject.ToolTypes.TextDatas.ToList();
            Assert.Equal(2, textDatas.Count);
            Assert.Equal("TOOL1=TRUE", AmigaTextHelper.GetString(textDatas[0].Data, 0,
                (int)textDatas[0].Size - 1));
            Assert.Equal("TOOL2=TRUE", AmigaTextHelper.GetString(textDatas[1].Data, 0,
                (int)textDatas[1].Size - 1));
        }
        finally
        {
            TestHelper.DeletePaths(iconPath, toolTypesPath);
        }
    }
}