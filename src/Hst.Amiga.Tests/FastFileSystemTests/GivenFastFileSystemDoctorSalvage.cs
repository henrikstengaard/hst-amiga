namespace Hst.Amiga.Tests.FastFileSystemTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Core.Converters;
using FileSystems;
using FileSystems.FastFileSystem;
using FileSystems.FastFileSystem.Doctor;
using FileSystems.Salvage;
using Xunit;
using Directory = FileSystems.FastFileSystem.Directory;

/// <summary>
/// Salvage tests for fast file system doctor. Partial class shares disk and block helpers with check and repair
/// tests.
/// </summary>
public partial class GivenFastFileSystemDoctor
{
    [Theory]
    [MemberData(nameof(DosTypes))]
    public async Task When_SalvagingDisk_Then_DirectoriesFilesAndMetadataAreSalvaged(byte dosType)
    {
        // arrange - create disk with directories and files
        var stream = await CreateDiskWithDirectoriesAndFiles(dosType);
        var before = ToArray(stream);

        // act - salvage disk
        var target = new MemorySalvageTarget();
        var result = await Salvage(stream, target);

        // assert - directories and files are salvaged
        Assert.True(result.RootBlockFound);
        Assert.Equal(1, result.Directories);
        Assert.Equal(4, result.Files);
        Assert.Equal(0, result.PartialFiles);
        Assert.Equal(0, result.Errors);
        Assert.Equal(CreateData(LargeFileSize), target.Files["file1.txt"]);
        Assert.Equal(CreateData(100), target.Files["small.txt"]);
        Assert.Equal(CreateData(100), target.Files["link1"]);
        Assert.Equal(CreateData(2000), target.Files["dir1/file2.txt"]);
        Assert.Contains("dir1", target.Directories);

        // assert - metadata is salvaged
        var file1 = target.Entries["file1.txt"];
        Assert.Equal("comment on file1", file1.Comment);
        Assert.Equal(File1Date, file1.Date);
        Assert.Equal(LargeFileSize, file1.Size);

        // assert - disk is not changed
        Assert.Equal(before, ToArray(stream));
    }

    [Theory]
    [MemberData(nameof(DosTypes))]
    public async Task When_SalvagingDiskWithDestroyedRootBlock_Then_DirectoriesAndFilesAreSalvaged(byte dosType)
    {
        // arrange - create disk and destroy root block
        var stream = await CreateDiskWithDirectoriesAndFiles(dosType);
        var volume = await OpenVolume(stream);
        await WriteBlock(volume, volume.RootBlock, new byte[volume.BlockSize], -1);

        // act - salvage disk
        var target = new MemorySalvageTarget();
        var result = await Salvage(stream, target);

        // assert - directories and files are salvaged using entries with root block as parent
        Assert.False(result.RootBlockFound);
        Assert.Equal(1, result.Directories);
        Assert.Equal(4, result.Files);
        Assert.Equal(CreateData(LargeFileSize), target.Files["file1.txt"]);
        Assert.Equal(CreateData(2000), target.Files["dir1/file2.txt"]);
    }

    [Theory]
    [MemberData(nameof(DosTypes))]
    public async Task When_SalvagingDiskWithBrokenHashChain_Then_EntriesWithDirectoryAsParentAreSalvaged(byte dosType)
    {
        // arrange - create disk and set hash chain of file1 outside volume
        var stream = await CreateDiskWithDirectoriesAndFiles(dosType);
        var volume = await OpenVolume(stream);
        var slot = Directory.GetHashValue(72, "file1.txt", dosType >= 2);
        var root = await ReadBlock(volume, volume.RootBlock);
        SetUInt32(root, 0x18 + slot * 4, volume.Blocks + 1000);
        await WriteBlock(volume, volume.RootBlock, root);

        // act - salvage disk
        var target = new MemorySalvageTarget();
        var result = await Salvage(stream, target);

        // assert - file1 is salvaged in root directory
        Assert.Equal(4, result.Files);
        Assert.Equal(CreateData(LargeFileSize), target.Files["file1.txt"]);
    }

    [Theory]
    [MemberData(nameof(DosTypes))]
    public async Task When_SalvagingDiskWithDestroyedDirectoryBlock_Then_OrphanedEntriesAreSalvagedToLost(byte dosType)
    {
        // arrange - create disk and destroy dir1 directory block
        var stream = await CreateDiskWithDirectoriesAndFiles(dosType);
        var volume = await OpenVolume(stream);
        var dir1 = await FindEntryBlock(volume, volume.RootBlock, "dir1", dosType);
        await WriteBlock(volume, dir1, new byte[volume.BlockSize], -1);

        // act - salvage disk
        var target = new MemorySalvageTarget();
        var result = await Salvage(stream, target);

        // assert - file2 is salvaged as orphan in lost directory
        Assert.Equal(1, result.OrphanedDirectories);
        Assert.Equal(CreateData(2000), target.Files[$"Lost+Found/Dir_{dir1}/file2.txt"]);
        Assert.DoesNotContain("dir1", target.Directories);
    }

