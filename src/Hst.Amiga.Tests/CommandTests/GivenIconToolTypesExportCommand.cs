using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.ConsoleApp.Commands;
using Hst.Amiga.DataTypes.DiskObjects;
using Hst.Amiga.DataTypes.DiskObjects.ColorIcons;
using Hst.Amiga.DataTypes.DiskObjects.NewIcons;
using Hst.Amiga.DataTypes.DiskObjects.TrueColorIcons;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Amiga.Tests.CommandTests;

public class GivenIconToolTypesExportCommand
{
    [Fact]
    public async Task When_ExportingToolTypesFromDiskObject_Then_ToolTypesAreExported()
    {
        // arrange - paths
        var iconPath = $"{Guid.NewGuid()}.info";
        var toolTypesPath = $"{Guid.NewGuid()}.txt";

        // arrange - tool types to import
        var toolTypes = new []{ "TOOL1=TRUE", "TOOL2=TRUE" };
        
        try
        {
            // arrange - create disk object with tool types
            var diskObject = DiskObjectHelper.CreateProjectInfo();
            diskObject.ToolTypes = new ToolTypes
            {
                TextDatas = DiskObjectHelper.ConvertStringsToTextDatas(toolTypes)
            };
            diskObject.ToolTypesPointer = 1;

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
            
            // arrange - create icon tool types export command
            var command = new IconToolTypesExport(new NullLogger<IconToolTypesExport>(), iconPath, toolTypesPath, 
                true);
            
            // act - execute icon tool types export command
            var result = await command.Execute(CancellationToken.None);
            
            // assert - result is successful
            Assert.True(result.IsSuccess);

            // assert - tool types are exported
            Assert.True(File.Exists(toolTypesPath));
            var exportedToolTypes = await File.ReadAllLinesAsync(toolTypesPath);
            Assert.Equal(toolTypes.Length, exportedToolTypes.Length);
            Assert.Equal(toolTypes, exportedToolTypes);
        }
        finally
        {
            TestHelper.DeletePaths(iconPath, toolTypesPath);
        }
    }

    [Fact]
    public async Task When_ExportingToolTypesFromNewIcon_Then_ToolTypesAreExported()
    {
        // arrange - paths
        var iconPath = $"{Guid.NewGuid()}.info";
        var toolTypesPath = $"{Guid.NewGuid()}.txt";

        // arrange - tool types to import
        var toolTypes = new []{ "TOOL1=TRUE", "TOOL2=TRUE" };
        
        try
        {
            // arrange - create disk object with tool types
            var diskObject = DiskObjectHelper.CreateProjectInfo();
            diskObject.ToolTypes = new ToolTypes
            {
                TextDatas = DiskObjectHelper.ConvertStringsToTextDatas(toolTypes)
            };
            diskObject.ToolTypesPointer = 1;

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
            
            // arrange - create icon tool types export command
            var command = new IconToolTypesExport(new NullLogger<IconToolTypesExport>(), iconPath, toolTypesPath, 
                true);
            
            // act - execute icon tool types export command
            var result = await command.Execute(CancellationToken.None);
            
            // assert - result is successful
            Assert.True(result.IsSuccess);

            // assert - tool types are exported
            Assert.True(File.Exists(toolTypesPath));
            var exportedToolTypes = await File.ReadAllLinesAsync(toolTypesPath);
            Assert.Equal(toolTypes.Length, exportedToolTypes.Length);
            Assert.Equal(toolTypes, exportedToolTypes);
        }
        finally
        {
            TestHelper.DeletePaths(iconPath, toolTypesPath);
        }
    }
    
