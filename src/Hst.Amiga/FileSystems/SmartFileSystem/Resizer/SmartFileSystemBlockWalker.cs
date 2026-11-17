namespace Hst.Amiga.FileSystems.SmartFileSystem.Resizer
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Blocks;
    using Core.Converters;
    using RigidDiskBlocks.Partitioning;

    /// <summary>
    /// Reads structures of a Smart File System volume: root block, admin space containers, object node tree,
    /// extent B-tree and objects in directory tree. Volume must be consistent, e.g. checked by doctor.
    /// </summary>
    internal static class SmartFileSystemBlockWalker
    {
        private const int OffsetOwnBlock = 8;

        public static async Task<SmartFileSystemLayout> Load(PartitionBlockIo io, uint totalBlocks,
            CancellationToken cancellationToken)
        {
            var blockSize = io.BlockSize;
            var root = await io.Read(0);
            if (GetUInt32(root, 0) != Constants.RootBlockId || !ChecksumHelper.IsValid(root))
            {
                throw new PartitionOperationException("Smart File System root block is invalid, run doctor to repair");
            }

            var layout = new SmartFileSystemLayout
            {
                BlockSize = blockSize,
                ShiftsBlock32 = SmartFileSystemHelper.Log2(blockSize) - Constants.BlckFAccuracy,
                RootBlock = root,
                TotalBlocks = GetUInt32(root, 0x30),
                BitmapBase = GetUInt32(root, 0x60),
                AdminSpaceContainer = GetUInt32(root, 0x64),
                RootObjectContainer = GetUInt32(root, 0x68),
                ExtentBNodeRoot = GetUInt32(root, 0x6c),
                ObjectNodeRoot = GetUInt32(root, 0x70)
            };

            if (layout.TotalBlocks != totalBlocks || GetUInt32(root, 0x34) != blockSize)
            {
                throw new PartitionOperationException(
                    $"Smart File System root block has {layout.TotalBlocks} blocks of {GetUInt32(root, 0x34)} bytes, partition has {totalBlocks} blocks of {blockSize} bytes, run doctor to repair");
            }

            layout.References.Add(new BlockReference(0, 0x64, layout.AdminSpaceContainer, BlockReferenceKind.Block));
            layout.References.Add(new BlockReference(0, 0x68, layout.RootObjectContainer, BlockReferenceKind.Block));
            layout.References.Add(new BlockReference(0, 0x6c, layout.ExtentBNodeRoot, BlockReferenceKind.Block));
            layout.References.Add(new BlockReference(0, 0x70, layout.ObjectNodeRoot, BlockReferenceKind.Block));

            var transaction = await ReadBlock(io, layout, layout.TransactionBlock);
            if (GetUInt32(transaction, 0) != Constants.TransactionOkId)
            {
                throw new PartitionOperationException(
                    "Smart File System has an unfinished transaction, mount volume or run doctor to complete it");
            }

            layout.AdminBlocks.Add(layout.TransactionBlock);

            await ReadAdminSpaceContainers(io, layout, cancellationToken);
            await ReadNodeTree(io, layout, cancellationToken);
            await ReadExtentTree(io, layout, cancellationToken);
            await ReadObjects(io, layout, cancellationToken);

            return layout;
        }

        private static async Task ReadAdminSpaceContainers(PartitionBlockIo io, SmartFileSystemLayout layout,
            CancellationToken cancellationToken)
        {
            var block = layout.AdminSpaceContainer;
            var entries = (layout.BlockSize - Constants.AdminSpaceContainerHeaderSize) / Constants.AdminSpaceSize;

            while (block != 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var data = await ReadAdminBlock(io, layout, block, Constants.AdminSpaceContainerId);
                layout.AdminContainers.Add(block);
                AddReference(layout, block, 0xc, GetUInt32(data, 0xc));
                AddReference(layout, block, 0x10, GetUInt32(data, 0x10));

                for (var i = 0; i < entries; i++)
                {
                    var space = GetUInt32(data,
                        Constants.AdminSpaceContainerHeaderSize + i * Constants.AdminSpaceSize);
                    if (space != 0)
                    {
                        layout.AdminSpaces.Add(new AdminSpaceEntry { Container = block, Index = i, Space = space });
                    }
                }

                block = GetUInt32(data, 0xc);
            }
        }

        private static async Task ReadNodeTree(PartitionBlockIo io, SmartFileSystemLayout layout,
            CancellationToken cancellationToken)
        {
            var blocks = new Stack<uint>();
            blocks.Push(layout.ObjectNodeRoot);

            while (blocks.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var block = blocks.Pop();
                var data = await ReadAdminBlock(io, layout, block, Constants.NodeContainerId);
                var nodes = GetUInt32(data, 0x10);

                if (nodes == 1)
                {
                    var count = (layout.BlockSize - Constants.NodeContainerHeaderSize) / Constants.ObjectNodeSize;
                    for (var i = 0; i < count; i++)
                    {
                        var offset = Constants.NodeContainerHeaderSize + i * Constants.ObjectNodeSize;
                        var objectContainer = GetUInt32(data, offset);
                        if (objectContainer != Constants.ReservedNode)
                        {
                            AddReference(layout, block, offset, objectContainer);
                        }
                    }

                    continue;
                }

                var pointers = (layout.BlockSize - Constants.NodeContainerHeaderSize) / 4;
                for (var i = 0; i < pointers; i++)
                {
                    var offset = Constants.NodeContainerHeaderSize + i * 4;
                    var pointer = GetUInt32(data, offset);
                    if (pointer == 0)
                    {
                        continue;
                    }

                    var child = pointer >> layout.ShiftsBlock32;
                    layout.References.Add(new BlockReference(block, offset, child, BlockReferenceKind.NodeIndex));
                    blocks.Push(child);
                }
            }
        }

        private static async Task ReadExtentTree(PartitionBlockIo io, SmartFileSystemLayout layout,
            CancellationToken cancellationToken)
        {
            var blocks = new Stack<uint>();
            blocks.Push(layout.ExtentBNodeRoot);

            while (blocks.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var block = blocks.Pop();
                var data = await ReadAdminBlock(io, layout, block, Constants.BNodeContainerId);
                layout.BNodeContainers.Add(block);
                var container = BNodeContainerParser.Parse(data);

                for (var i = 0; i < container.Nodes.Count; i++)
                {
                    var node = container.Nodes[i];
                    if (container.IsLeaf)
                    {
                        if (layout.Extents.ContainsKey(node.Key))
                        {
                            throw new PartitionOperationException(
                                $"Extent {node.Key} exists more than once in extent B-tree, run doctor to repair");
                        }

                        layout.Extents[node.Key] = (ExtentBNode)node;
                        continue;
                    }

                    var offset = Constants.BNodeContainerHeaderSize + i * Constants.BNodeSize + 4;
                    AddReference(layout, block, offset, node.Data);
                    blocks.Push(node.Data);
                }
            }
        }

        private static async Task ReadObjects(PartitionBlockIo io, SmartFileSystemLayout layout,
            CancellationToken cancellationToken)
        {
            var visited = new HashSet<uint>();
            var containers = new Queue<uint>();
            containers.Enqueue(layout.RootObjectContainer);

            while (containers.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var block = containers.Dequeue();
                if (!visited.Add(block))
                {
                    continue;
                }

                var data = await ReadAdminBlock(io, layout, block, Constants.ObjectContainerId);
                var next = GetUInt32(data, 0x10);
                AddReference(layout, block, 0x10, next);
                AddReference(layout, block, 0x14, GetUInt32(data, 0x14));
                if (next != 0)
                {
                    containers.Enqueue(next);
                }

                var hasRootInfo = block == layout.RootObjectContainer;
                var endOffset = data.Length - (hasRootInfo ? Constants.RootInfoSize : 0);
                var objectsEndOffset = data.Length - 26 - 2;
                var offset = Constants.ObjectContainerHeaderSize;
                while (offset < objectsEndOffset && offset + Constants.ObjectHeaderSize < endOffset &&
                       data[offset + Constants.ObjectHeaderSize] != 0)
                {
                    var sfsObject = ObjectContainerParser.ParseObject(data, offset, out var size);

                    if (sfsObject.IsDir)
                    {
                        if (sfsObject.HashTable != 0)
                        {
                            AddReference(layout, block, offset + 12, sfsObject.HashTable);
                            await ReadAdminBlock(io, layout, sfsObject.HashTable, Constants.HashTableId);
                        }

                        if (sfsObject.FirstDirBlock != 0)
                        {
                            AddReference(layout, block, offset + 16, sfsObject.FirstDirBlock);
                            containers.Enqueue(sfsObject.FirstDirBlock);
                        }
                    }
                    else if (sfsObject.IsSoftLink)
                    {
                        if (sfsObject.Data != 0)
                        {
                            AddReference(layout, block, offset + 12, sfsObject.Data);
                            var softLink = await ReadAdminBlock(io, layout, sfsObject.Data, Constants.SoftLinkId);
                            AddReference(layout, sfsObject.Data, 0x10, GetUInt32(softLink, 0x10));
                            AddReference(layout, sfsObject.Data, 0x14, GetUInt32(softLink, 0x14));
                        }
                    }
                    else if (sfsObject.IsFile && sfsObject.Data != 0)
                    {
                        layout.Files.Add(new FileObject
                        {
                            Container = block,
                            Offset = offset + 12,
                            FirstExtent = sfsObject.Data
                        });
                    }

                    offset += size;
                }
            }
        }

        /// <summary>
        /// Read administration block, verify id, checksum and own block and add it to administration blocks with
        /// reference to own block.
        /// </summary>
        private static async Task<byte[]> ReadAdminBlock(PartitionBlockIo io, SmartFileSystemLayout layout,
            uint block, uint id)
        {
            var data = await ReadBlock(io, layout, block);
            if (GetUInt32(data, 0) != id)
            {
                throw new PartitionOperationException(
                    $"Block {block} has id '{BlockHelper.FormatId(GetUInt32(data, 0))}', expected '{BlockHelper.FormatId(id)}', run doctor to repair");
            }

            if (!layout.AdminBlocks.Add(block))
            {
                throw new PartitionOperationException(
                    $"Block {block} is used more than once, run doctor to repair");
            }

            layout.References.Add(new BlockReference(block, OffsetOwnBlock, block, BlockReferenceKind.Block));
            return data;
        }

        private static async Task<byte[]> ReadBlock(PartitionBlockIo io, SmartFileSystemLayout layout, uint block)
        {
            if (block == 0 || block >= layout.TotalBlocks)
            {
                throw new PartitionOperationException(
                    $"Block pointer {block} is outside partition, run doctor to repair");
            }

            var data = await io.Read(block);
            if (!ChecksumHelper.IsValid(data) || GetUInt32(data, OffsetOwnBlock) != block)
            {
                throw new PartitionOperationException($"Block {block} is invalid, run doctor to repair");
            }

            return data;
        }

        private static void AddReference(SmartFileSystemLayout layout, uint holder, int offset, uint target)
        {
            if (target != 0)
            {
                layout.References.Add(new BlockReference(holder, offset, target, BlockReferenceKind.Block));
            }
        }

        private static uint GetUInt32(byte[] data, int offset) =>
            BigEndianConverter.ConvertBytesToUInt32(data, offset);
    }
}
