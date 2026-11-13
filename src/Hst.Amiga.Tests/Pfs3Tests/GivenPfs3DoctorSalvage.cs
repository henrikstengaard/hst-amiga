namespace Hst.Amiga.Tests.Pfs3Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Converters;
using DataTypes.UaeFsDbs;
using FileSystems;
using FileSystems.Pfs3;
using FileSystems.Pfs3.Doctor;
using FileSystems.Pfs3.Doctor.Salvage;
using RigidDiskBlocks;
using Xunit;
using Xunit.Abstractions;
using Constants = FileSystems.Pfs3.Constants;
using FileMode = FileSystems.FileMode;

[Trait("Category", "PFS3")]
public class GivenPfs3DoctorSalvage : Pfs3TestBase, IDisposable
{
    private static readonly DateTime File1Date = new(2024, 1, 2, 3, 4, 5);
    private static readonly DateTime Dir1Date = new(2023, 6, 7, 8, 9, 10);
    private static readonly DateTime File2Date = new(2022, 11, 12, 13, 14, 15);

    private readonly ITestOutputHelper output;
    private readonly string path;

    public GivenPfs3DoctorSalvage(ITestOutputHelper output)
    {
        this.output = output;
        path = Path.Combine(Path.GetTempPath(), $"pfs3-salvage-{Guid.NewGuid()}");
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(path))
        {
            System.IO.Directory.Delete(path, true);
        }
    }

    [Fact]
    public async Task When_SalvagingToDirectoryWithUaeFsDb_Then_DirectoriesFilesAndMetadataAreCreated()
    {
        // arrange - create pfs3 formatted disk with directories and files
        var stream = await CreatePfs3FormattedDisk();
        await CreateDirectoriesAndFiles(stream);

        // act - salvage to directory with uaefsdb
        var result = await Salvage(stream, new DirectorySalvageTarget(path, UaeMetadata.UaeFsDb));

        // assert - directories and files are salvaged
        Assert.True(result.RootBlockFound);
        Assert.Equal(2, result.Directories);
        Assert.Equal(5, result.Files);
        Assert.Equal(0, result.PartialFiles);
        Assert.Equal(0, result.Errors);

        AssertFileData(Path.Combine(path, "file1"), 5000);
        AssertFileData(Path.Combine(path, "link1"), 5000);
        AssertFileData(Path.Combine(path, "dir1", "file2"), 100000);
        AssertFileData(Path.Combine(path, "dir1", "file3"), 0);
        Assert.True(System.IO.Directory.Exists(Path.Combine(path, "dir2")));

        // assert - dates are set as last write time
        Assert.Equal(File1Date, System.IO.File.GetLastWriteTime(Path.Combine(path, "file1")));
        Assert.Equal(Dir1Date, System.IO.Directory.GetLastWriteTime(Path.Combine(path, "dir1")));
        Assert.Equal(File2Date, System.IO.File.GetLastWriteTime(Path.Combine(path, "dir1", "file2")));

        // assert - uaefsdb in root has nodes for file1 with protection bits and comment and file with special
        // characters
        var rootNodes = (await UaeFsDbReader.ReadFromFile(Path.Combine(path, "_UAEFSDB.___"))).ToList();
        var file1Node = Assert.Single(rootNodes, x => x.AmigaName == "file1");
        Assert.Equal("file1", file1Node.NormalName);
        Assert.Equal(0x40U | 0x4U | 0x1U, file1Node.Mode); // -s--r-e-
        Assert.Equal("comment on file1", file1Node.Comment);

        var specialNode = Assert.Single(rootNodes, x => x.AmigaName == "file*4");
        Assert.Equal("__uae___file_4", specialNode.NormalName);
        AssertFileData(Path.Combine(path, specialNode.NormalName), 10);

        // assert - uaefsdb in dir1 has node for dir2 with comment
        var dir1Nodes = (await UaeFsDbReader.ReadFromFile(Path.Combine(path, "dir1", "_UAEFSDB.___"))).ToList();
        var file2Node = Assert.Single(dir1Nodes, x => x.AmigaName == "file2");
        Assert.Equal("comment on file2", file2Node.Comment);
    }

    [Fact]
    public async Task When_SalvagingToDirectoryWithUaeMetafiles_Then_DirectoriesFilesAndMetafilesAreCreated()
    {
        // arrange - create pfs3 formatted disk with directories and files
        var stream = await CreatePfs3FormattedDisk();
        await CreateDirectoriesAndFiles(stream);

        // act - salvage to directory with uae metafiles
        var result = await Salvage(stream, new DirectorySalvageTarget(path, UaeMetadata.UaeMetafile));

        // assert - directories and files are salvaged
        Assert.Equal(2, result.Directories);
        Assert.Equal(5, result.Files);
        AssertFileData(Path.Combine(path, "file1"), 5000);
        AssertFileData(Path.Combine(path, "file%2a4"), 10);
        AssertFileData(Path.Combine(path, "dir1", "file2"), 100000);

        // assert - uae metafiles have protection bits, date and comment
        Assert.Equal("-s--r-e- 2024-01-02 03:04:05.00 comment on file1\n",
            await System.IO.File.ReadAllTextAsync(Path.Combine(path, "file1.uaem")));
        Assert.Equal("----rwed 2023-06-07 08:09:10.00 \n",
            await System.IO.File.ReadAllTextAsync(Path.Combine(path, "dir1.uaem")));
        Assert.Equal("----rwed 2022-11-12 13:14:15.00 comment on file2\n",
            await System.IO.File.ReadAllTextAsync(Path.Combine(path, "dir1", "file2.uaem")));
        Assert.True(System.IO.File.Exists(Path.Combine(path, "file%2a4.uaem")));
        Assert.Equal(File1Date, System.IO.File.GetLastWriteTime(Path.Combine(path, "file1")));
    }

    [Fact]
    public async Task When_SalvagingToRigidDiskBlockPartition_Then_DirectoriesFilesAndMetadataAreCreated()
    {
        // arrange - create pfs3 formatted source disk with directories and files
        var stream = await CreatePfs3FormattedDisk();
        await CreateDirectoriesAndFiles(stream);

        // arrange - create pfs3 formatted target disk
        var targetStream = await CreatePfs3FormattedDisk();

        // act - salvage to partition dh0 in target disk
        SalvageResult result;
        using (var target = await FileSystemVolumeSalvageTarget.MountPartition(targetStream, "DH0"))
        {
            result = await Salvage(stream, target);
        }

        // assert - directories and files are salvaged
        Assert.Equal(2, result.Directories);
        Assert.Equal(5, result.Files);
        Assert.Equal(0, result.Errors);

        // assert - entries in target disk has data, protection bits, comments and dates
        await using var volume = await MountVolume(targetStream);
        var rootEntries = (await volume.ListEntries()).ToList();
        var file1 = Assert.Single(rootEntries, x => x.Name == "file1");
        Assert.Equal(5000, file1.Size);
        Assert.Equal(ProtectionBits.Script | ProtectionBits.Read | ProtectionBits.Executable,
            file1.ProtectionBits);
        Assert.Equal("comment on file1", file1.Comment);
        Assert.Equal(File1Date, file1.Date);
        Assert.Contains(rootEntries, x => x.Name == "file*4" && x.Size == 10);
        Assert.Contains(rootEntries, x => x.Name == "link1" && x.Type == EntryType.File && x.Size == 5000);
        var dir1 = Assert.Single(rootEntries, x => x.Name == "dir1");
        Assert.Equal(EntryType.Dir, dir1.Type);
        Assert.Equal(Dir1Date, dir1.Date);

        await volume.ChangeDirectory("dir1");
        var dir1Entries = (await volume.ListEntries()).ToList();
        var file2 = Assert.Single(dir1Entries, x => x.Name == "file2");
        Assert.Equal("comment on file2", file2.Comment);
        Assert.Equal(File2Date, file2.Date);
        Assert.Equal(CreateData(100000), await ReadFile(volume, "file2"));
    }

    [Fact]
    public async Task When_SalvagingDiskWithDestroyedRootBlock_Then_DirectoriesAndFilesAreSalvaged()
    {
        // arrange - create pfs3 formatted disk with directories and files
        var stream = await CreatePfs3FormattedDisk();
        await CreateDirectoriesAndFiles(stream);

        // arrange - destroy root block
        var partitionOffset = await GetPartitionOffset(stream);
        stream.Position = partitionOffset + Constants.ROOTBLOCK * 512;
        await stream.WriteAsync(new byte[512], 0, 512);

        // act - salvage to directory
        var result = await Salvage(stream, new DirectorySalvageTarget(path, UaeMetadata.UaeFsDb));

        // assert - directories and files are salvaged without root block
        Assert.False(result.RootBlockFound);
        Assert.Equal(2, result.Directories);
        Assert.Equal(5, result.Files);
        Assert.Equal(0, result.PartialFiles);
        AssertFileData(Path.Combine(path, "file1"), 5000);
        AssertFileData(Path.Combine(path, "dir1", "file2"), 100000);
    }

    [Fact]
    public async Task When_SalvagingDiskWithDestroyedRootDirectoryBlocks_Then_OrphanedDirectoryIsSalvagedToLost()
    {
        // arrange - create pfs3 formatted disk with directories and files
        var stream = await CreatePfs3FormattedDisk();
        await CreateDirectoriesAndFiles(stream);

        // arrange - destroy root directory blocks, including old copies
        foreach (var rootDirBlockOffset in await FindRootDirBlockOffsets(stream))
        {
            stream.Position = rootDirBlockOffset;
            await stream.WriteAsync(new byte[512], 0, 512);
        }

        // act - salvage to directory
        var result = await Salvage(stream, new DirectorySalvageTarget(path, UaeMetadata.UaeFsDb));

        // assert - dir1 is salvaged as orphaned directory in lost directory
        Assert.Equal(1, result.OrphanedDirectories);
        var lostPath = Path.Combine(path, SalvageOptions.DefaultLostDirectoryName);
        var orphanPath = Assert.Single(System.IO.Directory.GetDirectories(lostPath));
        Assert.StartsWith("Dir_", Path.GetFileName(orphanPath));
        AssertFileData(Path.Combine(orphanPath, "file2"), 100000);
        AssertFileData(Path.Combine(orphanPath, "file3"), 0);
        Assert.False(System.IO.File.Exists(Path.Combine(path, "file1")));
    }

    private async Task<SalvageResult> Salvage(Stream stream, ISalvageTarget target)
    {
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        var partitionBlock = rigidDiskBlock.PartitionBlocks.First();

        var line = new StringBuilder();
        var volume = Pfs3Doctor.OpenVolume(stream, partitionBlock);
        volume.ShowMsg = message =>
        {
            line.Append(message);
            var text = line.ToString();
            int index;
            while ((index = text.IndexOf('\n')) >= 0)
            {
                output.WriteLine(text.Substring(0, index));
                text = text.Substring(index + 1);
            }

            line.Clear();
            line.Append(text);
        };

        return await Pfs3Doctor.Salvage(volume, target);
    }

    private async Task CreateDirectoriesAndFiles(Stream stream)
    {
        await using var pfs3Volume = await MountVolume(stream);
        await pfs3Volume.CreateFile("file1", true, true);
        await WriteFile(pfs3Volume, "file1", 5000);
        await pfs3Volume.SetProtectionBits("file1", ProtectionBits.Script | ProtectionBits.Read |
                                                   ProtectionBits.Executable);
        await pfs3Volume.SetComment("file1", "comment on file1");
        await pfs3Volume.SetDate("file1", File1Date);
        await pfs3Volume.CreateFile("file*4", true, true);
        await WriteFile(pfs3Volume, "file*4", 10);
        await pfs3Volume.CreateLink("link1", "file1");
        await pfs3Volume.CreateDirectory("dir1");
        await pfs3Volume.CreateDirectory("dir2");
        await pfs3Volume.ChangeDirectory("dir1");
        await pfs3Volume.CreateFile("file2", true, true);
        await WriteFile(pfs3Volume, "file2", 100000);
        await pfs3Volume.SetComment("file2", "comment on file2");
        await pfs3Volume.SetDate("file2", File2Date);
        await pfs3Volume.CreateFile("file3", true, true);
        await pfs3Volume.ChangeDirectory("/");
        await pfs3Volume.SetDate("dir1", Dir1Date);
    }

    private static byte[] CreateData(int size)
    {
        var data = new byte[size];
        for (var i = 0; i < size; i++)
        {
            data[i] = (byte)(i & 0xff);
        }

        return data;
    }

    private static async Task WriteFile(Pfs3Volume pfs3Volume, string name, int size)
    {
        await using var fileStream = await pfs3Volume.OpenFile(name, FileMode.Write);
        var data = CreateData(size);
        await fileStream.WriteAsync(data, 0, data.Length);
    }

    private static async Task<byte[]> ReadFile(Pfs3Volume pfs3Volume, string name)
    {
        await using var fileStream = await pfs3Volume.OpenFile(name, FileMode.Read);
        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }

    private static void AssertFileData(string filePath, int size)
    {
        Assert.True(System.IO.File.Exists(filePath), $"File '{filePath}' doesn't exist");
        Assert.Equal(CreateData(size), System.IO.File.ReadAllBytes(filePath));
    }

    private static async Task<long> GetPartitionOffset(Stream stream)
    {
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        var partitionBlock = rigidDiskBlock.PartitionBlocks.First();
        return (long)partitionBlock.LowCyl * partitionBlock.Surfaces * partitionBlock.BlocksPerTrack * 512;
    }

    private static async Task<IList<long>> FindRootDirBlockOffsets(Stream stream)
    {
        var partitionOffset = await GetPartitionOffset(stream);
        var data = new byte[512];
        var offsets = new List<long>();
        for (var blockNo = 3; blockNo < 4096; blockNo++)
        {
            var offset = partitionOffset + blockNo * 512L;
            stream.Position = offset;
            _ = await stream.ReadAsync(data, 0, data.Length);
            if (BigEndianConverter.ConvertBytesToUInt16(data) == Constants.DBLKID &&
                BigEndianConverter.ConvertBytesToUInt32(data, 12) == Constants.ANODE_ROOTDIR)
            {
                offsets.Add(offset);
            }
        }

        return offsets;
    }
}