    [Theory]
    [MemberData(nameof(DosTypes))]
    public async Task When_SalvagingFileWithInvalidDataBlockPointer_Then_OfsFileIsFullySalvagedAndFfsFileIsPartial(
        byte dosType)
    {
        // arrange - create disk and set 11th data block pointer of file1 outside volume
        var stream = await CreateDiskWithDirectoriesAndFiles(dosType);
        var volume = await OpenVolume(stream);
        var block = await FindEntryBlock(volume, volume.RootBlock, "file1.txt", dosType);
        var data = await ReadBlock(volume, block);
        SetUInt32(data, 0x18 + (72 - 1 - 10) * 4, volume.Blocks + 10);
        await WriteBlock(volume, block, data);

        // act - salvage disk
        var target = new MemorySalvageTarget();
        var result = await Salvage(stream, target);

        // assert - ofs file is fully salvaged using data blocks found by scan, ffs file is salvaged up to
        // invalid data block pointer
        if (dosType == 0)
        {
            Assert.Equal(0, result.PartialFiles);
            Assert.Equal(CreateData(LargeFileSize), target.Files["file1.txt"]);
        }
        else
        {
            Assert.Equal(1, result.PartialFiles);
            Assert.Equal(CreateData(LargeFileSize).Take(10 * 512).ToArray(), target.Files["file1.txt"]);
        }
    }

    [Fact]
    public async Task When_SalvagingToFastFileSystemPartition_Then_DirectoriesAndFilesAreCreated()
    {
        // arrange - create source disk with directories and files and destroy root block
        var stream = await CreateDiskWithDirectoriesAndFiles(3);
        var volume = await OpenVolume(stream);
        await WriteBlock(volume, volume.RootBlock, new byte[volume.BlockSize], -1);

        // arrange - create target disk
        var targetStream = await CreateFastFileSystemFormattedDisk(DiskSize10Mb);

        // act - salvage to partition dh0 in target disk
        SalvageResult result;
        using (var target = await FileSystemVolumeSalvageTarget.MountPartition(targetStream, "DH0"))
        {
            result = await Salvage(stream, target);
        }

        // assert - directories and files are salvaged to target disk
        Assert.Equal(4, result.Files);
        Assert.Equal(0, result.Errors);
        Assert.Equal(CreateData(LargeFileSize), await ReadFile(targetStream, "file1.txt"));
        Assert.Equal(CreateData(100), await ReadFile(targetStream, "link1"));
        Assert.Empty((await RunDoctor(targetStream, Mode.Check)).Issues);
    }

    private async Task<SalvageResult> Salvage(Stream stream, ISalvageTarget target)
    {
        var volume = await OpenVolume(stream);
        volume.ShowMsg = message => output.WriteLine(message.TrimEnd());
        output.WriteLine("--- Salvage ---");
        return await FastFileSystemDoctor.Salvage(volume, target);
    }

    private static byte[] ToArray(Stream stream)
    {
        var memoryStream = new MemoryStream();
        stream.Seek(0, SeekOrigin.Begin);
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    /// <summary>
    /// Salvage target keeping directories, files and entries in memory by path.
    /// </summary>
    private class MemorySalvageTarget : ISalvageTarget
    {
        public HashSet<string> Directories { get; } = new();
        public Dictionary<string, byte[]> Files { get; } = new();
        public Dictionary<string, SalvageEntry> Entries { get; } = new();

        private static string GetPath(IReadOnlyList<string> parentPath, string name) =>
            string.Join("/", parentPath.Concat(new[] { name }));

        public Task CreateDirectory(IReadOnlyList<string> parentPath, SalvageEntry entry)
        {
            var path = GetPath(parentPath, entry.Name);
            Directories.Add(path);
            Entries[path] = entry;
            return Task.CompletedTask;
        }

        public Task SetDirectoryMetadata(IReadOnlyList<string> parentPath, SalvageEntry entry) => Task.CompletedTask;

        public async Task WriteFile(IReadOnlyList<string> parentPath, SalvageEntry entry, Stream data)
        {
            var path = GetPath(parentPath, entry.Name);
            var memoryStream = new MemoryStream();
            await data.CopyToAsync(memoryStream);
            Files[path] = memoryStream.ToArray();
            Entries[path] = entry;
        }

        public Task Flush() => Task.CompletedTask;
    }
}
