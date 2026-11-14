namespace Hst.Amiga.Tests.SmartFileSystemTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Core.Converters;
using FileSystems;
using FileSystems.SmartFileSystem;
using FileSystems.SmartFileSystem.Blocks;
using FileSystems.SmartFileSystem.Doctor;
using Xunit;
using Xunit.Abstractions;
using Constants = FileSystems.SmartFileSystem.Constants;
using FileMode = FileSystems.FileMode;

[Trait("Category", "SFS")]
public partial class GivenSmartFileSystemDoctor : SmartFileSystemTestBase
{
    private const long DiskSize10Mb = 10 * 1024 * 1024;
    private const int LargeFileSize = 100 * 1024;
    private static readonly DateTime File1Date = new(2024, 1, 2, 3, 4, 6);

    private readonly ITestOutputHelper output;

    public GivenSmartFileSystemDoctor(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task When_CheckingDiskWithDirectoriesAndFiles_Then_NoIssuesAreFound()
    {
        // arrange - create disk with directories and files
        var stream = await CreateDiskWithDirectoriesAndFiles();

        // act - check disk
        var doctor = await RunDoctor(stream, Mode.Check);

        // assert - no issues are found and entries are counted
        Assert.False(doctor.Aborted);
        Assert.Empty(doctor.Issues);
        Assert.Equal(3, doctor.Stats.Directories);
        Assert.Equal(4, doctor.Stats.Files);
        Assert.Equal(1, doctor.Stats.SoftLinks);
    }

    [Fact]
    public async Task When_CheckingDiskWithManyFiles_Then_NoIssuesAreFound()
    {
        // arrange - create disk with enough files for multiple levels in object node tree and extent B-tree
        var stream = await CreateDiskWithManyFiles();

        // act - check disk
        var doctor = await RunDoctor(stream, Mode.Check);

        // assert - no issues are found
        Assert.Empty(doctor.Issues);
        Assert.Equal(150, doctor.Stats.Files);
    }

    [Fact]
    public async Task When_CheckingDiskWithInvalidRootBlocks_Then_CheckIsAborted()
    {
        // arrange - create disk and destroy both root blocks
        var stream = await CreateDiskWithDirectoriesAndFiles();
        var volume = await OpenVolume(stream);
        await WriteRawBlock(volume, 0, new byte[volume.BlockSize]);
        await WriteRawBlock(volume, volume.Blocks - 1, new byte[volume.BlockSize]);

        // act - check disk
        var doctor = await RunDoctor(stream, Mode.Check);

        // assert - check is aborted
        Assert.True(doctor.Aborted);
        Assert.True(doctor.HasErrors);
        Assert.Contains(doctor.Issues, x => x.Type == IssueType.NotSfsDisk);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task When_RepairingInvalidRootBlock_Then_RootBlockIsRebuilt(bool firstRootBlock)
    {
        // arrange - create disk and destroy one root block
        var stream = await CreateDiskWithDirectoriesAndFiles();
        var volume = await OpenVolume(stream);
        var block = firstRootBlock ? 0 : volume.Blocks - 1;
        var data = await ReadRawBlock(volume, block);
        data[0x10] ^= 0xff;
        await WriteRawBlock(volume, block, data);

        // act & assert - check finds invalid root block
        var check = await RunDoctor(stream, Mode.Check);
        Assert.Contains(check.Issues, x => x.Type == IssueType.RootBlockInvalid && x.Block == block);
        Assert.True(check.HasErrors);

        // act & assert - repair rebuilds root block
        await RepairAndAssertDiskIsValid(stream);
    }

    [Fact]
    public async Task When_RepairingBitmapWithAllBlocksFree_Then_BitmapIsRebuilt()
    {
        // arrange - create disk and mark all blocks free in bitmap
        var stream = await CreateDiskWithDirectoriesAndFiles();
        var volume = await OpenVolume(stream);
        var rootBlock = await ReadRootBlock(stream);
        var bitmapBlocks = Bitmap.CalculateBitmapBlocks(volume.Blocks, volume.BlockSize);
        for (var i = 0; i < bitmapBlocks; i++)
        {
            var block = rootBlock.BitmapBase + (uint)i;
            var data = await ReadRawBlock(volume, block);
            for (var offset = Constants.BitmapHeaderSize; offset < data.Length; offset++)
            {
                data[offset] = 0xff;
            }

            await WriteRawBlock(volume, block, data, true);
        }

        // act & assert - check finds used blocks marked free
        var check = await RunDoctor(stream, Mode.Check);
        Assert.Contains(check.Issues, x => x.Type == IssueType.BlocksUsedMarkedFree);
        Assert.Contains(check.Issues, x => x.Type == IssueType.BitmapPadding);

        // act & assert - repair rebuilds bitmap
        await RepairAndAssertDiskIsValid(stream);
    }

    [Fact]
    public async Task When_RepairingRootInfoWithWrongFreeBlocks_Then_FreeBlocksIsUpdated()
    {
        // arrange - create disk and change free blocks in root info
        var stream = await CreateDiskWithDirectoriesAndFiles();
        var volume = await OpenVolume(stream);
        var rootBlock = await ReadRootBlock(stream);
        var data = await ReadRawBlock(volume, rootBlock.RootObjectContainer);
        SetUInt32(data, volume.BlockSize - 28, 12345);
        await WriteRawBlock(volume, rootBlock.RootObjectContainer, data, true);

        // act & assert - check finds root info mismatch as warning
        var check = await RunDoctor(stream, Mode.Check);
        var issue = Assert.Single(check.Issues);
        Assert.Equal(IssueType.RootInfoMismatch, issue.Type);
        Assert.True(issue.Warning);
        Assert.False(check.HasErrors);

        // act & assert - repair updates free blocks
        await RepairAndAssertDiskIsValid(stream);
    }

    [Fact]
    public async Task When_RepairingDiskWithPendingTransaction_Then_TransactionIsReplayed()
    {
        // arrange - create disk and write a pending transaction, which changes comment of file1.txt
        var stream = await CreateDiskWithDirectoriesAndFiles();
        var volume = await OpenVolume(stream);
        var rootBlock = await ReadRootBlock(stream);
        var file1 = await FindObject(stream, "file1.txt");
        var containerBlock = file1.Container.OwnBlock;
        var container = file1.Container;
        container.Objects.First(x => x.Name == "file1.txt").Comment = "replayed";
        var newContainer = ObjectContainerBuilder.Build(container, volume.BlockSize);
        await WritePendingTransaction(volume, rootBlock, containerBlock, newContainer);

        // act & assert - check finds pending transaction and doesn't change disk
        var check = await RunDoctor(stream, Mode.Check);
        var issue = Assert.Single(check.Issues);
        Assert.Equal(IssueType.TransactionPending, issue.Type);
        Assert.False(check.HasErrors);
        Assert.Equal(Constants.TransactionFailureId,
            BigEndianConverter.ConvertBytesToUInt32(
                await ReadRawBlock(volume, Transactions.GetTransactionBlock(rootBlock)), 0));

        // act & assert - repair replays transaction
        await RepairAndAssertDiskIsValid(stream);
        Assert.Equal("replayed", (await FindObject(stream, "file1.txt")).Object.Comment);
    }

    [Fact]
    public async Task When_RepairingInvalidTransactionBlock_Then_TransactionOkBlockIsWritten()
    {
        // arrange - create disk and destroy transaction block
        var stream = await CreateDiskWithDirectoriesAndFiles();
        var volume = await OpenVolume(stream);
        var rootBlock = await ReadRootBlock(stream);
        await WriteRawBlock(volume, Transactions.GetTransactionBlock(rootBlock), new byte[volume.BlockSize]);

        // act & assert - check finds invalid transaction block
        var check = await RunDoctor(stream, Mode.Check);
        Assert.Contains(check.Issues, x => x.Type == IssueType.TransactionBlockInvalid);

        // act & assert - repair writes transaction ok block
        await RepairAndAssertDiskIsValid(stream);
    }

    [Fact]
    public async Task When_RepairingInvalidObjectContainer_Then_ObjectsInItAreRemoved()
    {
        // arrange - create disk and destroy object container of dir1
        var stream = await CreateDiskWithDirectoriesAndFiles();
        var volume = await OpenVolume(stream);
        var dir1 = await FindObject(stream, "dir1");
        await WriteRawBlock(volume, dir1.Object.FirstDirBlock, new byte[volume.BlockSize]);

        // act & assert - check finds invalid object container
        var check = await RunDoctor(stream, Mode.Check);
        Assert.Contains(check.Issues, x => x.Type == IssueType.ObjectContainerInvalid && x.Path == "dir1");

        // act & assert - repair removes object container and objects in it
        var repair = await RepairAndAssertDiskIsValid(stream);
        Assert.Equal(3, repair.Stats.Files);
        Assert.Equal(CreateTestData(LargeFileSize), await ReadFile(stream, "file1.txt"));
    }

    [Fact]
    public async Task When_RepairingObjectContainerWithWrongParent_Then_ParentIsUpdated()
    {
        // arrange - create disk and change parent of object container in dir1
        var stream = await CreateDiskWithDirectoriesAndFiles();
        var volume = await OpenVolume(stream);
        var dir1 = await FindObject(stream, "dir1");
        var data = await ReadRawBlock(volume, dir1.Object.FirstDirBlock);
        SetUInt32(data, 0xc, 1234);
        await WriteRawBlock(volume, dir1.Object.FirstDirBlock, data, true);

        // act & assert - check finds wrong parent
        var check = await RunDoctor(stream, Mode.Check);
        Assert.Contains(check.Issues, x => x.Type == IssueType.ObjectContainerChain);

        // act & assert - repair updates parent
        await RepairAndAssertDiskIsValid(stream);
        Assert.Equal(CreateTestData(2000), await ReadFile(stream, "dir1/file2.txt"));
    }

    [Fact]
    public async Task When_RepairingBrokenHashChain_Then_HashTableIsRebuilt()
    {
        // arrange - create disk and clear root directory hash table entries
        var stream = await CreateDiskWithDirectoriesAndFiles();
        var volume = await OpenVolume(stream);
        var root = await ReadRootObject(stream);
        var data = await ReadRawBlock(volume, root.HashTable);
        Array.Clear(data, Constants.HashTableHeaderSize, data.Length - Constants.HashTableHeaderSize);
        await WriteRawBlock(volume, root.HashTable, data, true);

        // act & assert - check finds invalid hash chains
        var check = await RunDoctor(stream, Mode.Check);
        var issue = Assert.Single(check.Issues);
        Assert.Equal(IssueType.HashChainInvalid, issue.Type);

        // act & assert - repair rebuilds hash table
        await RepairAndAssertDiskIsValid(stream);
        Assert.Equal(CreateTestData(100), await ReadFile(stream, "small.txt"));
    }

    [Fact]
    public async Task When_RepairingInvalidHashTableBlock_Then_HashTableIsRebuilt()
    {
        // arrange - create disk and destroy hash table block of dir1
        var stream = await CreateDiskWithDirectoriesAndFiles();
        var volume = await OpenVolume(stream);
        var dir1 = await FindObject(stream, "dir1");
        await WriteRawBlock(volume, dir1.Object.HashTable, new byte[volume.BlockSize]);

        // act & assert - check finds invalid hash table
        var check = await RunDoctor(stream, Mode.Check);
        Assert.Contains(check.Issues, x => x.Type == IssueType.HashTableInvalid && x.Path == "dir1");

        // act & assert - repair rebuilds hash table
        await RepairAndAssertDiskIsValid(stream);
        Assert.Equal(CreateTestData(2000), await ReadFile(stream, "dir1/file2.txt"));
    }

    [Fact]
    public async Task When_RepairingObjectNodePointingToWrongBlock_Then_ObjectNodeIsUpdated()
    {
        // arrange - create disk and change object node of small.txt to point to another block
        var stream = await CreateDiskWithDirectoriesAndFiles();
        var small = await FindObject(stream, "small.txt");
        await UpdateObjectNode(stream, small.Object.ObjectNode, x => x.Data = 1234);

        // act & assert - check finds invalid object node
        var check = await RunDoctor(stream, Mode.Check);
        Assert.Contains(check.Issues, x => x.Type == IssueType.ObjectNodeInvalid && x.Path == "small.txt");

        // act & assert - repair updates object node
        await RepairAndAssertDiskIsValid(stream);
        Assert.Equal(CreateTestData(100), await ReadFile(stream, "small.txt"));
    }

    [Fact]
    public async Task When_RepairingDuplicateObjectNode_Then_NewObjectNodeIsAllocated()
    {
        // arrange - create disk and set object node of small.txt to object node of file1.txt
        var stream = await CreateDiskWithDirectoriesAndFiles();
        var volume = await OpenVolume(stream);
        var file1 = await FindObject(stream, "file1.txt");
        var small = await FindObject(stream, "small.txt");
        await UpdateObject(volume, small, x => x.ObjectNode = file1.Object.ObjectNode);

        // act & assert - check finds duplicate object node
        var check = await RunDoctor(stream, Mode.Check);
        Assert.Contains(check.Issues, x => x.Type == IssueType.ObjectNodeNumberInvalid);

        // act & assert - repair allocates new object node
        await RepairAndAssertDiskIsValid(stream);
        Assert.Equal(CreateTestData(100), await ReadFile(stream, "small.txt"));
        Assert.Equal(CreateTestData(LargeFileSize), await ReadFile(stream, "file1.txt"));
    }

    [Fact]
    public async Task When_RepairingInvalidNodeContainer_Then_ObjectNodeTreeIsRebuilt()
    {
        // arrange - create disk with many files and destroy a leaf node container
        var stream = await CreateDiskWithManyFiles();
        var volume = await OpenVolume(stream);
        var rootBlock = await ReadRootBlock(stream);
        var rootNodeContainer = NodeContainerParser.Parse(await ReadRawBlock(volume, rootBlock.ObjectNodeRoot));
        Assert.False(rootNodeContainer.IsLeaf);
        var leafBlock = rootNodeContainer.Pointers[1] >> (SmartFileSystemHelper.Log2(volume.BlockSize) - 5);
        await WriteRawBlock(volume, leafBlock, new byte[volume.BlockSize]);

        // act & assert - check finds invalid node container
        var check = await RunDoctor(stream, Mode.Check);
        Assert.Contains(check.Issues, x => x.Type == IssueType.NodeContainerInvalid);

        // act & assert - repair rebuilds object node tree
        var repair = await RepairAndAssertDiskIsValid(stream);
        Assert.Equal(150, repair.Stats.Files);
        Assert.Equal(CreateTestData(1000, 100), await ReadFile(stream, "file100.txt"));
    }

    [Fact]
    public async Task When_RepairingUnreachableBNodeContainer_Then_ExtentBTreeIsRebuilt()
    {
        // arrange - create disk with many files and make a leaf B-tree node container unreachable
        var stream = await CreateDiskWithManyFiles();
        var volume = await OpenVolume(stream);
        var rootBlock = await ReadRootBlock(stream);
        var data = await ReadRawBlock(volume, rootBlock.ExtentBNodeRoot);
        var root = BNodeContainerParser.Parse(data);
        Assert.False(root.IsLeaf);
        SetUInt32(data, Constants.BNodeContainerHeaderSize + Constants.BNodeSize + 4, volume.Blocks + 100);
        await WriteRawBlock(volume, rootBlock.ExtentBNodeRoot, data, true);

        // act & assert - check finds invalid B-tree node container
        var check = await RunDoctor(stream, Mode.Check);
        Assert.Contains(check.Issues, x => x.Type == IssueType.BNodeContainerInvalid);
        Assert.DoesNotContain(check.Issues, x => x.Type == IssueType.ExtentInvalid);

        // act & assert - repair rebuilds extent B-tree with extents found in unreachable container
        var repair = await RepairAndAssertDiskIsValid(stream);
        Assert.Equal(150, repair.Stats.Files);
        for (var i = 0; i < 150; i += 10)
        {
            Assert.Equal(CreateTestData(1000, i), await ReadFile(stream, $"file{i}.txt"));
        }
    }

    [Fact]
    public async Task When_RepairingFileWithMissingExtent_Then_FileIsTruncated()
    {
        // arrange - create disk and set first extent of file1.txt to a key not in extent B-tree
        var stream = await CreateDiskWithDirectoriesAndFiles();
        var volume = await OpenVolume(stream);
        var file1 = await FindObject(stream, "file1.txt");
        await UpdateObject(volume, file1, x => x.Data = volume.Blocks - 10);

        // act & assert - check finds invalid extent
        var check = await RunDoctor(stream, Mode.Check);
        Assert.Contains(check.Issues, x => x.Type == IssueType.ExtentInvalid && x.Path == "file1.txt");

        // act & assert - repair truncates file and old extent is freed
        await RepairAndAssertDiskIsValid(stream);
        Assert.Empty(await ReadFile(stream, "file1.txt"));
    }

    [Fact]
    public async Task When_RepairingFileWithTooLargeSize_Then_FileSizeIsUpdated()
    {
        // arrange - create disk and change size of small.txt
        var stream = await CreateDiskWithDirectoriesAndFiles();
        var volume = await OpenVolume(stream);
        var small = await FindObject(stream, "small.txt");
        await UpdateObject(volume, small, x => x.Size = 100000);

        // act & assert - check finds file size mismatch
        var check = await RunDoctor(stream, Mode.Check);
        Assert.Contains(check.Issues, x => x.Type == IssueType.FileSizeMismatch && x.Path == "small.txt");

        // act & assert - repair sets file size to size of extents
        await RepairAndAssertDiskIsValid(stream);
        Assert.Equal(512U, (await FindObject(stream, "small.txt")).Object.Size);
    }

    [Fact]
    public async Task When_RepairingFileUsingExtentOfOtherFile_Then_FileNotOwningExtentIsRemoved()
    {
        // arrange - create disk and let newer dir1/file2.txt use extent of older file1.txt, extent previous still
        // points to file1.txt
        var stream = await CreateDiskWithDirectoriesAndFiles();
        var volume = await OpenVolume(stream);
        var file1 = await FindObject(stream, "file1.txt");
        var file2 = await FindObject(stream, "dir1", "file2.txt");
        await UpdateObject(volume, file2, x => x.Data = file1.Object.Data);

        // act & assert - check finds cross linked extent
        var check = await RunDoctor(stream, Mode.Check);
        Assert.Contains(check.Issues, x => x.Type == IssueType.CrossLinkedExtent && x.Path == "dir1/file2.txt");

        // act & assert - repair removes file2.txt, which doesn't own extent, and keeps file1.txt
        var repair = await RepairAndAssertDiskIsValid(stream);
        Assert.Equal(3, repair.Stats.Files);
        Assert.Equal(CreateTestData(LargeFileSize), await ReadFile(stream, "file1.txt"));
    }

    [Fact]
    public async Task When_RepairingCrossLinkedFiles_Then_OlderFileIsRemoved()
    {
        // arrange - create disk and extend extent of older file1.txt to overlap first block of newer small.txt
        var stream = await CreateDiskWithDirectoriesAndFiles();
        var volume = await OpenVolume(stream);
        var file1 = await FindObject(stream, "file1.txt");
        var small = await FindObject(stream, "small.txt");
        var rawVolume = await MountRaw(stream);
        var extent = await ExtentBTree.FindExtent(rawVolume, file1.Object.Data);
        var extentNode = (ExtentBNode)extent.Node;
        Assert.Equal(0U, extentNode.Next);
        var blocks = (ushort)(small.Object.Data - extentNode.Key + 1);
        extentNode.Blocks = blocks;
        await Disk.WriteBNodeContainer(rawVolume, extent.Container);
        await UpdateObject(volume, file1, x => x.Size = blocks * 512U);

        // act & assert - check finds cross linked extent
        var check = await RunDoctor(stream, Mode.Check);
        Assert.Contains(check.Issues, x => x.Type == IssueType.CrossLinkedExtent && x.Path == "file1.txt");

        // act & assert - repair removes older file1.txt and keeps newer small.txt
        var repair = await RepairAndAssertDiskIsValid(stream);
        Assert.Equal(3, repair.Stats.Files);
        Assert.Equal(CreateTestData(100), await ReadFile(stream, "small.txt"));
    }

    [Fact]
    public async Task When_RepairingDuplicateNames_Then_ObjectIsRenamed()
    {
        // arrange - create disk and rename small.txt to file1.txt without updating hash table
        var stream = await CreateDiskWithDirectoriesAndFiles();
        var volume = await OpenVolume(stream);
        var small = await FindObject(stream, "small.txt");
        await UpdateObject(volume, small, x => x.Name = "file1.txt");

        // act & assert - check finds duplicate name
        var check = await RunDoctor(stream, Mode.Check);
        Assert.Contains(check.Issues, x => x.Type == IssueType.DuplicateName);

        // act & assert - repair renames object
        await RepairAndAssertDiskIsValid(stream);
        var names = await ListNames(stream);
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task When_RepairingInvalidName_Then_ObjectIsRenamed()
    {
        // arrange - create disk and rename small.txt to name with ':'
        var stream = await CreateDiskWithDirectoriesAndFiles();
        var volume = await OpenVolume(stream);
        var small = await FindObject(stream, "small.txt");
        await UpdateObject(volume, small, x => x.Name = "sma:l.txt");

        // act & assert - check finds invalid name
        var check = await RunDoctor(stream, Mode.Check);
        Assert.Contains(check.Issues, x => x.Type == IssueType.InvalidName);

        // act & assert - repair renames object
        await RepairAndAssertDiskIsValid(stream);
        Assert.Equal(CreateTestData(100), await ReadFile(stream, "sma_l.txt"));
    }

    [Fact]
    public async Task When_RepairingInvalidSoftLinkBlock_Then_SoftLinkIsRemoved()
    {
        // arrange - create disk and destroy soft link block of link1
        var stream = await CreateDiskWithDirectoriesAndFiles();
        var volume = await OpenVolume(stream);
        var link1 = await FindObject(stream, "link1");
        await WriteRawBlock(volume, link1.Object.Data, new byte[volume.BlockSize]);

        // act & assert - check finds invalid soft link
        var check = await RunDoctor(stream, Mode.Check);
        Assert.Contains(check.Issues, x => x.Type == IssueType.SoftLinkInvalid && x.Path == "link1");

        // act & assert - repair removes soft link
        var repair = await RepairAndAssertDiskIsValid(stream);
        Assert.Equal(0, repair.Stats.SoftLinks);
        Assert.DoesNotContain("link1", await ListNames(stream));
    }

    [Fact]
    public async Task When_RepairingAdminSpaceWithClearedBits_Then_AdminSpaceBitsAreUpdated()
    {
        // arrange - create disk and clear bits of first admin space
        var stream = await CreateDiskWithDirectoriesAndFiles();
        var volume = await OpenVolume(stream);
        var rootBlock = await ReadRootBlock(stream);
        var data = await ReadRawBlock(volume, rootBlock.AdminSpaceContainer);
        SetUInt32(data, Constants.AdminSpaceContainerHeaderSize + 4, 0);
        await WriteRawBlock(volume, rootBlock.AdminSpaceContainer, data, true);

        // act & assert - check finds administration blocks not marked used
        var check = await RunDoctor(stream, Mode.Check);
        Assert.Contains(check.Issues, x => x.Type == IssueType.AdminBlockNotMarked);

        // act & assert - repair updates admin space bits
        await RepairAndAssertDiskIsValid(stream);
    }

    [Fact]
    public async Task When_RepairingInvalidRootObjectContainer_Then_RootDirectoryIsRecovered()
    {
        // arrange - create disk and destroy root object container
        var stream = await CreateDiskWithDirectoriesAndFiles();
        var volume = await OpenVolume(stream);
        var rootBlock = await ReadRootBlock(stream);
        await WriteRawBlock(volume, rootBlock.RootObjectContainer, new byte[volume.BlockSize]);

        // act & assert - check finds invalid root object container
        var check = await RunDoctor(stream, Mode.Check);
        Assert.Contains(check.Issues, x => x.Type == IssueType.RootObjectContainerInvalid);

        // act & assert - repair recovers root directory
        var repair = await RepairAndAssertDiskIsValid(stream);
        Assert.Equal(4, repair.Stats.Files);
        Assert.Equal(CreateTestData(LargeFileSize), await ReadFile(stream, "file1.txt"));
        Assert.Equal(CreateTestData(2000), await ReadFile(stream, "dir1/file2.txt"));
    }

    [Theory]
    [InlineData(512)]
    [InlineData(1024)]
    [InlineData(4096)]
    public async Task When_RepairingDiskWithBlockSizeAndInvalidStructures_Then_DiskIsValid(int blockSize)
    {
        // arrange - create disk and destroy first root block, a bitmap block and the hash table of dir1
        var stream = await CreateDiskWithDirectoriesAndFiles(blockSize);
        var volume = await OpenVolume(stream);
        var rootBlock = await ReadRootBlock(stream);
        var dir1 = await FindObject(stream, "dir1");
        await WriteRawBlock(volume, 0, new byte[volume.BlockSize]);
        await WriteRawBlock(volume, rootBlock.BitmapBase, new byte[volume.BlockSize]);
        await WriteRawBlock(volume, dir1.Object.HashTable, new byte[volume.BlockSize]);

        // act & assert - check finds invalid structures
        var check = await RunDoctor(stream, Mode.Check);
        Assert.Contains(check.Issues, x => x.Type == IssueType.RootBlockInvalid);
        Assert.Contains(check.Issues, x => x.Type == IssueType.BitmapBlockInvalid);
        Assert.Contains(check.Issues, x => x.Type == IssueType.HashTableInvalid);

        // act & assert - repair fixes all issues
        await RepairAndAssertDiskIsValid(stream);
        Assert.Equal(CreateTestData(LargeFileSize), await ReadFile(stream, "file1.txt"));
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
    public async Task When_RepairingDiskWithRandomCorruptMetadata_Then_DiskIsValid(int seed)
    {
        // arrange - create disk with many files and corrupt random bytes in metadata blocks between root block
        // and end of bitmap, every second block with updated checksum
        var stream = await CreateDiskWithManyFiles();
        var volume = await OpenVolume(stream);
        var rootBlock = await ReadRootBlock(stream);
        var lastMetadataBlock = rootBlock.BitmapBase + (uint)Bitmap.CalculateBitmapBlocks(volume.Blocks, volume.BlockSize);
        var random = new Random(seed);
        for (var i = 0; i < 20; i++)
        {
            var block = (uint)random.Next(1, (int)lastMetadataBlock);
            var data = await ReadRawBlock(volume, block);
            for (var j = 0; j < 4; j++)
            {
                data[random.Next(12, data.Length)] = (byte)random.Next(256);
            }

            await WriteRawBlock(volume, block, data, i % 2 == 0);
        }

        // act - check and repair disk
        var check = await RunDoctor(stream, Mode.Check);
        Assert.False(check.Aborted);
        var repair = await RunDoctor(stream, Mode.Repair);

        // assert - all issues are fixed and a following check finds no issues, except warnings for objects with
        // unsupported type, which are not changed
        Assert.False(repair.HasErrors);
        var recheck = await RunDoctor(stream, Mode.Check);
        Assert.All(recheck.Issues, x => Assert.Equal(IssueType.UnsupportedObject, x.Type));
        Assert.False(recheck.HasErrors);

        // unsupported objects are listed as files, but can't be read and verifier treats them as files
        if (recheck.Issues.Count == 0)
        {
            await AssertVolumeIsValid(stream);
            await ReadAllFiles(stream);
        }
    }

    private async Task<Stream> CreateDiskWithDirectoriesAndFiles(int blockSize = 512)
    {
        var stream = await CreateSmartFileSystemFormattedDisk(DiskSize10Mb, blockSize);

        await using (var volume = await MountVolume(stream))
        {
            await WriteFile(volume, "file1.txt", CreateTestData(LargeFileSize));
            await WriteFile(volume, "small.txt", CreateTestData(100));
            await WriteFile(volume, "empty.txt", Array.Empty<byte>());
            await volume.SetComment("file1.txt", "comment on file1");
            await volume.SetDate("file1.txt", File1Date);
            await volume.CreateLink("link1", "small.txt");
            await volume.CreateDirectory("dir1");
            await volume.CreateDirectory("dir2");
            await volume.ChangeDirectory("dir1");
            await WriteFile(volume, "file2.txt", CreateTestData(2000));
            await volume.Flush();
        }

        // disk written by the file system must be valid before tests corrupt it
        Assert.Empty((await RunDoctor(stream, Mode.Check)).Issues);
        await AssertVolumeIsValid(stream);

        return stream;
    }

    private async Task<Stream> CreateDiskWithManyFiles()
    {
        var stream = await CreateSmartFileSystemFormattedDisk(DiskSize10Mb);

        await using (var volume = await MountVolume(stream))
        {
            for (var i = 0; i < 150; i++)
            {
                await WriteFile(volume, $"file{i}.txt", CreateTestData(1000, i));
            }

            await volume.Flush();
        }

        return stream;
    }

    private async Task<SmartFileSystemDoctor> RunDoctor(Stream stream, Mode mode)
    {
        var volume = await OpenVolume(stream);
        volume.ShowMsg = message => output.WriteLine(message.TrimEnd());
        output.WriteLine($"--- {mode} ---");
        return await SmartFileSystemDoctor.Run(volume, mode);
    }

    /// <summary>
    /// Repair disk and assert all issues are fixed, a following check finds no issues and volume is valid.
    /// </summary>
    private async Task<SmartFileSystemDoctor> RepairAndAssertDiskIsValid(Stream stream)
    {
        var repair = await RunDoctor(stream, Mode.Repair);
        Assert.False(repair.HasErrors);
        Assert.All(repair.Issues, x => Assert.True(x.Fixed || x.Warning, x.ToString()));

        var check = await RunDoctor(stream, Mode.Check);
        Assert.False(check.Aborted);
        Assert.Empty(check.Issues);

        await AssertVolumeIsValid(stream);
        await ReadAllFiles(stream);
        return repair;
    }

    private static async Task ReadAllFiles(Stream stream)
    {
        await using var volume = await MountVolume(stream);
        await ReadAllFiles(volume, "/");
    }

    private static async Task ReadAllFiles(SmartFileSystemVolume volume, string path)
    {
        await volume.ChangeDirectory(path);
        foreach (var entry in (await volume.ListEntries()).ToList())
        {
            if (entry.Type == EntryType.Dir)
            {
                await ReadAllFiles(volume, path == "/" ? "/" + entry.Name : path + "/" + entry.Name);
                await volume.ChangeDirectory(path);
                continue;
            }

            if (entry.Type == EntryType.File)
            {
                await using var entryStream = await volume.OpenFile(entry.Name, FileMode.Read, false, true);
                await entryStream.CopyToAsync(Stream.Null);
            }
        }
    }

    private static async Task<byte[]> ReadFile(Stream stream, string path)
    {
        await using var volume = await MountVolume(stream);
        var parts = path.Split('/');
        if (parts.Length > 1)
        {
            await volume.ChangeDirectory(string.Join("/", parts.Take(parts.Length - 1)));
        }

        return await ReadFile(volume, parts.Last());
    }

    private static async Task<IList<string>> ListNames(Stream stream)
    {
        await using var volume = await MountVolume(stream);
        return (await volume.ListEntries()).Select(x => x.Name).ToList();
    }

    private static async Task<DoctorVolume> OpenVolume(Stream stream) =>
        SmartFileSystemDoctor.OpenVolume(stream, await ReadPartitionBlock(stream));

    private static async Task<RootBlock> ReadRootBlock(Stream stream)
    {
        var volume = await OpenVolume(stream);
        return RootBlockParser.Parse(await ReadRawBlock(volume, 0));
    }

    private static async Task<Volume> MountRaw(Stream stream) =>
        await SmartFileSystemHelper.Mount(stream, await ReadPartitionBlock(stream));

    private static async Task<SfsObject> ReadRootObject(Stream stream)
    {
        var volume = await MountRaw(stream);
        return (await Disk.ReadObjectContainer(volume, volume.RootBlock.RootObjectContainer)).Objects[0];
    }

    /// <summary>
    /// Find object by path from root directory.
    /// </summary>
    private static async Task<ObjectInfo> FindObject(Stream stream, params string[] path)
    {
        var volume = await MountRaw(stream);
        var dir = (await Disk.ReadObjectContainer(volume, volume.RootBlock.RootObjectContainer)).Objects[0];
        ObjectInfo objectInfo = null;
        foreach (var name in path)
        {
            objectInfo = await Objects.FindObject(volume, dir, name);
            Assert.NotNull(objectInfo);
            dir = objectInfo.Object;
        }

        return objectInfo;
    }

    /// <summary>
    /// Update object in object container and write object container with valid checksum, but without updating
    /// other structures.
    /// </summary>
    private static async Task UpdateObject(DoctorVolume volume, ObjectInfo objectInfo, Action<SfsObject> update)
    {
        var container = objectInfo.Container;
        update(container.Objects.First(x => x.ObjectNode == objectInfo.Object.ObjectNode &&
                                            x.Name == objectInfo.Object.Name));
        await WriteRawBlock(volume, container.OwnBlock, ObjectContainerBuilder.Build(container, volume.BlockSize));
    }

    private static async Task UpdateObjectNode(Stream stream, uint nodeNumber, Action<ObjectNode> update)
    {
        var volume = await MountRaw(stream);
        await NodeTree.UpdateNode(volume, nodeNumber, update);
    }

    /// <summary>
    /// Write pending transaction with one operation replacing block with new content. Transaction storage blocks
    /// are written to free blocks at end of partition.
    /// </summary>
    private static async Task WritePendingTransaction(DoctorVolume volume, RootBlock rootBlock, uint block,
        byte[] newContent)
    {
        // operation copying all longs of block, max 64 longs per code
        var compressed = new List<byte>();
        for (var offset = 0; offset < newContent.Length; offset += 256)
        {
            var longs = Math.Min(64, (newContent.Length - offset) / 4);
            compressed.Add((byte)(0xc0 | (longs - 1)));
            compressed.AddRange(newContent.Skip(offset).Take(longs * 4));
        }

        var operation = new List<byte>();
        operation.Add((byte)(compressed.Count >> 8));
        operation.Add((byte)compressed.Count);
        var blockBytes = new byte[4];
        BigEndianConverter.ConvertUInt32ToBytes(block, blockBytes, 0);
        operation.AddRange(blockBytes);
        operation.Add(0);
        operation.AddRange(compressed);

        // transaction storage blocks
        var storageSize = volume.BlockSize - Constants.TransactionStorageHeaderSize;
        var storageBlocks = (operation.Count + 2 + storageSize - 1) / storageSize;
        var firstStorageBlock = volume.Blocks - 100;
        for (var i = 0; i < storageBlocks; i++)
        {
            var storage = new TransactionStorageBlock
            {
                OwnBlock = firstStorageBlock + (uint)i,
                Next = i < storageBlocks - 1 ? firstStorageBlock + (uint)i + 1 : 0,
                Data = operation.Skip(i * storageSize).Take(storageSize).ToArray()
            };
            await WriteRawBlock(volume, storage.OwnBlock, TransactionBlockBuilder.BuildStorage(storage, volume.BlockSize));
        }

        var transactionBlock = Transactions.GetTransactionBlock(rootBlock);
        await WriteRawBlock(volume, transactionBlock, TransactionBlockBuilder.BuildFailure(
            new TransactionFailureBlock { OwnBlock = transactionBlock, FirstTransaction = firstStorageBlock },
            volume.BlockSize));
    }

    private static async Task<byte[]> ReadRawBlock(DoctorVolume volume, uint block)
    {
        var data = new byte[volume.BlockSize];
        volume.Stream.Seek(volume.PartitionStartOffset + (long)block * volume.BlockSize, SeekOrigin.Begin);
        var read = await volume.Stream.ReadAsync(data, 0, data.Length);
        Assert.Equal(data.Length, read);
        return data;
    }

    /// <summary>
    /// Write block, optionally updating checksum.
    /// </summary>
    private static async Task WriteRawBlock(DoctorVolume volume, uint block, byte[] data, bool updateChecksum = false)
    {
        if (updateChecksum)
        {
            FileSystems.SmartFileSystem.ChecksumHelper.UpdateChecksum(data);
        }

        volume.Stream.Seek(volume.PartitionStartOffset + (long)block * volume.BlockSize, SeekOrigin.Begin);
        await volume.Stream.WriteAsync(data, 0, data.Length);
    }

    private static void SetUInt32(byte[] data, int offset, uint value) =>
        BigEndianConverter.ConvertUInt32ToBytes(value, data, offset);
}
