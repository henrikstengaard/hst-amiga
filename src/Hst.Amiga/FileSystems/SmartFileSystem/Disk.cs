namespace Hst.Amiga.FileSystems.SmartFileSystem
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;
    using Exceptions;

    /// <summary>
    /// Reading and writing of smart file system blocks.
    /// </summary>
    public static class Disk
    {
        public static async Task<byte[]> ReadBlock(Volume volume, uint block)
        {
            if (block >= volume.TotalBlocks)
            {
                throw new IOException($"Block {block} is outside partition with {volume.TotalBlocks} blocks");
            }

            if (volume.Overlay.TryGetValue(block, out var overlayBytes))
            {
                var copy = new byte[overlayBytes.Length];
                Array.Copy(overlayBytes, copy, copy.Length);
                return copy;
            }

            volume.Stream.Seek(volume.PartitionOffset + (long)block * volume.BlockSize, SeekOrigin.Begin);
            var blockBytes = new byte[volume.BlockSize];
            var offset = 0;
            while (offset < blockBytes.Length)
            {
                var bytesRead = await volume.Stream.ReadAsync(blockBytes, offset, blockBytes.Length - offset);
                if (bytesRead <= 0)
                {
                    break;
                }

                offset += bytesRead;
            }

            return blockBytes;
        }

        public static async Task WriteBlock(Volume volume, uint block, byte[] blockBytes)
        {
            if (volume.IsReadOnly)
            {
                throw new FileSystemException("Volume is mounted read only");
            }

            if (block >= volume.TotalBlocks)
            {
                throw new IOException($"Block {block} is outside partition with {volume.TotalBlocks} blocks");
            }

            if (blockBytes.Length != volume.BlockSize)
            {
                throw new ArgumentException($"Block bytes must be {volume.BlockSize} bytes", nameof(blockBytes));
            }

            volume.Overlay.Remove(block);
            volume.Stream.Seek(volume.PartitionOffset + (long)block * volume.BlockSize, SeekOrigin.Begin);
            await volume.Stream.WriteAsync(blockBytes, 0, blockBytes.Length);
        }

        /// <summary>
        /// Read block and verify id, checksum and own block, readcachebuffercheck() in smart file system source.
        /// </summary>
        public static async Task<byte[]> ReadCheckedBlock(Volume volume, uint block, uint id)
        {
            var blockBytes = await ReadBlock(volume, block);
            CheckBlock(blockBytes, block, id);
            return blockBytes;
        }

        public static void CheckBlock(byte[] blockBytes, uint block, uint id)
        {
            var header = new RawBlock();
            BlockHelper.ReadHeader(blockBytes, header);

            if (header.Id != id)
            {
                throw new IOException(
                    $"Invalid block id '{BlockHelper.FormatId(header.Id)}' at block {block}, expected block id '{BlockHelper.FormatId(id)}'");
            }

            if (!ChecksumHelper.IsValid(blockBytes))
            {
                throw new IOException($"Invalid checksum for block '{BlockHelper.FormatId(id)}' at block {block}");
            }

            if (header.OwnBlock != block)
            {
                throw new IOException(
                    $"Invalid own block {header.OwnBlock} for block '{BlockHelper.FormatId(id)}' at block {block}");
            }
        }

        public static async Task<ObjectContainer> ReadObjectContainer(Volume volume, uint block)
        {
            return ObjectContainerParser.Parse(await ReadCheckedBlock(volume, block, Constants.ObjectContainerId),
                block == volume.RootBlock.RootObjectContainer);
        }

        public static async Task WriteObjectContainer(Volume volume, ObjectContainer objectContainer)
        {
            if (volume.RootBlock != null && objectContainer.OwnBlock == volume.RootBlock.RootObjectContainer &&
                objectContainer.RootInfo == null)
            {
                throw new IOException("Root object container must contain root info");
            }

            await WriteBlock(volume, objectContainer.OwnBlock,
                ObjectContainerBuilder.Build(objectContainer, volume.BlockSize));
        }

        public static async Task<HashTableBlock> ReadHashTable(Volume volume, uint block)
        {
            return HashTableBlockParser.Parse(await ReadCheckedBlock(volume, block, Constants.HashTableId));
        }

        public static async Task WriteHashTable(Volume volume, HashTableBlock hashTableBlock)
        {
            await WriteBlock(volume, hashTableBlock.OwnBlock, HashTableBlockBuilder.Build(hashTableBlock, volume.BlockSize));
        }

        public static async Task<SoftLinkBlock> ReadSoftLink(Volume volume, uint block)
        {
            return SoftLinkBlockParser.Parse(await ReadCheckedBlock(volume, block, Constants.SoftLinkId));
        }

        public static async Task WriteSoftLink(Volume volume, SoftLinkBlock softLinkBlock)
        {
            await WriteBlock(volume, softLinkBlock.OwnBlock, SoftLinkBlockBuilder.Build(softLinkBlock, volume.BlockSize));
        }

        public static async Task<NodeContainer> ReadNodeContainer(Volume volume, uint block)
        {
            return NodeContainerParser.Parse(await ReadCheckedBlock(volume, block, Constants.NodeContainerId));
        }

        public static async Task WriteNodeContainer(Volume volume, NodeContainer nodeContainer)
        {
            await WriteBlock(volume, nodeContainer.OwnBlock, NodeContainerBuilder.Build(nodeContainer, volume.BlockSize));
        }

        public static async Task<BNodeContainer> ReadBNodeContainer(Volume volume, uint block)
        {
            return BNodeContainerParser.Parse(await ReadCheckedBlock(volume, block, Constants.BNodeContainerId));
        }

        public static async Task WriteBNodeContainer(Volume volume, BNodeContainer bNodeContainer)
        {
            await WriteBlock(volume, bNodeContainer.OwnBlock, BNodeContainerBuilder.Build(bNodeContainer, volume.BlockSize));
        }

        public static async Task<AdminSpaceContainer> ReadAdminSpaceContainer(Volume volume, uint block)
        {
            return AdminSpaceContainerParser.Parse(await ReadCheckedBlock(volume, block, Constants.AdminSpaceContainerId));
        }

        public static async Task WriteAdminSpaceContainer(Volume volume, AdminSpaceContainer adminSpaceContainer)
        {
            await WriteBlock(volume, adminSpaceContainer.OwnBlock,
                AdminSpaceContainerBuilder.Build(adminSpaceContainer, volume.BlockSize));
        }

        public static async Task<BitmapBlock> ReadBitmapBlock(Volume volume, uint block)
        {
            return BitmapBlockParser.Parse(await ReadCheckedBlock(volume, block, Constants.BitmapId));
        }

        public static async Task WriteBitmapBlock(Volume volume, BitmapBlock bitmapBlock)
        {
            await WriteBlock(volume, bitmapBlock.OwnBlock, BitmapBlockBuilder.Build(bitmapBlock, volume.BlockSize));
        }

        public static async Task<RootInfo> ReadRootInfo(Volume volume)
        {
            return (await ReadObjectContainer(volume, volume.RootBlock.RootObjectContainer)).RootInfo;
        }

        public static async Task UpdateRootInfo(Volume volume, Action<RootInfo> update)
        {
            var rootContainer = await ReadObjectContainer(volume, volume.RootBlock.RootObjectContainer);
            update(rootContainer.RootInfo);
            await WriteObjectContainer(volume, rootContainer);
        }

        private class RawBlock : Block
        {
            public RawBlock() : base(0)
            {
            }
        }
    }
}
