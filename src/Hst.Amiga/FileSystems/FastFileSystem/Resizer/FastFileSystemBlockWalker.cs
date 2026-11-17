namespace Hst.Amiga.FileSystems.FastFileSystem.Resizer
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Core.Converters;
    using RigidDiskBlocks.Partitioning;

    /// <summary>
    /// Walks directory tree of a Fast File System volume and reports blocks used and block pointers. A block
    /// pointer is reported as holder block, offset of pointer in holder block and target block. Pointers to the
    /// block itself (header key) are reported as well, so all fields to update when a block is relocated are
    /// reported. Root block, bitmap blocks and bitmap extension blocks are not reported as used. Volume must be
    /// consistent, e.g. checked by doctor.
    /// </summary>
    internal class FastFileSystemBlockWalker
    {
        private const int OffsetHeaderKey = 0x4;
        private const int OffsetHighSeq = 0x8;
        private const int OffsetDataSize = 0xc;
        private const int OffsetFirstData = 0x10;
        private const int OffsetNextData = 0x10;
        private const int OffsetTable = 0x18;

        private readonly PartitionBlockIo io;
        private readonly uint rootBlock;
        private readonly uint blocks;
        private readonly int blockSize;
        private readonly int tableSize;
        private readonly bool useOfs;
        private readonly bool useDirCache;
        private readonly bool useLnfs;

        public FastFileSystemBlockWalker(PartitionBlockIo io, uint dosType, uint rootBlock, uint blocks)
        {
            this.io = io;
            this.rootBlock = rootBlock;
            this.blocks = blocks;
            blockSize = io.BlockSize;
            tableSize = (int)FastFileSystemHelper.CalculateHashtableSize((uint)blockSize);
            var mode = (int)(dosType & 0xff);
            useOfs = Macro.UseOfs(mode);
            useDirCache = Macro.UseDirCache(mode);
            useLnfs = Macro.UseLnfs(mode);
        }

        public bool UseDirCache => useDirCache;

        private int OffsetHashChain => blockSize - 0x10;
        private int OffsetParent => blockSize - 0x0c;
        private int OffsetExtension => blockSize - 0x08;
        private int OffsetSecType => blockSize - 0x04;
        private int OffsetRealEntry => blockSize - 0x2c;
        private int OffsetNextLink => blockSize - 0x28;
        private int OffsetLnfsCommentBlock => blockSize - 0x48;

        private static int TableOffset(int index) => OffsetTable + index * 4;
        private int DataPointerOffset(int index) => TableOffset(tableSize - 1 - index);

        /// <summary>
        /// Walk volume.
        /// </summary>
        /// <param name="used">Called for each block used by entries, file data, dir caches and comments.</param>
        /// <param name="reference">Called for each block pointer with holder block, offset and target block.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task Walk(Action<uint> used, Action<uint, int, uint> reference,
            CancellationToken cancellationToken)
        {
            used = used ?? (_ => { });
            reference = reference ?? ((_, __, ___) => { });

            var visited = new HashSet<uint>();
            var directories = new Stack<(uint Block, byte[] Data)>();
            directories.Push((rootBlock, await io.Read(rootBlock)));

            while (directories.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (directory, directoryData) = directories.Pop();

                for (var i = 0; i < tableSize; i++)
                {
                    var holder = directory;
                    var offset = TableOffset(i);
                    var entry = GetUInt32(directoryData, offset);

                    while (entry != 0)
                    {
                        Validate(entry, holder);
                        if (!visited.Add(entry))
                        {
                            throw new PartitionOperationException(
                                $"Entry block {entry} is linked more than once, run doctor to repair");
                        }

                        reference(holder, offset, entry);

                        var entryData = await io.Read(entry);
                        await VisitEntry(entry, entryData, directory, directories, used, reference);

                        holder = entry;
                        offset = OffsetHashChain;
                        entry = GetUInt32(entryData, OffsetHashChain);
                    }
                }

                if (useDirCache)
                {
                    await VisitDirCache(directory, directoryData, used, reference);
                }
            }
        }

        private async Task VisitEntry(uint block, byte[] data, uint parent,
            Stack<(uint Block, byte[] Data)> directories, Action<uint> used, Action<uint, int, uint> reference)
        {
            used(block);
            reference(block, OffsetHeaderKey, block);
            reference(block, OffsetParent, parent);

            var secType = GetInt32(data, OffsetSecType);

            if (useLnfs)
            {
                var commentBlock = GetUInt32(data, OffsetLnfsCommentBlock);
                if (commentBlock != 0)
                {
                    Validate(commentBlock, block);
                    used(commentBlock);
                    reference(block, OffsetLnfsCommentBlock, commentBlock);
                    reference(commentBlock, 0x4, commentBlock);
                    reference(commentBlock, 0x8, block);
                }
            }

            if (secType != Constants.ST_LSOFT)
            {
                var nextLink = GetUInt32(data, OffsetNextLink);
                if (nextLink != 0)
                {
                    Validate(nextLink, block);
                    reference(block, OffsetNextLink, nextLink);
                }
            }

            switch (secType)
            {
                case Constants.ST_DIR:
                    directories.Push((block, data));
                    break;
                case Constants.ST_FILE:
                    await VisitFile(block, data, used, reference);
                    break;
                case Constants.ST_LFILE:
                case Constants.ST_LDIR:
                    var realEntry = GetUInt32(data, OffsetRealEntry);
                    Validate(realEntry, block);
                    reference(block, OffsetRealEntry, realEntry);
                    break;
            }
        }

        private async Task VisitFile(uint block, byte[] header, Action<uint> used,
            Action<uint, int, uint> reference)
        {
            var firstData = GetUInt32(header, OffsetFirstData);
            if (firstData != 0)
            {
                Validate(firstData, block);
                reference(block, OffsetFirstData, firstData);
            }

            var container = header;
            var containerBlock = block;
            var previousData = 0U;
            var extensions = 0;

            while (true)
            {
                var highSeq = (int)Math.Min(GetUInt32(container, OffsetHighSeq), (uint)tableSize);
                for (var i = 0; i < highSeq; i++)
                {
                    var dataBlock = GetUInt32(container, DataPointerOffset(i));
                    Validate(dataBlock, containerBlock);
                    used(dataBlock);
                    reference(containerBlock, DataPointerOffset(i), dataBlock);

                    if (useOfs)
                    {
                        reference(dataBlock, OffsetHeaderKey, block);
                        if (previousData != 0)
                        {
                            reference(previousData, OffsetNextData, dataBlock);
                        }

                        previousData = dataBlock;
                    }
                }

                var extension = GetUInt32(container, OffsetExtension);
                if (extension == 0)
                {
                    break;
                }

                Validate(extension, containerBlock);
                if (++extensions > blocks)
                {
                    throw new PartitionOperationException(
                        $"File header block {block} has a loop in file extension blocks, run doctor to repair");
                }

                reference(containerBlock, OffsetExtension, extension);
                used(extension);
                reference(extension, OffsetHeaderKey, extension);
                reference(extension, OffsetParent, block);

                container = await io.Read(extension);
                containerBlock = extension;
            }
        }

        private async Task VisitDirCache(uint directory, byte[] directoryData, Action<uint> used,
            Action<uint, int, uint> reference)
        {
            var holder = directory;
            var offset = OffsetExtension;
            var dirCacheBlock = GetUInt32(directoryData, OffsetExtension);
            var count = 0;

            while (dirCacheBlock != 0)
            {
                Validate(dirCacheBlock, holder);
                if (++count > blocks)
                {
                    throw new PartitionOperationException(
                        $"Directory block {directory} has a loop in dir cache blocks, run doctor to repair");
                }

                reference(holder, offset, dirCacheBlock);
                used(dirCacheBlock);
                reference(dirCacheBlock, OffsetHeaderKey, dirCacheBlock);
                reference(dirCacheBlock, 0x8, directory);

                var data = await io.Read(dirCacheBlock);
                var records = GetUInt32(data, OffsetDataSize);
                var ptr = OffsetTable;
                for (var i = 0; i < records && ptr + 25 <= blockSize; i++)
                {
                    reference(dirCacheBlock, ptr, GetUInt32(data, ptr));

                    var nameLength = data[ptr + 23];
                    var commentLength = ptr + 24 + nameLength < blockSize ? data[ptr + 24 + nameLength] : 0;
                    var length = 25 + nameLength + commentLength;
                    ptr += length % 2 == 0 ? length : length + 1;
                }

                holder = dirCacheBlock;
                offset = OffsetNextData;
                dirCacheBlock = GetUInt32(data, OffsetNextData);
            }
        }

        private void Validate(uint block, uint holder)
        {
            if (block >= blocks || block == rootBlock && holder != rootBlock)
            {
                throw new PartitionOperationException(
                    $"Block {holder} has invalid block pointer {block}, run doctor to repair");
            }
        }

        private static uint GetUInt32(byte[] data, int offset) =>
            BigEndianConverter.ConvertBytesToUInt32(data, offset);

        private static int GetInt32(byte[] data, int offset) =>
            BigEndianConverter.ConvertBytesToInt32(data, offset);
    }
}
