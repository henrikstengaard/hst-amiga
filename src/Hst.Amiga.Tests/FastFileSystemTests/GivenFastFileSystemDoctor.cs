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
using RigidDiskBlocks;
using Xunit;
using Xunit.Abstractions;
using Constants = FileSystems.FastFileSystem.Constants;
using Directory = FileSystems.FastFileSystem.Directory;
using FileMode = FileSystems.FileMode;

[Trait("Category", "FFS")]
public partial class GivenFastFileSystemDoctor : FastFileSystemTestBase
{
    protected const long DiskSize10Mb = 10 * 1024 * 1024;
    protected const int LargeFileSize = 100 * 1024;
    protected static readonly DateTime File1Date = new(2024, 1, 2, 3, 4, 6);

    private readonly ITestOutputHelper output;

    public GivenFastFileSystemDoctor(ITestOutputHelper output)
    {
        this.output = output;
    }

    public static IEnumerable<object[]> DosTypes => new[]
    {
        new object[] { (byte)0 },
        new object[] { (byte)3 },
        new object[] { (byte)5 },
        new object[] { (byte)7 }
    };

    [Theory]
    [MemberData(nameof(DosTypes))]
    public async Task When_CheckingDiskWithDirectoriesAndFiles_Then_NoIssuesAreFound(byte dosType)
    {
        // arrange - create disk with directories and files
        var stream = await CreateDiskWithDirectoriesAndFiles(dosType);

        // act - check disk
        var doctor = await RunDoctor(stream, Mode.Check);

        // assert - no issues are found and entries are counted
        Assert.False(doctor.Aborted);
        Assert.Empty(doctor.Issues);
        Assert.Equal(1, doctor.Stats.Directories);
        Assert.Equal(3, doctor.Stats.Files);
        Assert.Equal(1, doctor.Stats.HardLinks);
    }

    [Theory]
    [MemberData(nameof(DosTypes))]
    public async Task When_RepairingNotValidatedDisk_Then_BitmapIsRebuilt(byte dosType)
    {
        // arrange - create disk and clear bitmap flag and bitmap
        var stream = await CreateDiskWithDirectoriesAndFiles(dosType);
        var volume = await OpenVolume(stream);
        var root = await ReadBlock(volume, volume.RootBlock);
        SetUInt32(root, volume.BlockSize - 0xc8, Constants.BM_INVALID);
        await WriteBlock(volume, volume.RootBlock, root);
        for (var b = 0; b < Constants.BM_SIZE; b++)
        {
            var bitmapBlock = BigEndianConverter.ConvertBytesToUInt32(root, volume.BlockSize - 0xc4 + b * 4);
            if (bitmapBlock == 0) break;
            var bitmap = new byte[volume.BlockSize];
            for (var i = 4; i < volume.BlockSize; i++) bitmap[i] = 0xff;
            await WriteBlock(volume, bitmapBlock, bitmap, 0);
        }

        // act - check disk
        var check = await RunDoctor(stream, Mode.Check);

        // assert - not validated and used blocks marked free are found and disk is not changed
        Assert.Contains(check.Issues, x => x.Type == IssueType.NotValidated);
        Assert.Contains(check.Issues, x => x.Type == IssueType.BlocksUsedMarkedFree);
        Assert.True(check.HasErrors);
        Assert.Equal(Constants.BM_INVALID,
            BigEndianConverter.ConvertBytesToUInt32(await ReadBlock(volume, volume.RootBlock), volume.BlockSize - 0xc8));

        // act - repair disk
        var repair = await RunDoctor(stream, Mode.Repair);

        // assert - issues are fixed and disk is valid
        Assert.False(repair.HasErrors);
        Assert.All(repair.Issues, x => Assert.True(x.Fixed));
        await AssertDiskIsValid(stream);
    }

