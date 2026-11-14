namespace Hst.Amiga.Tests.SmartFileSystemTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileSystems.Salvage;
using FileSystems.SmartFileSystem;
using FileSystems.SmartFileSystem.Blocks;
using FileSystems.SmartFileSystem.Doctor;
using Xunit;
using Constants = FileSystems.SmartFileSystem.Constants;

/// <summary>
/// Salvage tests for smart file system doctor. Partial class shares disk and block helpers with check and repair
/// tests.
/// </summary>
public partial class GivenSmartFileSystemDoctor
{
    [Fact]
    public async Task When_SalvagingDisk_Then_DirectoriesFilesAndMetadataAreSalvaged()
    {
        // arrange - create disk with directories and files
        var stream = await CreateDiskWithDirectoriesAndFiles();
        var before = ToArray(stream);

        // act - salvage disk
        var target = new MemorySalvageTarget();
        var result = await Salvage(stream, target);

        // assert - directories and files are salvaged and soft link is skipped
        Assert.True(result.RootBlockFound);
        Assert.Equal(3, result.Directories);
        Assert.Equal(4, result.Files);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.PartialFiles);
        Assert.Equal(0, result.Errors);
        Assert.Equal(CreateTestData(LargeFileSize), target.Files["file1.txt"]);
        Assert.Equal(CreateTestData(100), target.Files["small.txt"]);
        Assert.Empty(target.Files["empty.txt"]);
        Assert.Equal(CreateTestData(2000), target.Files["dir1/file2.txt"]);
        Assert.Contains("dir1", target.Directories);
        Assert.Contains("dir2", target.Directories);

        // assert - metadata is salvaged
        var file1 = target.Entries["file1.txt"];
        Assert.Equal("comment on file1", file1.Comment);
        Assert.Equal(File1Date, file1.Date);
        Assert.Equal(LargeFileSize, file1.Size);

