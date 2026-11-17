namespace Hst.Amiga.Tests.VmTests;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConsoleApp;
using ConsoleApp.Commands;
using RigidDiskBlocks;
using Vm.FileSystem;
using Xunit;
using static GivenAmigaVmWithImages;

public class GivenVmRunCommandWithImages : IDisposable
{
    private readonly string directory;

    public GivenVmRunCommandWithImages()
    {
        directory = Path.Combine(Path.GetTempPath(), $"hst-amiga-vm-run-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
    }

    public void Dispose()
    {
        Directory.Delete(directory, true);
    }

    private async Task<string> WriteImage(string name, MemoryStream stream)
    {
        var path = Path.Combine(directory, name);
        await File.WriteAllBytesAsync(path, stream.ToArray());
        return path;
    }

    private static async Task<string> Run(string executablePath, bool imageContext)
    {
        var output = new MemoryStream();
        var command = new VmRunCommand(executablePath, Array.Empty<string>(), CreateOptions(output),
            imageContext: imageContext);

        var result = await command.Execute(null, CancellationToken.None);

        Assert.Null(result.Error);
        return Text(output);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_RunningExecutableInAdf_Then_ExecutableIsReadFromAdf(bool imageContext)
    {
        var adf = await WriteImage("workbench.adf", await CreateAdf("Workbench"));

        Assert.Equal("Hello from Workbench\n", await Run(adf + "\\C\\Hello", imageContext));
    }

    [Theory]
    [InlineData("\\C\\Hello", false, "Hello from Boot\n")]
    [InlineData("\\C\\Hello", true, "Hello from Boot\n")]
    [InlineData("\\rdb\\1\\C\\Hello", false, "Hello from Work\n")]
    [InlineData("\\rdb\\dh1\\C\\Hello", true, "Hello from System\n")]
    public async Task When_RunningExecutableInDiskImage_Then_ExecutableIsReadFromPartition(string path,
        bool imageContext, string expected)
    {
        var disk = await WriteImage("disk.hdf", await CreateDisk());

        Assert.Equal(expected, await Run(disk + path, imageContext));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_RunningExecutableInFileNotImage_Then_ExceptionIsThrown(bool imageContext)
    {
        var file = Path.Combine(directory, "notes.txt");
        await File.WriteAllTextAsync(file, "not an image");
        var command = new VmRunCommand(file + "\\C\\Hello", Array.Empty<string>(),
            CreateOptions(new MemoryStream()), imageContext: imageContext);

        var exception = await Assert.ThrowsAsync<IOException>(() => command.Execute(null, CancellationToken.None));

        Assert.Contains("is not an adf or disk image", exception.Message);
    }

    [Theory]
    [InlineData("run x.adf\\c\\list", false, "x.adf\\c\\list")]
    [InlineData("run --image-context x.adf\\c\\list", true, "x.adf\\c\\list")]
    [InlineData("run --image-context true x.adf\\c\\list", true, "x.adf\\c\\list")]
    [InlineData("run --image-context false x.adf\\c\\list", false, "x.adf\\c\\list")]
    public void When_ParsingImageContextOption_Then_ValueAndExecutableAreParsed(string commandLine, bool expected,
        string executable)
    {
        var parseResult = VmCommandFactory.CreateVmCommand().Parse(commandLine.Split(' '));

        Assert.Empty(parseResult.Errors);
        Assert.Equal(expected, parseResult.GetValue<bool>("--image-context"));
        Assert.Equal(executable, parseResult.GetValue<string>("Executable"));
    }

    [Fact]
    public async Task When_DetectingImages_Then_AdfAndDiskImagesWithRigidDiskBlockAreImages()
    {
        Assert.True(await ImageMounter.IsImage(await CreateAdf("Workbench")));
        Assert.True(await ImageMounter.IsImage(await CreateDisk()));
        Assert.False(await ImageMounter.IsImage(new MemoryStream(new byte[100])));
        Assert.False(await ImageMounter.IsImage(new MemoryStream(new byte[512 * 32])));

        // rigid disk block in sector 3
        var disk = new MemoryStream(new byte[512 * 32]);
        disk.Position = 512 * 3;
        disk.Write("RDSK"u8);
        disk.Position = 10;
        Assert.True(await ImageMounter.IsImage(disk));
        Assert.Equal(10, disk.Position);

        // rigid disk block after first 16 sectors is not detected
        var outside = new MemoryStream(new byte[512 * 32]);
        outside.Position = 512 * 16;
        outside.Write("RDSK"u8);
        Assert.False(await ImageMounter.IsImage(outside));
    }

    [Fact]
    public async Task When_FindingBootPartition_Then_BootablePartitionWithHighestBootPriorityIsFound()
    {
        var disk = await CreateDisk();
        var rigidDiskBlock = await RigidDiskBlockReader.Read(disk);
        var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();

        Assert.Equal(3, ImageMounter.FindBootPartition(partitionBlocks).Number);

        partitionBlocks[2].Flags |= (uint)PartitionBlock.PartitionFlagsEnum.NoMount;
        Assert.Equal(2, ImageMounter.FindBootPartition(partitionBlocks).Number);

        partitionBlocks[1].Flags &= ~(uint)PartitionBlock.PartitionFlagsEnum.Bootable;
        Assert.Equal("DH0", ImageMounter.FindBootPartition(partitionBlocks).PartitionBlock.DriveName);
    }
}