    [Theory]
    [MemberData(nameof(DosTypes))]
    public async Task When_RepairingEntryWithChecksumError_Then_ChecksumIsUpdated(byte dosType)
    {
        // arrange - create disk and change unused byte after name of file header without updating checksum
        var stream = await CreateDiskWithDirectoriesAndFiles(dosType);
        var volume = await OpenVolume(stream);
        var block = await FindEntryBlock(volume, volume.RootBlock, "file1.txt", dosType);
        var data = await ReadBlock(volume, block);
        data[volume.BlockSize - 0x44] ^= 0x5a;
        await WriteBlock(volume, block, data, -1);

        // act - check disk
        var check = await RunDoctor(stream, Mode.Check);

        // assert - checksum error is found
        Assert.Contains(check.Issues, x => x.Type == IssueType.EntryBlockChecksum && x.Block == block);

        // act - repair disk
        var repair = await RunDoctor(stream, Mode.Repair);

        // assert - checksum error is fixed and file is intact
        Assert.False(repair.HasErrors);
        await AssertDiskIsValid(stream);
        Assert.Equal(CreateData(LargeFileSize), await ReadFile(stream, "file1.txt"));
    }

    [Theory]
    [MemberData(nameof(DosTypes))]
    public async Task When_RepairingHashChainPointingOutsideVolume_Then_HashChainIsTruncated(byte dosType)
    {
        // arrange - create disk and set hash chain of file to point outside volume
        var stream = await CreateDiskWithDirectoriesAndFiles(dosType);
        var volume = await OpenVolume(stream);
        var slot = Directory.GetHashValue(72, "file1.txt", true);
        var root = await ReadBlock(volume, volume.RootBlock);
        SetUInt32(root, 0x18 + slot * 4, volume.Blocks + 1000);
        await WriteBlock(volume, volume.RootBlock, root);

        // act - check disk
        var check = await RunDoctor(stream, Mode.Check);

        // assert - hash chain outside volume is found
        Assert.Contains(check.Issues, x => x.Type == IssueType.HashChainOutOfRange);

        // act - repair disk
        var repair = await RunDoctor(stream, Mode.Repair);

        // assert - hash chain is truncated, blocks are freed and disk is valid
        Assert.False(repair.HasErrors);
        Assert.Contains(repair.Issues, x => x.Type == IssueType.BlocksFreeMarkedUsed && x.Fixed);
        await AssertDiskIsValid(stream);
        Assert.DoesNotContain(await ListNames(stream), x => x == "file1.txt");
    }

    [Theory]
    [MemberData(nameof(DosTypes))]
    public async Task When_RepairingFileWithInvalidDataBlockPointer_Then_FileIsTruncated(byte dosType)
    {
        // arrange - create disk and set 11th data block pointer in file header outside volume
        var stream = await CreateDiskWithDirectoriesAndFiles(dosType);
        var volume = await OpenVolume(stream);
        var block = await FindEntryBlock(volume, volume.RootBlock, "file1.txt", dosType);
        var data = await ReadBlock(volume, block);
        SetUInt32(data, 0x18 + (72 - 1 - 10) * 4, volume.Blocks + 10);
        await WriteBlock(volume, block, data);

        // act - check disk
        var check = await RunDoctor(stream, Mode.Check);

        // assert - invalid data block pointer is found
        Assert.Contains(check.Issues, x => x.Type == IssueType.InvalidDataBlockPointer && x.Block == block);

        // act - repair disk
        var repair = await RunDoctor(stream, Mode.Repair);

        // assert - file is truncated to 10 data blocks and disk is valid
        Assert.False(repair.HasErrors);
        await AssertDiskIsValid(stream);
        var dataBlockSize = dosType == 0 ? 488 : 512;
        Assert.Equal(CreateData(LargeFileSize).Take(10 * dataBlockSize).ToArray(), await ReadFile(stream, "file1.txt"));
    }

