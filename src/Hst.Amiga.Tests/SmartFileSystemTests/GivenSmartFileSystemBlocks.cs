namespace Hst.Amiga.Tests.SmartFileSystemTests;

using System;
using FileSystems.SmartFileSystem;
using FileSystems.SmartFileSystem.Blocks;
using Xunit;

public class GivenSmartFileSystemBlocks
{
    private const int BlockSize = 512;

    [Fact]
    public void WhenBuildingAndParsingRootBlockThenRootBlockIsEqual()
    {
        // arrange
        var rootBlock = new RootBlock
        {
            OwnBlock = 0,
            SequenceNumber = 2,
            DateCreated = new DateTime(2024, 5, 6, 7, 8, 9),
            Bits = Constants.RootBitsRecycled,
            FirstByte = 0x1_0000_0200,
            LastByte = 0x2_0000_0000,
            TotalBlocks = 1000,
            BlockSize = BlockSize,
            BitmapBase = 34,
            AdminSpaceContainer = 2,
            RootObjectContainer = 3,
            ExtentBNodeRoot = 6,
            ObjectNodeRoot = 7
        };

        // act
        var blockBytes = RootBlockBuilder.Build(rootBlock, BlockSize);
        var parsed = RootBlockParser.Parse(blockBytes);

        // assert
        Assert.True(ChecksumHelper.IsValid(blockBytes));
        Assert.Equal(new byte[] { 0x53, 0x46, 0x53, 0x00 }, blockBytes[..4]);
        Assert.Equal(Constants.StructureVersion, parsed.Version);
        Assert.Equal(rootBlock.SequenceNumber, parsed.SequenceNumber);
        Assert.Equal(rootBlock.DateCreated, parsed.DateCreated);
        Assert.Equal(rootBlock.Bits, parsed.Bits);
        Assert.Equal(rootBlock.FirstByte, parsed.FirstByte);
        Assert.Equal(rootBlock.LastByte, parsed.LastByte);
        Assert.Equal(rootBlock.TotalBlocks, parsed.TotalBlocks);
        Assert.Equal(rootBlock.BlockSize, parsed.BlockSize);
        Assert.Equal(rootBlock.BitmapBase, parsed.BitmapBase);
        Assert.Equal(rootBlock.AdminSpaceContainer, parsed.AdminSpaceContainer);
        Assert.Equal(rootBlock.RootObjectContainer, parsed.RootObjectContainer);
        Assert.Equal(rootBlock.ExtentBNodeRoot, parsed.ExtentBNodeRoot);
        Assert.Equal(rootBlock.ObjectNodeRoot, parsed.ObjectNodeRoot);
        Assert.True(parsed.Recycled);
        Assert.False(parsed.CaseSensitive);
    }

    [Fact]
    public void WhenBuildingAndParsingObjectContainerThenObjectsAreEqual()
    {
        // arrange - object container with directory and file with odd and even object sizes
        var objectContainer = new ObjectContainer
        {
            OwnBlock = 10,
            Parent = 1,
            Next = 20,
            Previous = 30
        };
        objectContainer.Objects.Add(new SfsObject
        {
            ObjectNode = 7,
            Protection = 0x0f,
            HashTable = 40,
            FirstDirBlock = 50,
            DateModified = new DateTime(2020, 1, 2, 3, 4, 5),
            Bits = Constants.OTypeDir,
            Name = "Dir"
        });
        objectContainer.Objects.Add(new SfsObject
        {
            ObjectNode = 8,
            Protection = 0x1f,
            Data = 60,
            Size = 12345,
            DateModified = new DateTime(2021, 2, 3, 4, 5, 6),
            Bits = 0,
            Name = "File.txt",
            Comment = "Comment"
        });

        // act
        var blockBytes = ObjectContainerBuilder.Build(objectContainer, BlockSize);
        var parsed = ObjectContainerParser.Parse(blockBytes);

        // assert - first object is 25 + 4 + 1 = 30 bytes, second object starts at offset 24 + 30
        Assert.True(ChecksumHelper.IsValid(blockBytes));
        Assert.Equal((byte)'D', blockBytes[24 + 25]);
        Assert.Equal((byte)'F', blockBytes[24 + 30 + 25]);
        Assert.Equal(objectContainer.Parent, parsed.Parent);
        Assert.Equal(objectContainer.Next, parsed.Next);
        Assert.Equal(objectContainer.Previous, parsed.Previous);
        Assert.Equal(2, parsed.Objects.Count);

        var dir = parsed.Objects[0];
        Assert.True(dir.IsDir);
        Assert.Equal(7U, dir.ObjectNode);
        Assert.Equal(40U, dir.HashTable);
        Assert.Equal(50U, dir.FirstDirBlock);
        Assert.Equal("Dir", dir.Name);
        Assert.Equal(string.Empty, dir.Comment);
        Assert.Equal(new DateTime(2020, 1, 2, 3, 4, 5), dir.DateModified);

        var file = parsed.Objects[1];
        Assert.True(file.IsFile);
        Assert.Equal(8U, file.ObjectNode);
        Assert.Equal(0x1fU, file.Protection);
        Assert.Equal(60U, file.Data);
        Assert.Equal(12345U, file.Size);
        Assert.Equal("File.txt", file.Name);
        Assert.Equal("Comment", file.Comment);
    }

