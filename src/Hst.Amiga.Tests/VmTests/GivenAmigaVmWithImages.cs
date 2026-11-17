namespace Hst.Amiga.Tests.VmTests;

using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Extensions;
using Extensions;
using FileSystems;
using FileSystems.FastFileSystem;
using HunkTests;
using RigidDiskBlocks;
using Vm;
using Xunit;
using static M68kAssembler;
using FileMode = FileSystems.FileMode;
using Size = M68kAssembler.Size;

public class GivenAmigaVmWithImages
{
    private const int OpenLibrary = -552;
    private const int PutStr = -948;

    private static readonly byte[] FfsDosType = { 0x44, 0x4f, 0x53, 0x3 };

    /// <summary>
    /// Executable writing message to output.
    /// </summary>
    private static byte[] CreateHelloExecutable(string message) =>
        new HunkFileBuilder().AddCode(new M68kAssembler()
            .Move(Size.Long, AbsW(4), A(6))
            .Lea(Pc("dosname"), 1)
            .Moveq(36, 0)
            .CallLib(OpenLibrary)
            .Move(Size.Long, D(0), A(6))
            .Lea(Pc("message"), 0)
            .Move(Size.Long, A(0), D(1))
            .CallLib(PutStr)
            .Moveq(0, 0)
            .Rts()
            .Label("message").String(message)
            .Label("dosname").String("dos.library")
            .ToArray()).Build();

    private static async Task WriteFile(IFileSystemVolume volume, string directory, string name, byte[] data)
    {
        await volume.ChangeDirectory("/");
        if (directory != null)
        {
            if ((await volume.FindEntry(directory)).Entry == null)
            {
                await volume.CreateDirectory(directory);
            }

            await volume.ChangeDirectory(directory);
        }

        await using var stream = await volume.OpenFile(name, FileMode.Write, true, true);
        await stream.WriteAsync(data);
    }

    private static async Task<byte[]> ReadFile(IFileSystemVolume volume, string name)
    {
        await volume.ChangeDirectory("/");
        await using var stream = await volume.OpenFile(name, FileMode.Read, false, true);
        var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }

    internal static async Task<MemoryStream> CreateAdf(string diskName)
    {
        var stream = new MemoryStream(new byte[FloppyDiskConstants.DoubleDensity.Size]);
        await FastFileSystemFormatter.Format(stream, FloppyDiskConstants.DoubleDensity.LowCyl,
            FloppyDiskConstants.DoubleDensity.HighCyl, FloppyDiskConstants.DoubleDensity.ReservedBlocks,
            FloppyDiskConstants.DoubleDensity.Heads, FloppyDiskConstants.DoubleDensity.Sectors,
            FloppyDiskConstants.BlockSize, FloppyDiskConstants.FileSystemBlockSize, FfsDosType, diskName);

        await using var volume = await FastFileSystemVolume.MountAdf(stream);
        await WriteFile(volume, "C", "Hello", CreateHelloExecutable($"Hello from {diskName}\n"));
        await WriteFile(volume, "S", "file.txt", Encoding.ASCII.GetBytes("text"));
        return stream;
    }

    /// <summary>
    /// Create disk image with rigid disk block and partitions DH0 "Work" not bootable, DH1 "System" bootable with
    /// boot priority 0 and DH2 "Boot" bootable with boot priority 2.
    /// </summary>
    internal static async Task<MemoryStream> CreateDisk()
    {
        var stream = new MemoryStream();
        var rigidDiskBlock = RigidDiskBlock.Create((12L * 1024 * 1024).ToUniversalSize());
        stream.SetLength(rigidDiskBlock.DiskSize);
        const long partitionSize = 3L * 1024 * 1024;
        rigidDiskBlock.AddFileSystem(FfsDosType, Encoding.ASCII.GetBytes("$VER: FileSystem 0.1 (01/01/22) "))
            .AddPartition("DH0", partitionSize)
            .AddPartition("DH1", partitionSize, bootable: true)
            .AddPartition("DH2", partitionSize, bootable: true);
        rigidDiskBlock.PartitionBlocks.Last().BootPriority = 2;
        await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);