    [Theory]
    [MemberData(nameof(DosTypes))]
    public async Task When_RepairingFileWithInvalidFileExtensionBlock_Then_FileIsTruncated(byte dosType)
    {
        // arrange - create disk and destroy first file extension block of file
        var stream = await CreateDiskWithDirectoriesAndFiles(dosType);
        var volume = await OpenVolume(stream);
        var block = await FindEntryBlock(volume, volume.RootBlock, "file1.txt", dosType);
        var data = await ReadBlock(volume, block);
        var extension = BigEndianConverter.ConvertBytesToUInt32(data, volume.BlockSize - 0x08);
        Assert.NotEqual(0U, extension);
        await WriteBlock(volume, extension, new byte[volume.BlockSize], -1);

        // act - repair disk
        var repair = await RunDoctor(stream, Mode.Repair);

        // assert - file is truncated to data blocks in file header and disk is valid
        Assert.Contains(repair.Issues, x => x.Type == IssueType.InvalidFileExtensionBlock && x.Fixed);
        Assert.False(repair.HasErrors);
        await AssertDiskIsValid(stream);
        var dataBlockSize = dosType == 0 ? 488 : 512;
        Assert.Equal(CreateData(LargeFileSize).Take(72 * dataBlockSize).ToArray(), await ReadFile(stream, "file1.txt"));
    }

    [Theory]
    [MemberData(nameof(DosTypes))]
    public async Task When_RepairingCrossLinkedFiles_Then_SecondFileIsTruncated(byte dosType)
    {
        // arrange - create disk and set first data block of file2 to first data block of file1
        var stream = await CreateDiskWithDirectoriesAndFiles(dosType);
        var volume = await OpenVolume(stream);
        var file1 = await ReadBlock(volume, await FindEntryBlock(volume, volume.RootBlock, "file1.txt", dosType));
        var dir1 = await FindEntryBlock(volume, volume.RootBlock, "dir1", dosType);
        var file2Block = await FindEntryBlock(volume, dir1, "file2.txt", dosType);
        var file2 = await ReadBlock(volume, file2Block);
        SetUInt32(file2, 0x18 + 71 * 4, BigEndianConverter.ConvertBytesToUInt32(file1, 0x18 + 71 * 4));
        await WriteBlock(volume, file2Block, file2);

        // act - repair disk
        var repair = await RunDoctor(stream, Mode.Repair);

        // assert - cross link is found and fixed and disk is valid
        Assert.Contains(repair.Issues,
            x => x.Fixed && x.Block == file2Block &&
                 (x.Type == IssueType.CrossLinkedBlock || x.Type == IssueType.InvalidDataBlock));
        Assert.False(repair.HasErrors);
        await AssertDiskIsValid(stream);
        Assert.Equal(CreateData(LargeFileSize), await ReadFile(stream, "file1.txt"));
    }

    [Theory]
    [MemberData(nameof(DosTypes))]
    public async Task When_RepairingEntryWithWrongParent_Then_ParentIsUpdated(byte dosType)
    {
        // arrange - create disk and set parent of file in dir1 to root block
        var stream = await CreateDiskWithDirectoriesAndFiles(dosType);
        var volume = await OpenVolume(stream);
        var dir1 = await FindEntryBlock(volume, volume.RootBlock, "dir1", dosType);
        var block = await FindEntryBlock(volume, dir1, "file2.txt", dosType);
        var data = await ReadBlock(volume, block);
        SetUInt32(data, volume.BlockSize - 0x0c, volume.RootBlock);
        await WriteBlock(volume, block, data);

        // act - repair disk
        var repair = await RunDoctor(stream, Mode.Repair);

        // assert - wrong parent is fixed
        Assert.Contains(repair.Issues, x => x.Type == IssueType.WrongParent && x.Fixed);
        Assert.Equal(dir1, BigEndianConverter.ConvertBytesToUInt32(await ReadBlock(volume, block), volume.BlockSize - 0x0c));
        await AssertDiskIsValid(stream);
    }

    [Theory]
    [MemberData(nameof(DosTypes))]
    public async Task When_RepairingDanglingHardLink_Then_HardLinkIsRemoved(byte dosType)
    {
        // arrange - create disk and set real entry of hard link outside volume
        var stream = await CreateDiskWithDirectoriesAndFiles(dosType);
        var volume = await OpenVolume(stream);
        var block = await FindEntryBlock(volume, volume.RootBlock, "link1", dosType);
        var data = await ReadBlock(volume, block);
        SetUInt32(data, volume.BlockSize - 0x2c, volume.Blocks + 5);
        await WriteBlock(volume, block, data);

        // act - repair disk
        var repair = await RunDoctor(stream, Mode.Repair);

        // assert - hard link is removed and disk is valid
        Assert.Contains(repair.Issues, x => x.Type == IssueType.DanglingLink && x.Fixed);
        Assert.False(repair.HasErrors);
        await AssertDiskIsValid(stream);
        Assert.DoesNotContain(await ListNames(stream), x => x == "link1");
    }