    [Fact]
    public async Task When_ExportingToolTypesFromColorIcon_Then_ToolTypesAreExported()
    {
        // arrange - paths
        var iconPath = $"{Guid.NewGuid()}.info";
        var toolTypesPath = $"{Guid.NewGuid()}.txt";

        // arrange - tool types to import
        var toolTypes = new []{ "TOOL1=TRUE", "TOOL2=TRUE" };
        
        try
        {
            // arrange - create disk object with tool types
            var diskObject = DiskObjectHelper.CreateProjectInfo();
            diskObject.ToolTypes = new ToolTypes
            {
                TextDatas = DiskObjectHelper.ConvertStringsToTextDatas(toolTypes)
            };
            diskObject.ToolTypesPointer = 1;
            
            // arrange - create color icon
            var image = AmigaIconHelper.CreateDefault8BppImage();
            var colorIcon = new ColorIcon();
            ColorIconHelper.SetFirstImage(colorIcon, image);

            // arrange - write icon
            var amigaIcon = new AmigaIcon
            {
                Kind = AmigaIcon.IconKind.Normal,
                DiskObject = diskObject,
                ColorIcon = colorIcon,
                TrueColorIcons = new List<TrueColorIcon>()
            };
            await using (var iconStream = File.OpenWrite(iconPath))
            {
                await AmigaIconHelper.WriteAmigaIcon(amigaIcon, iconStream);
            }
            
            // arrange - create icon tool types export command
            var command = new IconToolTypesExport(new NullLogger<IconToolTypesExport>(), iconPath, toolTypesPath, 
                true);
            
            // act - execute icon tool types export command
            var result = await command.Execute(CancellationToken.None);
            
            // assert - result is successful
            Assert.True(result.IsSuccess);

            // assert - tool types are exported
            Assert.True(File.Exists(toolTypesPath));
            var exportedToolTypes = await File.ReadAllLinesAsync(toolTypesPath);
            Assert.Equal(toolTypes.Length, exportedToolTypes.Length);
            Assert.Equal(toolTypes, exportedToolTypes);
        }
        finally
        {
            TestHelper.DeletePaths(iconPath, toolTypesPath);
        }
    }

    [Fact]
    public async Task When_ExportingToolTypesFromTrueColorIcon_Then_ToolTypesAreExported()
    {
        // arrange - paths
        var iconPath = $"{Guid.NewGuid()}.info";
        var toolTypesPath = $"{Guid.NewGuid()}.txt";

        // arrange - tool types to import
        var toolTypes = new []{ "TOOL1=TRUE", "TOOL2=TRUE" };
        
        try
        {
            // arrange - create disk object with tool types
            var diskObject = DiskObjectHelper.CreateProjectInfo();
            diskObject.ToolTypes = new ToolTypes
            {
                TextDatas = DiskObjectHelper.ConvertStringsToTextDatas(toolTypes)
            };
            diskObject.ToolTypesPointer = 1;
            
            // arrange - create true color icon
            var image = AmigaIconHelper.CreateDefault32BppImage();
            var trueColorIcon = await TrueColorIconHelper.CreateTrueColorIcon(image);

            // arrange - write icon
            var amigaIcon = new AmigaIcon
            {
                Kind = AmigaIcon.IconKind.TrueColor,
                DiskObject = diskObject,
                TrueColorIcons = new[] { trueColorIcon }
            };
            await using (var iconStream = File.OpenWrite(iconPath))
            {
                await AmigaIconHelper.WriteAmigaIcon(amigaIcon, iconStream);
            }
            
            // arrange - create icon tool types export command
            var command = new IconToolTypesExport(new NullLogger<IconToolTypesExport>(), iconPath, toolTypesPath, 
                true);
            
            // act - execute icon tool types export command
            var result = await command.Execute(CancellationToken.None);
            
            // assert - result is successful
            Assert.True(result.IsSuccess);

            // assert - tool types are exported
            Assert.True(File.Exists(toolTypesPath));
            var exportedToolTypes = await File.ReadAllLinesAsync(toolTypesPath);
            Assert.Equal(toolTypes.Length, exportedToolTypes.Length);
            Assert.Equal(toolTypes, exportedToolTypes);
        }
        finally
        {
            TestHelper.DeletePaths(iconPath, toolTypesPath);
        }
    }
}