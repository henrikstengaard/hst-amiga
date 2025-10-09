using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.ConsoleApp;
using Hst.Amiga.ConsoleApp.Commands;
using Hst.Amiga.DataTypes.DiskObjects;
using Hst.Amiga.Tests.DiskObjectTests;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Amiga.Tests.CommandTests;

public class GivenIconImageExportCommand
{
    [Fact]
    public async Task When_ExportPlanarIconImage1_Then_ImageIsExported()
    {
        // arrange - create paths
        var iconPath = $"icon_{Guid.NewGuid()}.info";
        var image1Path = $"image1_{Guid.NewGuid()}.png";
        
        // arrange - create first planar icon
        var firstImage = TestDataHelper.CreateFirstImage();

        // arrange - create new project disk object icon
        var diskObject = DiskObjectHelper.CreateProjectInfo();

        // arrange - set first image
        DiskObjectHelper.SetFirstImage(diskObject, ImageDataEncoder.Encode(firstImage, TestDataHelper.Depth));

        try
        {
            using (var fileStream = File.Create(iconPath))
            {
                await DiskObjectWriter.Write(diskObject, fileStream);
            }
        
            // arrange - create icon image export command
            var command = new IconImageExport(new NullLogger<IconImageExport>(), iconPath, ImageType.Planar, image1Path, null, null);

            // act - execute icon image export command
            var result = await command.Execute(CancellationToken.None);
        
            // assert - result is successful
            Assert.True(result.IsSuccess);
            
            // assert - image 1 file is created
            Assert.True(File.Exists(image1Path));
        }
        finally
        {
            if (File.Exists(iconPath))
            {
                File.Delete(iconPath);
            }

            if (File.Exists(image1Path))
            {
                File.Delete(image1Path);
            }
        }
    }
    
    [Fact]
    public async Task When_ExportPlanarIconImage2_Then_ImageIsExported()
    {
        // arrange - create paths
        var iconPath = $"icon_{Guid.NewGuid()}.info";
        var image2Path = $"image2_{Guid.NewGuid()}.png";
        
        // arrange - create first planar icon
        var firstImage = TestDataHelper.CreateFirstImage();

        // arrange - create new project disk object icon
        var diskObject = DiskObjectHelper.CreateProjectInfo();

        // arrange - set second image
        DiskObjectHelper.SetSecondImage(diskObject, ImageDataEncoder.Encode(firstImage, TestDataHelper.Depth));

        try
        {
            using (var fileStream = File.Create(iconPath))
            {
                await DiskObjectWriter.Write(diskObject, fileStream);
            }
        
            // arrange - create icon image export command
            var command = new IconImageExport(new NullLogger<IconImageExport>(), iconPath, ImageType.Planar, null, image2Path, null);

            // act - execute icon image export command
            var result = await command.Execute(CancellationToken.None);
        
            // assert - result is successful
            Assert.True(result.IsSuccess);
            
            // assert - image 2 file is created
            Assert.True(File.Exists(image2Path));
        }
        finally
        {
            if (File.Exists(iconPath))
            {
                File.Delete(iconPath);
            }

            if (File.Exists(image2Path))
            {
                File.Delete(image2Path);
            }
        }
    }
    
    [Fact]
    public async Task When_ExportPlanarIconImage1ThatDoesntExist_Then_ErrorIsReturned()
    {
        // arrange - create paths
        var iconPath = $"icon_{Guid.NewGuid()}.info";
        var image1Path = $"image1_{Guid.NewGuid()}.png";
        
        // arrange - create new project disk object icon
        var diskObject = DiskObjectHelper.CreateProjectInfo();

        try
        {
            using (var fileStream = File.Create(iconPath))
            {
                await DiskObjectWriter.Write(diskObject, fileStream);
            }
        
            // arrange - create icon image export command
            var command = new IconImageExport(new NullLogger<IconImageExport>(), iconPath, ImageType.Planar, image1Path, null, null);

            // act - execute icon image export command
            var result = await command.Execute(CancellationToken.None);
        
            // assert - result is not successful
            Assert.True(result.IsFaulted);
            Assert.False(result.IsSuccess);
        }
        finally
        {
            if (File.Exists(iconPath))
            {
                File.Delete(iconPath);
            }

            if (File.Exists(image1Path))
            {
                File.Delete(image1Path);
            }
        }
    }

    [Fact]
    public async Task When_ExportPlanarIconImage2ThatDoesntExist_Then_ErrorIsReturned()
    {
        // arrange - create paths
        var iconPath = $"icon_{Guid.NewGuid()}.info";
        var image2Path = $"image2_{Guid.NewGuid()}.png";
        
        // arrange - create new project disk object icon
        var diskObject = DiskObjectHelper.CreateProjectInfo();

        try
        {
            using (var fileStream = File.Create(iconPath))
            {
                await DiskObjectWriter.Write(diskObject, fileStream);
            }
        
            // arrange - create icon image export command
            var command = new IconImageExport(new NullLogger<IconImageExport>(), iconPath, ImageType.Planar, null, image2Path, null);

            // act - execute icon image export command
            var result = await command.Execute(CancellationToken.None);
        
            // assert - result is not successful
            Assert.True(result.IsFaulted);
            Assert.False(result.IsSuccess);
        }
        finally
        {
            if (File.Exists(iconPath))
            {
                File.Delete(iconPath);
            }

            if (File.Exists(image2Path))
            {
                File.Delete(image2Path);
            }
        }
    }
}