    [Theory]
    [MemberData(nameof(DosTypes))]
    public async Task When_RepairingDiskWithDestroyedRootBlock_Then_RootBlockIsRebuilt(byte dosType)
    {
        // arrange - create disk and destroy root block
        var stream = await CreateDiskWithDirectoriesAndFiles(dosType);
        var volume = await OpenVolume(stream);
        await WriteBlock(volume, volume.RootBlock, new byte[volume.BlockSize], -1);

        // act - check disk
        var check = await RunDoctor(stream, Mode.Check);

        // assert - check is aborted
        Assert.True(check.Aborted);
        Assert.Contains(check.Issues, x => x.Type == IssueType.RootBlockInvalid);

        // act - repair disk
        var repair = await RunDoctor(stream, Mode.Repair);

        // assert - root block is rebuilt with entries and disk is valid
        Assert.False(repair.HasErrors);
        await AssertDiskIsValid(stream);
        var names = await ListNames(stream);
        Assert.Contains("file1.txt", names);
        Assert.Contains("dir1", names);
        Assert.Equal(CreateData(LargeFileSize), await ReadFile(stream, "file1.txt"));
    }

    [Fact]
    public async Task When_RepairingDiskWithInvalidDirCacheBlock_Then_DirCacheIsRebuilt()
    {
        // arrange - create dos5 disk and destroy dir cache block of root directory
        var stream = await CreateDiskWithDirectoriesAndFiles(5);
        var volume = await OpenVolume(stream);
        var root = await ReadBlock(volume, volume.RootBlock);
        var dirCacheBlock = BigEndianConverter.ConvertBytesToUInt32(root, volume.BlockSize - 0x08);
        Assert.NotEqual(0U, dirCacheBlock);
        await WriteBlock(volume, dirCacheBlock, new byte[volume.BlockSize], -1);

        // act - check disk
        var check = await RunDoctor(stream, Mode.Check);

        // assert - invalid dir cache is found
        Assert.Contains(check.Issues, x => x.Type == IssueType.DirCacheInvalid && x.Block == volume.RootBlock);

        // act - repair disk
        var repair = await RunDoctor(stream, Mode.Repair);

        // assert - dir cache is rebuilt and entries can be listed
        Assert.False(repair.HasErrors);
        await AssertDiskIsValid(stream);
        var names = await ListNames(stream);
        Assert.Contains("file1.txt", names);
        Assert.Contains("dir1", names);
    }

    [Fact]
    public async Task When_RepairingBootBlockWithWrongDosType_Then_DosTypeIsUpdated()
    {
        // arrange - create disk and change boot block dos type
        var stream = await CreateDiskWithDirectoriesAndFiles(3);
        var volume = await OpenVolume(stream);
        var boot = await ReadBlock(volume, 0);
        SetUInt32(boot, 0, 0x444f5301);
        await WriteBlock(volume, 0, boot, -1);

        // act - repair disk
        var repair = await RunDoctor(stream, Mode.Repair);

        // assert - boot block dos type is updated
        Assert.Contains(repair.Issues, x => x.Type == IssueType.BootBlockDosType && x.Fixed);
        Assert.Equal(0x444f5303U, BigEndianConverter.ConvertBytesToUInt32(await ReadBlock(volume, 0)));
        await AssertDiskIsValid(stream);
    }