        // assert - disk is not changed
        Assert.Equal(before, ToArray(stream));
    }

    [Fact]
    public async Task When_SalvagingDiskWithDestroyedRootBlocks_Then_DirectoriesAndFilesAreSalvaged()
    {
        // arrange - create disk and destroy both root blocks
        var stream = await CreateDiskWithDirectoriesAndFiles();
        var volume = await OpenVolume(stream);
        await WriteRawBlock(volume, 0, new byte[volume.BlockSize]);
        await WriteRawBlock(volume, volume.Blocks - 1, new byte[volume.BlockSize]);

        // act - salvage disk
        var target = new MemorySalvageTarget();
        var result = await Salvage(stream, target);

        // assert - directories and files are salvaged using objects with root directory as parent
        Assert.False(result.RootBlockFound);
        Assert.Equal(4, result.Files);
        Assert.Equal(CreateTestData(LargeFileSize), target.Files["file1.txt"]);
        Assert.Equal(CreateTestData(2000), target.Files["dir1/file2.txt"]);
    }

    [Fact]
    public async Task When_SalvagingDiskWithDestroyedRootObjectContainer_Then_DirectoriesAndFilesAreSalvaged()
    {
        // arrange - create disk and destroy root object container
        var stream = await CreateDiskWithDirectoriesAndFiles();
        var volume = await OpenVolume(stream);
        var rootBlock = await ReadRootBlock(stream);
        await WriteRawBlock(volume, rootBlock.RootObjectContainer, new byte[volume.BlockSize]);

        // act - salvage disk
        var target = new MemorySalvageTarget();
        var result = await Salvage(stream, target);

        // assert - directories and files are salvaged using objects with root directory as parent
        Assert.False(result.RootBlockFound);
        Assert.Equal(4, result.Files);
        Assert.Equal(CreateTestData(100), target.Files["small.txt"]);
        Assert.Equal(CreateTestData(2000), target.Files["dir1/file2.txt"]);
    }

    [Fact]
    public async Task When_SalvagingDiskWithDestroyedObjectContainer_Then_OrphanedObjectsAreSalvagedToLost()
    {
        // arrange - create disk and destroy object container with objects in root directory
        var stream = await CreateDiskWithDirectoriesAndFiles();
        var volume = await OpenVolume(stream);
        var dir1 = await FindObject(stream, "dir1");
        await WriteRawBlock(volume, dir1.Container.OwnBlock, new byte[volume.BlockSize]);

        // act - salvage disk
        var target = new MemorySalvageTarget();
        var result = await Salvage(stream, target);

        // assert - file2 is salvaged as orphan in lost directory
        Assert.Equal(1, result.OrphanedDirectories);
        Assert.Equal(CreateTestData(2000), target.Files[$"Lost+Found/Dir_{dir1.Object.ObjectNode}/file2.txt"]);
        Assert.DoesNotContain("dir1", target.Directories);
    }

    [Fact]
    public async Task When_SalvagingDiskWithBrokenExtentBTree_Then_FilesAreSalvagedUsingExtentsFoundByScan()
    {
        // arrange - create disk with many files and make a leaf B-tree node container unreachable
        var stream = await CreateDiskWithManyFiles();
        var volume = await OpenVolume(stream);
        var rootBlock = await ReadRootBlock(stream);
        var data = await ReadRawBlock(volume, rootBlock.ExtentBNodeRoot);
        SetUInt32(data, Constants.BNodeContainerHeaderSize + Constants.BNodeSize + 4, volume.Blocks + 100);
        await WriteRawBlock(volume, rootBlock.ExtentBNodeRoot, data, true);

        // act - salvage disk
        var target = new MemorySalvageTarget();
        var result = await Salvage(stream, target);

        // assert - all files are salvaged
        Assert.Equal(150, result.Files);
        Assert.Equal(0, result.PartialFiles);
        for (var i = 0; i < 150; i++)
        {
            Assert.Equal(CreateTestData(1000, i), target.Files[$"file{i}.txt"]);
        }
    }

    [Fact]
    public async Task When_SalvagingDiskWithPendingTransaction_Then_TransactionIsReplayedInMemory()
    {
        // arrange - create disk and write a pending transaction, which changes comment of file1.txt
        var stream = await CreateDiskWithDirectoriesAndFiles();
        var volume = await OpenVolume(stream);
        var rootBlock = await ReadRootBlock(stream);
        var file1 = await FindObject(stream, "file1.txt");
        file1.Container.Objects.First(x => x.Name == "file1.txt").Comment = "replayed";
        await WritePendingTransaction(volume, rootBlock, file1.Container.OwnBlock,
            ObjectContainerBuilder.Build(file1.Container, volume.BlockSize));
        var before = ToArray(stream);

        // act - salvage disk
        var target = new MemorySalvageTarget();
        await Salvage(stream, target);

        // assert - salvaged file has comment from transaction and disk is not changed
        Assert.Equal("replayed", target.Entries["file1.txt"].Comment);
        Assert.Equal(before, ToArray(stream));
    }

    [Fact]
    public async Task When_SalvagingToSmartFileSystemPartition_Then_DirectoriesAndFilesAreCreated()
    {
        // arrange - create source disk with directories and files and destroy root object container
        var stream = await CreateDiskWithDirectoriesAndFiles();
        var volume = await OpenVolume(stream);
        var rootBlock = await ReadRootBlock(stream);
        await WriteRawBlock(volume, rootBlock.RootObjectContainer, new byte[volume.BlockSize]);

        // arrange - create target disk
        var targetStream = await CreateSmartFileSystemFormattedDisk(DiskSize10Mb);

        // act - salvage to partition dh0 in target disk
        SalvageResult result;
        using (var target = await FileSystemVolumeSalvageTarget.MountPartition(targetStream, "DH0"))
        {
            result = await Salvage(stream, target);
        }

        // assert - directories and files are salvaged to target disk
        Assert.Equal(4, result.Files);
        Assert.Equal(0, result.Errors);
        Assert.Equal(CreateTestData(LargeFileSize), await ReadFile(targetStream, "file1.txt"));
        Assert.Equal(CreateTestData(2000), await ReadFile(targetStream, "dir1/file2.txt"));
        Assert.Empty((await RunDoctor(targetStream, Mode.Check)).Issues);
        await AssertVolumeIsValid(targetStream);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task When_SalvagingDiskWithRandomCorruptBlocks_Then_FilesAreSalvaged(int seed)
    {
        // arrange - create disk with many files and corrupt random bytes in blocks at start of partition
        var stream = await CreateDiskWithManyFiles();
        var volume = await OpenVolume(stream);
        var random = new Random(seed);
        for (var i = 0; i < 20; i++)
        {
            var block = (uint)random.Next(0, 900);
            var data = await ReadRawBlock(volume, block);
            for (var j = 0; j < 4; j++)
            {
                data[random.Next(0, data.Length)] = (byte)random.Next(256);
            }

            await WriteRawBlock(volume, block, data, i % 2 == 0);
        }

        var before = ToArray(stream);

        // act - salvage disk
        var target = new MemorySalvageTarget();
        var result = await Salvage(stream, target);

        // assert - files are salvaged and disk is not changed
        Assert.True(result.Files > 0);
        Assert.Equal(before, ToArray(stream));
    }

    private async Task<SalvageResult> Salvage(Stream stream, ISalvageTarget target)
    {
        var volume = await OpenVolume(stream);
        volume.ShowMsg = message => output.WriteLine(message.TrimEnd());
        output.WriteLine("--- Salvage ---");
        return await SmartFileSystemDoctor.Salvage(volume, target);
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