        var names = new[] { "Work", "System", "Boot" };
        var partitionBlocks = rigidDiskBlock.PartitionBlocks.ToList();
        for (var i = 0; i < partitionBlocks.Count; i++)
        {
            await FastFileSystemFormatter.FormatPartition(stream, partitionBlocks[i], names[i]);
            await using var volume = await FastFileSystemVolume.MountPartition(stream, partitionBlocks[i]);
            await WriteFile(volume, "C", "Hello", CreateHelloExecutable($"Hello from {names[i]}\n"));
        }

        return stream;
    }

    internal static VmOptions CreateOptions(MemoryStream output, string input = "")
    {
        var options = new VmOptions
        {
            Input = new MemoryStream(Encoding.ASCII.GetBytes(input)),
            Output = output,
            Error = new MemoryStream(),
            Interactive = false,
            MaxInstructions = 10_000_000,
            Log = _ => { }
        };
        options.Volumes["HOST"] = Path.GetTempPath();
        return options;
    }

    internal static string Text(MemoryStream stream) =>
        Encoding.ASCII.GetString(stream.ToArray()).Replace("\r", string.Empty);

    [Fact]
    public async Task When_RunningExecutableFromAdf_Then_ProgramRunsWithSysInAdf()
    {
        var adf = await CreateAdf("Workbench");
        var output = new MemoryStream();
        var options = CreateOptions(output);
        options.Images.Add(new VmImage(adf));

        var result = await new AmigaVm(options).RunFromVolume("DF0:C/Hello");

        Assert.Null(result.Error);
        Assert.Equal(0, result.ReturnCode);
        Assert.Equal("Hello from Workbench\n", Text(output));
    }

    [Fact]
    public async Task When_RunningShellWithAdf_Then_SysAndDf0AreRootOfAdf()
    {
        var adf = await CreateAdf("Workbench");
        var output = new MemoryStream();
        var options = CreateOptions(output,
            "cd\nIf EXISTS DF0:S/file.txt\necho found\nEndIf\nHello\ncd HOST:\ncd SYS:S\ncd\n");
        options.Images.Add(new VmImage(adf));

        var result = await new AmigaVm(options).Shell();

        Assert.Null(result.Error);
        Assert.Equal("Workbench:\nfound\nHello from Workbench\nWorkbench:S\n", Text(output));
    }

    [Fact]
    public async Task When_AdfIsReadOnly_Then_WritingFailsAndAdfIsUnchanged()
    {
        var adf = await CreateAdf("Workbench");
        var before = adf.ToArray();
        var output = new MemoryStream();
        var options = CreateOptions(output, "echo >SYS:out.txt hello\nWhy\n");
        options.Images.Add(new VmImage(adf));

        await new AmigaVm(options).Shell();

        Assert.Contains("write-protected", Text(output));
        Assert.Equal(before, adf.ToArray());
    }

    [Fact]
    public async Task When_AdfIsWritable_Then_FilesWrittenAreStoredInAdf()
    {
        var adf = await CreateAdf("Workbench");
        var output = new MemoryStream();
        var options = CreateOptions(output, "echo >SYS:out.txt hello\necho >>DF0:out.txt world\n");
        options.Images.Add(new VmImage(adf, "workbench.adf", true));

        var result = await new AmigaVm(options).Shell();

        Assert.Null(result.Error);
        await using var volume = await FastFileSystemVolume.MountAdf(adf);
        Assert.Equal("hello\nworld\n", Encoding.ASCII.GetString(await ReadFile(volume, "out.txt")));
    }

    [Fact]
    public async Task When_RunningShellWithDiskImage_Then_SysIsBootablePartitionWithHighestBootPriority()
    {
        var disk = await CreateDisk();
        var output = new MemoryStream();
        var options = CreateOptions(output, "cd\nHello\ncd DH0:\ncd\nDH1:C/Hello\n");
        options.Images.Add(new VmImage(disk));

        var result = await new AmigaVm(options).Shell();

        Assert.Null(result.Error);
        Assert.Equal("Boot:\nHello from Boot\nWork:\nHello from System\n", Text(output));
    }

    [Fact]
    public async Task When_RunningExecutableFromPartition_Then_ProgDirIsDirectoryOfExecutable()
    {
        var disk = await CreateDisk();
        var output = new MemoryStream();
        var options = CreateOptions(output);
        options.Images.Add(new VmImage(disk));

        var result = await new AmigaVm(options).RunFromVolume("DH0:C/Hello");

        Assert.Null(result.Error);
        Assert.Equal("Hello from Work\n", Text(output));
    }
}
