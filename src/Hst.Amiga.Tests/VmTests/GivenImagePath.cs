namespace Hst.Amiga.Tests.VmTests;

using System;
using System.IO;
using ConsoleApp.Commands;
using Xunit;

public class GivenImagePath : IDisposable
{
    private readonly string directory;

    public GivenImagePath()
    {
        directory = Path.Combine(Path.GetTempPath(), $"hst-amiga-image-path-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "workbench.adf"), new byte[1]);
        File.WriteAllBytes(Path.Combine(directory, "disk.vhd"), new byte[1]);
    }

    public void Dispose()
    {
        Directory.Delete(directory, true);
    }

    [Fact]
    public void When_ParsingPathInAdf_Then_ImageFileAndComponentsAreReturned()
    {
        Assert.True(ImagePath.TryParse(Path.Combine(directory, "workbench.adf") + "\\c\\list", out var imagePath));

        Assert.Equal(Path.Combine(directory, "workbench.adf"), imagePath.ImageFile);
        Assert.Equal(new[] { "c", "list" }, imagePath.Components);
        Assert.Equal("c/list", imagePath.GetAmigaPath(0));
        Assert.False(imagePath.IsRigidDiskBlockPath);
    }

    [Theory]
    [InlineData("\\rdb\\1\\c\\list", "1")]
    [InlineData("/rdb/dh0/c/list", "dh0")]
    public void When_ParsingPathInDiskImage_Then_PartitionAndPathAreReturned(string path, string partition)
    {
        Assert.True(ImagePath.TryParse(Path.Combine(directory, "disk.vhd") + path, out var imagePath));

        Assert.Equal(Path.Combine(directory, "disk.vhd"), imagePath.ImageFile);
        Assert.True(imagePath.IsRigidDiskBlockPath);
        Assert.Equal(partition, imagePath.Components[1]);
        Assert.Equal("c/list", imagePath.GetAmigaPath(2));
    }

    [Fact]
    public void When_ParsingPathWithoutImageFile_Then_PathIsNotAnImagePath()
    {
        Assert.False(ImagePath.TryParse(Path.Combine(directory, "missing.adf", "c", "list"), out _));
        Assert.False(ImagePath.TryParse(Path.Combine(directory, "workbench.adf"), out _));
    }
}