    /// <summary>
    /// Create disk with directories and files. Formatter doesn't create dir cache blocks, so a dir cache disk
    /// (DOS\4 and DOS\5) is created as a disk without dir cache, which is converted by changing dos type and
    /// letting doctor rebuild dir caches.
    /// </summary>
    [Fact]
    public async Task When_RepairingLargeDiskWithDestroyedBitmapExtensionBlock_Then_BitmapIsRebuilt()
    {
        // arrange - create 100mb disk, which needs more bitmap blocks than root block can point to
        var stream = await CreateDiskWithDirectoriesAndFiles(3, DiskSize100Mb);
        var volume = await OpenVolume(stream);
        var root = await ReadBlock(volume, volume.RootBlock);
        var bitmapExtension = BigEndianConverter.ConvertBytesToUInt32(root, volume.BlockSize - 0x60);
        Assert.NotEqual(0U, bitmapExtension);

        // assert - check finds no issues on large disk
        Assert.Empty((await RunDoctor(stream, Mode.Check)).Issues);

        // arrange - destroy bitmap extension block and clear bitmap flag
        await WriteBlock(volume, bitmapExtension, new byte[volume.BlockSize], -1);
        SetUInt32(root, volume.BlockSize - 0xc8, Constants.BM_INVALID);
        await WriteBlock(volume, volume.RootBlock, root);

        // act - check disk
        var check = await RunDoctor(stream, Mode.Check);

        // assert - invalid bitmap pointers are found
        Assert.Contains(check.Issues, x => x.Type == IssueType.BitmapPointerInvalid);

        // act - repair disk
        var repair = await RunDoctor(stream, Mode.Repair);

        // assert - bitmap blocks are relocated, bitmap extension block is rebuilt and disk is valid
        Assert.False(repair.HasErrors);
        await AssertDiskIsValid(stream);
        Assert.Equal(CreateData(LargeFileSize), await ReadFile(stream, "file1.txt"));

        // assert - new files can be written using rebuilt bitmap
        await using (var ffsVolume = await MountVolume(stream))
        {
            await WriteFile(ffsVolume, "new.txt", CreateData(LargeFileSize));
        }

        Assert.Empty((await RunDoctor(stream, Mode.Check)).Issues);
        Assert.Equal(CreateData(LargeFileSize), await ReadFile(stream, "new.txt"));
    }

    protected async Task<Stream> CreateDiskWithDirectoriesAndFiles(byte dosType, long diskSize = DiskSize10Mb)
    {
        var stream = await CreateFastFileSystemFormattedDisk(diskSize,
            new byte[] { 0x44, 0x4f, 0x53, dosType });

        await using (var ffsVolume = await MountVolume(stream))
        {
            await WriteFile(ffsVolume, "file1.txt", CreateData(LargeFileSize));
            await WriteFile(ffsVolume, "small.txt", CreateData(100));
            await ffsVolume.SetComment("file1.txt", "comment on file1");
            await ffsVolume.SetDate("file1.txt", File1Date);
            await ffsVolume.CreateLink("link1", "small.txt");
            await ffsVolume.CreateDirectory("dir1");
            await ffsVolume.ChangeDirectory("dir1");
            await WriteFile(ffsVolume, "file2.txt", CreateData(2000));
            await ffsVolume.Flush();
        }

        // disk written by the file system must be valid before tests corrupt it
        Assert.Empty((await RunDoctor(stream, Mode.Check)).Issues);

        return stream;
    }

    protected static byte[] CreateData(int size) =>
        Enumerable.Range(0, size).Select(x => (byte)(x % 251)).ToArray();

    private static async Task WriteFile(FastFileSystemVolume ffsVolume, string name, byte[] data)
    {
        await ffsVolume.CreateFile(name, true, true);
        await using var entryStream = await ffsVolume.OpenFile(name, FileMode.Append);
        await entryStream.WriteAsync(data, 0, data.Length);
    }

    protected async Task<byte[]> ReadFile(Stream stream, string name)
    {
        await using var ffsVolume = await MountVolume(stream);
        await using var entryStream = await ffsVolume.OpenFile(name, FileMode.Read);
        var memoryStream = new MemoryStream();
        await entryStream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }

    private async Task<IList<string>> ListNames(Stream stream)
    {
        await using var ffsVolume = await MountVolume(stream);
        return (await ffsVolume.ListEntries()).Select(x => x.Name).ToList();
    }

