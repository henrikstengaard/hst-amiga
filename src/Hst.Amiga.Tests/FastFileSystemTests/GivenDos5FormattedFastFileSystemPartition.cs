namespace Hst.Amiga.Tests.FastFileSystemTests;

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Core.Converters;
using FileSystems;
using FileSystems.FastFileSystem;
using RigidDiskBlocks;
using Xunit;
using Constants = FileSystems.FastFileSystem.Constants;
using FileMode = FileSystems.FileMode;

/// <summary>
/// Tests for fast file system with dir cache (DOS\5).
/// </summary>
public class GivenDos5FormattedFastFileSystemPartition : FastFileSystemTestBase
{
    private static readonly byte[] Dos5DosType = { 0x44, 0x4f, 0x53, 0x5 };

    [Fact]
    public async Task When_Formatting_Then_RootBlockHasEmptyDirCacheBlock()
    {
        // arrange - create dos5 formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(dosType: Dos5DosType);

        // act - read root block and dir cache block
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        var partitionBlock = rigidDiskBlock.PartitionBlocks.First();
        var partitionOffset = (long)partitionBlock.LowCyl * partitionBlock.Surfaces * partitionBlock.BlocksPerTrack *
                              partitionBlock.BlockSize;
        var rootBlockOffset = OffsetHelper.CalculateRootBlockOffset(partitionBlock.LowCyl, partitionBlock.HighCyl,
            partitionBlock.Reserved, partitionBlock.Surfaces, partitionBlock.BlocksPerTrack,
            partitionBlock.FileSystemBlockSize);
        var rootBlock = await ReadBlock(stream, partitionOffset + rootBlockOffset * 512L);
        var dirCacheBlockOffset = BigEndianConverter.ConvertBytesToUInt32(rootBlock, 512 - 0x08);
        var dirCacheBlock = await ReadBlock(stream, partitionOffset + dirCacheBlockOffset * 512L);

        // assert - root block extension points to empty dir cache block with root block as parent
        Assert.NotEqual(0U, dirCacheBlockOffset);
        Assert.NotEqual(rootBlockOffset, dirCacheBlockOffset);
        Assert.Equal(Constants.T_DIRC, BigEndianConverter.ConvertBytesToInt32(dirCacheBlock, 0x0));
        Assert.Equal(dirCacheBlockOffset, BigEndianConverter.ConvertBytesToUInt32(dirCacheBlock, 0x4));
        Assert.Equal(rootBlockOffset, BigEndianConverter.ConvertBytesToUInt32(dirCacheBlock, 0x8));
        Assert.Equal(0U, BigEndianConverter.ConvertBytesToUInt32(dirCacheBlock, 0xc));
        Assert.Equal(0U, BigEndianConverter.ConvertBytesToUInt32(dirCacheBlock, 0x10));
        Assert.Equal(ChecksumHelper.CalculateChecksum(dirCacheBlock, 0x14),
            BigEndianConverter.ConvertBytesToInt32(dirCacheBlock, 0x14));

        // assert - dir cache block is marked used in bitmap, so it's not allocated for file data and only gets
        // a record for the file created
        await using (var ffsVolume = await MountVolume(stream))
        {
            await WriteFile(ffsVolume, "file1.txt", CreateData(10000));
        }

        var updatedDirCacheBlock = await ReadBlock(stream, partitionOffset + dirCacheBlockOffset * 512L);
        Assert.Equal(Constants.T_DIRC, BigEndianConverter.ConvertBytesToInt32(updatedDirCacheBlock, 0x0));
        Assert.Equal(dirCacheBlockOffset, BigEndianConverter.ConvertBytesToUInt32(updatedDirCacheBlock, 0x4));
        Assert.Equal(1U, BigEndianConverter.ConvertBytesToUInt32(updatedDirCacheBlock, 0xc));
    }

    [Fact]
    public async Task When_CreatingFilesInRootDirectory_Then_FilesAreListedAndCanBeRead()
    {
        // arrange - create dos5 formatted disk
        var stream = await CreateFastFileSystemFormattedDisk(dosType: Dos5DosType);

        // act - create files in root directory
        await using (var ffsVolume = await MountVolume(stream))
        {
            await WriteFile(ffsVolume, "file1.txt", CreateData(10000));
            await WriteFile(ffsVolume, "file2.txt", CreateData(100));
        }

        // assert - files are listed and can be read after mounting again
        await using var mountedVolume = await MountVolume(stream);
        var entries = (await mountedVolume.ListEntries()).ToList();
        Assert.Equal(new[] { "file1.txt", "file2.txt" }, entries.Select(x => x.Name).OrderBy(x => x));
        Assert.Equal(CreateData(10000), await ReadFile(mountedVolume, "file1.txt"));
        Assert.Equal(CreateData(100), await ReadFile(mountedVolume, "file2.txt"));
    }

