namespace Hst.Amiga.Tests.VmTests;

using System;
using System.IO;
using Vm.FileSystem;
using Xunit;

public class GivenVolumeManager : IDisposable
{
    private readonly string root;
    private readonly VolumeManager volumes;

    public GivenVolumeManager()
    {
        root = Path.Combine(Path.GetTempPath(), $"hst-amiga-vm-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "C"));
        Directory.CreateDirectory(Path.Combine(root, "Work", "Sub"));
        File.WriteAllText(Path.Combine(root, "Work", "ReadMe.txt"), "test");
        volumes = new VolumeManager();
        volumes.AddVolume("SYS", root);
        volumes.AddAssign("C", "SYS:C");
        volumes.AddAssign("WORK", "SYS:Work");
    }

    public void Dispose()
    {
        Directory.Delete(root, true);
    }

    [Fact]
    public void When_ResolvingAbsolutePath_Then_HostPathIsReturned()
    {
        var path = volumes.Resolve("sys:work/readme.txt", null);

        Assert.NotNull(path);
        Assert.Equal("SYS:Work/ReadMe.txt", path.FullPath);
        Assert.Equal(Path.Combine(root, "Work", "ReadMe.txt"), path.HostPath);
    }

    [Fact]
    public void When_ResolvingAssign_Then_AssignTargetIsUsed()
    {
        var path = volumes.Resolve("WORK:Sub", null);

        Assert.Equal("SYS:Work/Sub", path.FullPath);
    }

    [Theory]
    [InlineData("ReadMe.txt", "SYS:Work/ReadMe.txt")]
    [InlineData("/C", "SYS:C")]
    [InlineData("Sub//ReadMe.txt", "SYS:Work/ReadMe.txt")]
    [InlineData(":C", "SYS:C")]
    [InlineData("Sub/", "SYS:Work/Sub")]
    [InlineData("", "SYS:Work")]
    [InlineData("/", "SYS:")]
    public void When_ResolvingRelativePath_Then_PathIsRelativeToCurrentDirectory(string relativePath,
        string expectedPath)
    {
        var currentDirectory = volumes.Resolve("SYS:Work", null);

        var path = volumes.Resolve(relativePath, currentDirectory);

        Assert.Equal(expectedPath, path.FullPath);
    }

    [Theory]
    [InlineData("SYS:/")]
    [InlineData("SYS:..")]
    [InlineData("UNKNOWN:file")]
    public void When_ResolvingInvalidPath_Then_NullIsReturned(string amigaPath)
    {
        Assert.Null(volumes.Resolve(amigaPath, null));
    }
}
