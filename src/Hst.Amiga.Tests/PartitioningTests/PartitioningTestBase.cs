namespace Hst.Amiga.Tests.PartitioningTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Extensions;
using FileSystems;
using FileSystems.FastFileSystem;
using FileSystems.Pfs3;
using FileSystems.SmartFileSystem;
using RigidDiskBlocks;
using RigidDiskBlocks.Partitioning;
using Xunit;
using FileMode = FileSystems.FileMode;

public enum TestFileSystem
{
    Dos0,
    Dos3,
    Dos5,
    Dos7,
    Pfs3,
    Sfs
}

/// <summary>
/// Base for partition mover and resizer tests. Disk has rigid disk block with 3 partitions and free cylinders
/// between them:
/// - cylinders 2-21: DH0
/// - cylinders 22-29: free
/// - cylinders 30-49: DH1, partition used in tests
/// - cylinders 50-59: free
/// - cylinders 60-79: DH2
/// - cylinders 80-: free
/// </summary>
public abstract class PartitioningTestBase
{
    protected const long DiskSize = 60L * 1024 * 1024;
    protected const int TestPartitionNumber = 2;

    protected static byte[] GetDosType(TestFileSystem fileSystem) => fileSystem switch
    {
        TestFileSystem.Dos0 => new byte[] { 0x44, 0x4f, 0x53, 0x0 },
        TestFileSystem.Dos3 => new byte[] { 0x44, 0x4f, 0x53, 0x3 },
        TestFileSystem.Dos5 => new byte[] { 0x44, 0x4f, 0x53, 0x5 },
        TestFileSystem.Dos7 => new byte[] { 0x44, 0x4f, 0x53, 0x7 },
        TestFileSystem.Pfs3 => new byte[] { 0x50, 0x44, 0x53, 0x3 },
        TestFileSystem.Sfs => new byte[] { 0x53, 0x46, 0x53, 0x0 },
        _ => throw new ArgumentOutOfRangeException(nameof(fileSystem))
    };

    /// <summary>
    /// Create disk with 3 partitions formatted with file system. Stream can be larger than rigid disk block.
    /// </summary>
    protected static async Task<BlockMemoryStream> CreateDisk(TestFileSystem fileSystem, long streamSize = DiskSize)
    {
        var stream = new BlockMemoryStream();
        var rigidDiskBlock = RigidDiskBlock.Create(DiskSize);
        stream.SetLength(Math.Max(streamSize, rigidDiskBlock.DiskSize));

        var dosType = GetDosType(fileSystem);
        var versionBytes = Encoding.ASCII.GetBytes($"$VER: {fileSystem}FileSystem 1.0 (01/01/24) ");
        var fileSystemBytes = new byte[(versionBytes.Length + 3) / 4 * 4];
        Array.Copy(versionBytes, fileSystemBytes, versionBytes.Length);
        rigidDiskBlock.AddFileSystem(dosType, fileSystemBytes);

        var layout = new[] { ("DH0", 2U, 21U), ("DH1", 30U, 49U), ("DH2", 60U, 79U) };
        var partitionBlocks = new List<PartitionBlock>();
        foreach (var (name, lowCyl, highCyl) in layout)
        {
            var partitionBlock = PartitionBlock.Create(rigidDiskBlock, dosType, name);
            PartitionGeometry.SetCylinders(partitionBlock, lowCyl, highCyl);
            partitionBlocks.Add(partitionBlock);
            rigidDiskBlock.PartitionBlocks = partitionBlocks.ToList();
        }

        await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);

        foreach (var partitionBlock in partitionBlocks)
        {
            await Format(stream, partitionBlock, fileSystem);
        }