    protected async Task<FastFileSystemDoctor> RunDoctor(Stream stream, Mode mode)
    {
        var volume = await OpenVolume(stream);
        volume.ShowMsg = message => output.WriteLine(message.TrimEnd());
        output.WriteLine($"--- {mode} ---");
        return await FastFileSystemDoctor.Run(volume, mode);
    }

    /// <summary>
    /// Assert disk has no issues and all entries can be read.
    /// </summary>
    private async Task AssertDiskIsValid(Stream stream)
    {
        var check = await RunDoctor(stream, Mode.Check);
        Assert.False(check.Aborted);
        Assert.Empty(check.Issues);

        /* fast file system volume can't change to sub directories in dir cache volumes, entries listed from
         * dir cache have no entry block */
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        var dosType = rigidDiskBlock.PartitionBlocks.First().DosType[3];
        var recursive = dosType != 4 && dosType != 5;

        await using var ffsVolume = await MountVolume(stream);
        await ReadAllEntries(ffsVolume, "/", recursive);
    }

    private static async Task ReadAllEntries(FastFileSystemVolume ffsVolume, string path, bool recursive)
    {
        await ffsVolume.ChangeDirectory(path);
        foreach (var entry in (await ffsVolume.ListEntries()).ToList())
        {
            if (entry.Type == EntryType.Dir)
            {
                if (recursive)
                {
                    await ReadAllEntries(ffsVolume, path == "/" ? "/" + entry.Name : path + "/" + entry.Name, true);
                    await ffsVolume.ChangeDirectory(path);
                }

                continue;
            }

            if (entry.Type != EntryType.File)
            {
                continue;
            }

            await using var entryStream = await ffsVolume.OpenFile(entry.Name, FileMode.Read);
            await entryStream.CopyToAsync(Stream.Null);
        }
    }

    protected static async Task<DoctorVolume> OpenVolume(Stream stream)
    {
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        return FastFileSystemDoctor.OpenVolume(stream, rigidDiskBlock.PartitionBlocks.First());
    }

    protected static async Task<byte[]> ReadBlock(DoctorVolume volume, uint block)
    {
        var data = new byte[volume.BlockSize];
        volume.Stream.Seek(volume.PartitionStartOffset + (long)block * volume.BlockSize, SeekOrigin.Begin);
        var read = await volume.Stream.ReadAsync(data, 0, data.Length);
        Assert.Equal(data.Length, read);
        return data;
    }

    /// <summary>
    /// Write block and update checksum at checksum offset, -1 to not update checksum.
    /// </summary>
    protected static async Task WriteBlock(DoctorVolume volume, uint block, byte[] data, int checksumOffset = 0x14)
    {
        if (checksumOffset >= 0)
        {
            ChecksumHelper.UpdateChecksum(data, checksumOffset);
        }

        volume.Stream.Seek(volume.PartitionStartOffset + (long)block * volume.BlockSize, SeekOrigin.Begin);
        await volume.Stream.WriteAsync(data, 0, data.Length);
    }

    protected static void SetUInt32(byte[] data, int offset, uint value) =>
        BigEndianConverter.ConvertUInt32ToBytes(value, data, offset);

    /// <summary>
    /// Find entry block by walking hash chain for name in directory.
    /// </summary>
    protected static async Task<uint> FindEntryBlock(DoctorVolume volume, uint directory, string name, byte dosType)
    {
        var intl = dosType >= 2;
        var dir = await ReadBlock(volume, directory);
        var slot = Directory.GetHashValue(72, name, intl);
        var block = BigEndianConverter.ConvertBytesToUInt32(dir, 0x18 + slot * 4);
        while (block != 0)
        {
            var data = await ReadBlock(volume, block);
            var entryName = dosType >= 6
                ? AmigaTextHelper.GetString(data, volume.BlockSize - 0xb8 + 1, data[volume.BlockSize - 0xb8])
                : AmigaTextHelper.GetString(data, volume.BlockSize - 0x50 + 1, data[volume.BlockSize - 0x50]);
            if (entryName == name)
            {
                return block;
            }

            block = BigEndianConverter.ConvertBytesToUInt32(data, volume.BlockSize - 0x10);
        }

        throw new IOException($"Entry '{name}' not found");
    }
}