    [Fact]
    public async Task When_ChangingToSubDirectory_Then_FilesInSubDirectoryCanBeCreatedAndRead()
    {
        // arrange - create dos5 formatted disk with directory
        var stream = await CreateFastFileSystemFormattedDisk(dosType: Dos5DosType);
        await using (var ffsVolume = await MountVolume(stream))
        {
            await ffsVolume.CreateDirectory("dir1");

            // act - change to sub directory and create file
            await ffsVolume.ChangeDirectory("dir1");
            await ffsVolume.CreateDirectory("dir2");
            await ffsVolume.ChangeDirectory("dir2");
            await WriteFile(ffsVolume, "file1.txt", CreateData(2000));
        }

        // assert - file in sub directory can be listed and read using relative and absolute paths
        await using var mountedVolume = await MountVolume(stream);
        await mountedVolume.ChangeDirectory("dir1/dir2");
        Assert.Equal("file1.txt", Assert.Single(await mountedVolume.ListEntries()).Name);
        Assert.Equal(CreateData(2000), await ReadFile(mountedVolume, "file1.txt"));
        await mountedVolume.ChangeDirectory("/dir1");
        Assert.Equal("dir2", Assert.Single(await mountedVolume.ListEntries()).Name);
    }

    [Fact]
    public async Task When_CreatingHardLinkToFile_Then_FileCanBeReadThroughHardLink()
    {
        // arrange - create dos5 formatted disk with file
        var stream = await CreateFastFileSystemFormattedDisk(dosType: Dos5DosType);
        await using (var ffsVolume = await MountVolume(stream))
        {
            await WriteFile(ffsVolume, "file1.txt", CreateData(3000));

            // act - create hard link to file
            await ffsVolume.CreateLink("link1", "file1.txt");
        }

        // assert - file can be read through hard link
        await using var mountedVolume = await MountVolume(stream);
        Assert.Equal(CreateData(3000), await ReadFile(mountedVolume, "link1"));
    }

    [Fact]
    public async Task When_CreatingHardLinkToDirectory_Then_DirectoryCanBeChangedToThroughHardLink()
    {
        // arrange - create dos5 formatted disk with directory and file
        var stream = await CreateFastFileSystemFormattedDisk(dosType: Dos5DosType);
        await using (var ffsVolume = await MountVolume(stream))
        {
            await ffsVolume.CreateDirectory("dir1");
            await ffsVolume.ChangeDirectory("dir1");
            await WriteFile(ffsVolume, "file1.txt", CreateData(500));
            await ffsVolume.ChangeDirectory("/");

            // act - create hard link to directory
            await ffsVolume.CreateLink("link1", "dir1");
        }

        // assert - directory can be changed to through hard link
        await using var mountedVolume = await MountVolume(stream);
        await mountedVolume.ChangeDirectory("link1");
        Assert.Equal(CreateData(500), await ReadFile(mountedVolume, "file1.txt"));
    }

    private static byte[] CreateData(int size) =>
        Enumerable.Range(0, size).Select(x => (byte)(x % 251)).ToArray();

    private static async Task WriteFile(FastFileSystemVolume ffsVolume, string name, byte[] data)
    {
        await ffsVolume.CreateFile(name, true, true);
        await using var entryStream = await ffsVolume.OpenFile(name, FileMode.Append);
        await entryStream.WriteAsync(data, 0, data.Length);
    }

    private static async Task<byte[]> ReadFile(FastFileSystemVolume ffsVolume, string name)
    {
        await using var entryStream = await ffsVolume.OpenFile(name, FileMode.Read);
        var memoryStream = new MemoryStream();
        await entryStream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }

    private static async Task<byte[]> ReadBlock(Stream stream, long offset)
    {
        var data = new byte[512];
        stream.Seek(offset, SeekOrigin.Begin);
        var read = await stream.ReadAsync(data, 0, data.Length);
        Assert.Equal(data.Length, read);
        return data;
    }
}