        return stream;
    }

    private static async Task Format(Stream stream, PartitionBlock partitionBlock, TestFileSystem fileSystem)
    {
        switch (fileSystem)
        {
            case TestFileSystem.Pfs3:
                await Pfs3Formatter.FormatPartition(stream, partitionBlock, partitionBlock.DriveName);
                break;
            case TestFileSystem.Sfs:
                await SmartFileSystemFormatter.FormatPartition(stream, partitionBlock, partitionBlock.DriveName);
                break;
            default:
                await FastFileSystemFormatter.FormatPartition(stream, partitionBlock, partitionBlock.DriveName);
                break;
        }
    }

    protected static async Task<PartitionBlock> ReadPartitionBlock(Stream stream, int partitionNumber)
    {
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        return rigidDiskBlock.PartitionBlocks.ElementAt(partitionNumber - 1);
    }

    protected static async Task<IFileSystemVolume> Mount(Stream stream, int partitionNumber) =>
        await FileSystemVolumeFactory.MountPartition(stream, await ReadPartitionBlock(stream, partitionNumber));

    protected static byte[] CreateTestData(int length, int seed)
    {
        var data = new byte[length];
        var random = new Random(seed);
        random.NextBytes(data);
        return data;
    }

    /// <summary>
    /// Write directories and files to partition. Size is approximate total size of data written.
    /// </summary>
    protected static async Task WriteContent(Stream stream, int partitionNumber, TestFileSystem fileSystem,
        int largeFiles = 2, int largeFileSize = 300 * 1024, int smallFiles = 40)
    {
        await using var volume = await Mount(stream, partitionNumber);

        await volume.CreateDirectory("dir1");
        await volume.ChangeDirectory("dir1");
        await volume.CreateDirectory("dir2");

        for (var i = 0; i < smallFiles; i++)
        {
            await WriteFile(volume, $"small{i}.txt", CreateTestData(100 + i * 37, i));
        }

        await volume.ChangeDirectory("/");

        for (var i = 0; i < largeFiles; i++)
        {
            await WriteFile(volume, $"large{i}.bin", CreateTestData(largeFileSize + i * 1000, 1000 + i));
        }

        await WriteFile(volume, "file.txt", Encoding.ASCII.GetBytes("Hello Amiga"));
        await volume.SetComment("file.txt",
            fileSystem == TestFileSystem.Dos7 ? new string('c', 79) : "comment");

        if (fileSystem is TestFileSystem.Dos0 or TestFileSystem.Dos3 or TestFileSystem.Dos5
            or TestFileSystem.Dos7)
        {
            await volume.CreateLink("hardlink", "file.txt");
        }

        await volume.Flush();
    }

    /// <summary>
    /// Write content to partition, which places data and administration blocks near the end of partition: large
    /// files fill most of the partition, then many small files are written after them and large files are
    /// deleted leaving free blocks at the start of partition.
    /// </summary>
    protected static async Task WriteContentAtEnd(Stream stream, int partitionNumber, int largeFiles = 16,
        int largeFileSize = 512 * 1024, int smallFiles = 2000)
    {
        await using var volume = await Mount(stream, partitionNumber);

        for (var i = 0; i < largeFiles; i++)
        {
            await WriteFile(volume, $"fill{i}.bin", CreateTestData(largeFileSize, 2000 + i));
        }

        await volume.CreateDirectory("many");
        await volume.ChangeDirectory("many");
        for (var i = 0; i < smallFiles; i++)
        {
            await WriteFile(volume, $"file{i}.txt", CreateTestData(100 + i % 50, 3000 + i));
        }

        await volume.ChangeDirectory("/");
        await WriteFile(volume, "last.bin", CreateTestData(100 * 1024, 4000));

        for (var i = 0; i < largeFiles; i++)
        {
            await volume.Delete($"fill{i}.bin", true);
        }

        await volume.Flush();
    }

    protected static async Task WriteFile(IFileSystemVolume volume, string name, byte[] data)
    {
        await using var entryStream = await volume.OpenFile(name, FileMode.Write, true, true);
        await entryStream.WriteAsync(data, 0, data.Length);
    }

    /// <summary>
    /// Snapshot of entries and file contents in partition, path to type, size and hash of file content.
    /// </summary>
    protected static async Task<IDictionary<string, string>> Snapshot(Stream stream, int partitionNumber)
    {
        var snapshot = new SortedDictionary<string, string>();
        await using var volume = await Mount(stream, partitionNumber);
        await Snapshot(volume, string.Empty, snapshot);
        return snapshot;
    }

    private static async Task Snapshot(IFileSystemVolume volume, string path,
        IDictionary<string, string> snapshot)
    {
        var entries = (await volume.ListEntries()).OrderBy(x => x.Name).ToList();
        foreach (var entry in entries)
        {
            var entryPath = path + "/" + entry.Name;
            switch (entry.Type)
            {
                case EntryType.Dir:
                    snapshot[entryPath] = "dir";
                    await volume.ChangeDirectory(entry.Name);
                    await Snapshot(volume, entryPath, snapshot);
                    await volume.ChangeDirectory("/" + path.TrimStart('/'));
                    break;
                case EntryType.File:
                case EntryType.FileLink:
                    await using (var entryStream = await volume.OpenFile(entry.Name, FileMode.Read, false, true))
                    {
                        var memoryStream = new MemoryStream();
                        await entryStream.CopyToAsync(memoryStream);
                        snapshot[entryPath] =
                            $"{entry.Type}:{memoryStream.Length}:{Convert.ToHexString(SHA256.HashData(memoryStream.ToArray()))}:{entry.Comment}";
                    }

                    break;
                default:
                    snapshot[entryPath] = $"{entry.Type}:{entry.LinkPath}";
                    break;
            }
        }
    }

    protected static async Task<string> HashPartition(Stream stream, int partitionNumber)
    {
        var partitionBlock = await ReadPartitionBlock(stream, partitionNumber);
        var data = new byte[PartitionGeometry.GetSize(partitionBlock)];
        stream.Seek(PartitionGeometry.GetStartOffset(partitionBlock), SeekOrigin.Begin);
        var offset = 0;
        while (offset < data.Length)
        {
            var read = await stream.ReadAsync(data, offset, data.Length - offset);
            if (read == 0)
            {
                break;
            }

            offset += read;
        }

        return Convert.ToHexString(SHA256.HashData(data));
    }

    protected static void AssertSuccess(PartitionOperationResult result)
    {
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));
        Assert.Empty(result.Errors);
    }

    protected static void AssertSnapshotsEqual(IDictionary<string, string> expected,
        IDictionary<string, string> actual)
    {
        Assert.Equal(expected.Keys.ToList(), actual.Keys.ToList());
        foreach (var key in expected.Keys)
        {
            Assert.Equal(expected[key], actual[key]);
        }
    }
}