    [Fact]
    public void WhenBuildingAndParsingRootObjectContainerThenRootInfoIsEqual()
    {
        // arrange
        var objectContainer = new ObjectContainer
        {
            OwnBlock = 3,
            RootInfo = new RootInfo
            {
                DeletedBlocks = 1,
                DeletedFiles = 2,
                FreeBlocks = 3,
                DateCreated = new DateTime(2022, 1, 1)
            }
        };
        objectContainer.Objects.Add(new SfsObject { ObjectNode = 1, Bits = Constants.OTypeDir, Name = "Workbench" });

        // act
        var blockBytes = ObjectContainerBuilder.Build(objectContainer, BlockSize);
        var parsed = ObjectContainerParser.Parse(blockBytes, true);

        // assert
        Assert.Single(parsed.Objects);
        Assert.Equal("Workbench", parsed.Objects[0].Name);
        Assert.Equal(1U, parsed.RootInfo.DeletedBlocks);
        Assert.Equal(2U, parsed.RootInfo.DeletedFiles);
        Assert.Equal(3U, parsed.RootInfo.FreeBlocks);
        Assert.Equal(new DateTime(2022, 1, 1), parsed.RootInfo.DateCreated);
    }

    [Fact]
    public void WhenBuildingAndParsingNodeContainersThenNodesAreEqual()
    {
        // arrange
        var leaf = NodeContainer.CreateLeaf(BlockSize, 1);
        leaf.OwnBlock = 7;
        leaf.ObjectNodes[0].Data = 3;
        leaf.ObjectNodes[1].Data = 8;
        leaf.ObjectNodes[1].Next = 9;
        leaf.ObjectNodes[1].Hash16 = 0x47ea;
        var index = NodeContainer.CreateIndex(BlockSize, 1, 49);
        index.OwnBlock = 8;
        index.Pointers[0] = (100U << 4) | 1;

        // act
        var parsedLeaf = NodeContainerParser.Parse(NodeContainerBuilder.Build(leaf, BlockSize));
        var parsedIndex = NodeContainerParser.Parse(NodeContainerBuilder.Build(index, BlockSize));

        // assert
        Assert.Equal(49, NodeContainer.LeafCapacity(BlockSize));
        Assert.Equal(123, NodeContainer.IndexCapacity(BlockSize));
        Assert.True(parsedLeaf.IsLeaf);
        Assert.Equal(49, parsedLeaf.ObjectNodes.Length);
        Assert.Equal(3U, parsedLeaf.ObjectNodes[0].Data);
        Assert.Equal(8U, parsedLeaf.ObjectNodes[1].Data);
        Assert.Equal(9U, parsedLeaf.ObjectNodes[1].Next);
        Assert.Equal(0x47ea, parsedLeaf.ObjectNodes[1].Hash16);
        Assert.False(parsedIndex.IsLeaf);
        Assert.Equal(49U, parsedIndex.Nodes);
        Assert.Equal((100U << 4) | 1, parsedIndex.Pointers[0]);
    }

