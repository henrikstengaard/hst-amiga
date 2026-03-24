using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.ConsoleApp;
using Hst.Amiga.ConsoleApp.Commands;
using Hst.Amiga.DataTypes.DiskObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Amiga.Tests.CommandTests;

public class GivenIconImageExportCommandWithTrueColorIcon
{
    [Fact]
    public async Task When_ExportTrueColorIconImage1_Then_ImageIsExported()
    {
        // arrange - create paths
        var trueColorIconPath = Path.Combine("TestData", "DiskObjects", "AmigaPngIcon.info");
        var iconPath = $"{Guid.NewGuid()}.info";
        var image1Path = $"image1_{Guid.NewGuid()}.png";

        try
        {
            // arrange - copy the true color icon to icon path
            File.Copy(trueColorIconPath, iconPath, true);

            // arrange - create icon image export command
            var command = new IconImageExport(new NullLogger<IconImageExport>(), iconPath, ImageType.TrueColorIcon,
                image1Path, null, null);

            // act - execute icon image export command
            var result = await command.Execute(CancellationToken.None);

            // assert - result is successful
            Assert.True(result.IsSuccess);

            // assert - image 1 file is created
            Assert.True(File.Exists(image1Path));
        }
        finally
        {
            TestHelper.DeletePaths(iconPath, image1Path);
        }
    }
        
    [Fact]
    public async Task When_ExportTrueColorIconImage2_Then_ImageIsExported()
    {
        // arrange - create paths
        var trueColorIconPath = Path.Combine("TestData", "DiskObjects", "AmigaPngIcon.info");
        var iconPath = $"{Guid.NewGuid()}.info";
        var image2Path = $"image2_{Guid.NewGuid()}.png";

        try
        {
            // arrange - copy the true color icon to icon path
            File.Copy(trueColorIconPath, iconPath, true);

            // arrange - create icon image export command
            var command = new IconImageExport(new NullLogger<IconImageExport>(), iconPath, ImageType.TrueColorIcon,
                null, image2Path, null);

            // act - execute icon image export command
            var result = await command.Execute(CancellationToken.None);

            // assert - result is successful
            Assert.True(result.IsSuccess);

            // assert - image 2 file is created
            Assert.True(File.Exists(image2Path));
        }
        finally
        {
            TestHelper.DeletePaths(iconPath, image2Path);
        }
    }

    [Fact]
    public async Task When_ExportTrueColorIconImage1ThatDoesntExist_Then_ErrorIsReturned()
    {
        // arrange - create paths
        var iconPath = $"icon_{Guid.NewGuid()}.info";
        var image1Path = $"image1_{Guid.NewGuid()}.png";
        
        // arrange - create new project disk object icon
        var diskObject = DiskObjectHelper.CreateProjectInfo();
        
        try
        {
            // arrange - write icon data
            using (var fileStream = File.Create(iconPath))
            {
                await DiskObjectWriter.Write(diskObject, fileStream);
            }
        
            // arrange - create icon image export command
            var command = new IconImageExport(new NullLogger<IconImageExport>(), iconPath, ImageType.TrueColorIcon, image1Path, null, null);

            // act - execute icon image export command
            var result = await command.Execute(CancellationToken.None);
        
            // assert - result is not successful
            Assert.True(result.IsFaulted);
            Assert.False(result.IsSuccess);
        }
        finally
        {
            TestHelper.DeletePaths(iconPath, image1Path);
        }
    }
    
    [Fact]
    public async Task When_ExportTrueColorIconImage2ThatDoesntExist_Then_ErrorIsReturned()
    {
        // arrange - create paths
        var iconPath = $"icon_{Guid.NewGuid()}.info";
        var image2Path = $"image2_{Guid.NewGuid()}.png";
        
        // arrange - create new project disk object icon
        var diskObject = DiskObjectHelper.CreateProjectInfo();
        
        try
        {
            // arrange - write icon data
            using (var fileStream = File.Create(iconPath))
            {
                await DiskObjectWriter.Write(diskObject, fileStream);
            }
        
            // arrange - create icon image export command
            var command = new IconImageExport(new NullLogger<IconImageExport>(), iconPath, ImageType.TrueColorIcon, null, image2Path, null);

            // act - execute icon image export command
            var result = await command.Execute(CancellationToken.None);
        
            // assert - result is not successful
            Assert.True(result.IsFaulted);
            Assert.False(result.IsSuccess);
        }
        finally
        {
            TestHelper.DeletePaths(iconPath, image2Path);
        }
    }
}