    [Fact]
    public void WhenBuildingAndParsingBNodeContainersThenNodesAreEqual()
    {
        // arrange
        var leaf = BNodeContainer.CreateLeaf();
        leaf.OwnBlock = 6;
        leaf.Nodes.Add(new ExtentBNode { Key = 100, Next = 200, Prev = 0x80000007, Blocks = 10 });
        leaf.Nodes.Add(new ExtentBNode { Key = 200, Next = 0, Prev = 100, Blocks = 65535 });
        var internalContainer = BNodeContainer.CreateInternal();
        internalContainer.OwnBlock = 6;
        internalContainer.Nodes.Add(new BNode { Key = 0, Data = 10 });
        internalContainer.Nodes.Add(new BNode { Key = 500, Data = 11 });

        // act
        var parsedLeaf = BNodeContainerParser.Parse(BNodeContainerBuilder.Build(leaf, BlockSize));
        var parsedInternal = BNodeContainerParser.Parse(BNodeContainerBuilder.Build(internalContainer, BlockSize));

        // assert
        Assert.Equal(35, leaf.Capacity(BlockSize));
        Assert.Equal(62, internalContainer.Capacity(BlockSize));
        Assert.True(parsedLeaf.IsLeaf);
        Assert.Equal(14, parsedLeaf.NodeSize);
        Assert.Equal(2, parsedLeaf.Nodes.Count);
        var extent1 = Assert.IsType<ExtentBNode>(parsedLeaf.Nodes[0]);
        Assert.Equal(100U, extent1.Key);
        Assert.Equal(200U, extent1.Next);
        Assert.Equal(0x80000007U, extent1.Prev);
        Assert.Equal(10, extent1.Blocks);
        var extent2 = Assert.IsType<ExtentBNode>(parsedLeaf.Nodes[1]);
        Assert.Equal(65535, extent2.Blocks);
        Assert.False(parsedInternal.IsLeaf);
        Assert.Equal(8, parsedInternal.NodeSize);
        Assert.Equal(500U, parsedInternal.Nodes[1].Key);
        Assert.Equal(11U, parsedInternal.Nodes[1].Data);
    }

    [Fact]
    public void WhenBuildingAndParsingOtherBlocksThenBlocksAreEqual()
    {
        // arrange
        var adminSpaceContainer = new AdminSpaceContainer(BlockSize) { OwnBlock = 2, Next = 3, Previous = 4 };
        adminSpaceContainer.AdminSpaces[0].Space = 2;
        adminSpaceContainer.AdminSpaces[0].Bits = 0xfe000000;
        var bitmapBlock = new BitmapBlock(BlockSize) { OwnBlock = 34 };
        bitmapBlock.Map[0] = 0x0000ffff;
        var hashTable = new HashTableBlock(BlockSize) { OwnBlock = 4, Parent = 1 };
        hashTable.HashEntries[58] = 2;
        var softLink = new SoftLinkBlock { OwnBlock = 9, Parent = 10, Path = "dir/file" };

        // act
        var parsedAdminSpaceContainer = AdminSpaceContainerParser.Parse(
            AdminSpaceContainerBuilder.Build(adminSpaceContainer, BlockSize));
        var parsedBitmapBlock = BitmapBlockParser.Parse(BitmapBlockBuilder.Build(bitmapBlock, BlockSize));
        var parsedHashTable = HashTableBlockParser.Parse(HashTableBlockBuilder.Build(hashTable, BlockSize));
        var parsedSoftLink = SoftLinkBlockParser.Parse(SoftLinkBlockBuilder.Build(softLink, BlockSize));

        // assert
        Assert.Equal(61, parsedAdminSpaceContainer.AdminSpaces.Length);
        Assert.Equal(3U, parsedAdminSpaceContainer.Next);
        Assert.Equal(4U, parsedAdminSpaceContainer.Previous);
        Assert.Equal(32, parsedAdminSpaceContainer.Bits);
        Assert.Equal(2U, parsedAdminSpaceContainer.AdminSpaces[0].Space);
        Assert.Equal(0xfe000000U, parsedAdminSpaceContainer.AdminSpaces[0].Bits);
        Assert.Equal(125, parsedBitmapBlock.Map.Length);
        Assert.Equal(4000, BitmapBlock.BlocksInBitmap(BlockSize));
        Assert.Equal(0x0000ffffU, parsedBitmapBlock.Map[0]);
        Assert.Equal(124, parsedHashTable.HashEntries.Length);
        Assert.Equal(1U, parsedHashTable.Parent);
        Assert.Equal(2U, parsedHashTable.HashEntries[58]);
        Assert.Equal(10U, parsedSoftLink.Parent);
        Assert.Equal("dir/file", parsedSoftLink.Path);
    }
}
