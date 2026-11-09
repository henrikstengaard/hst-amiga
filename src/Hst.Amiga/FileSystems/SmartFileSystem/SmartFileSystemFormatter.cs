namespace Hst.Amiga.FileSystems.SmartFileSystem
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;
    using RigidDiskBlocks;
    using BlockHelper = Blocks.BlockHelper;

    /// <summary>
    /// Formatter for smart file system, ACTION_FORMAT in fs/filesystemmain.c in smart file system source.
    /// </summary>
    public static class SmartFileSystemFormatter
    {
        /// <summary>
        /// Format partition with smart file system.
        /// </summary>
        /// <param name="stream">Stream to format.</param>
        /// <param name="partitionBlock">Partition block for partition to format.</param>
        /// <param name="volumeName">Name of volume.</param>
        /// <param name="caseSensitive">Create case sensitive volume.</param>
        /// <param name="recycled">Create recycled directory for deleted files.</param>
        /// <param name="showRecycled">Show recycled directory.</param>
        public static async Task FormatPartition(Stream stream, PartitionBlock partitionBlock, string volumeName,
            bool caseSensitive = false, bool recycled = true, bool showRecycled = false)
        {
            SmartFileSystemHelper.ValidateDosType(partitionBlock.DosType);
            var geometry = SmartFileSystemHelper.GetGeometry(partitionBlock);
            await Format(stream, geometry.FirstByte, geometry.LastByte, geometry.BlockSize, partitionBlock.Reserved,
                partitionBlock.PreAlloc, volumeName, caseSensitive, recycled, showRecycled);
        }

        /// <summary>
        /// Format partition with smart file system.
        /// </summary>
        /// <param name="stream">Stream to format.</param>
        /// <param name="firstByte">Byte offset of start of partition in stream.</param>
        /// <param name="lastByte">Byte offset of end of partition in stream (exclusive).</param>
        /// <param name="blockSize">File system block size.</param>
        /// <param name="reserved">Number of reserved blocks at start of partition.</param>
        /// <param name="preAlloc">Number of reserved blocks at end of partition.</param>
        /// <param name="volumeName">Name of volume.</param>
        /// <param name="caseSensitive">Create case sensitive volume.</param>
        /// <param name="recycled">Create recycled directory for deleted files.</param>
        /// <param name="showRecycled">Show recycled directory.</param>
        public static async Task Format(Stream stream, long firstByte, long lastByte, int blockSize, uint reserved,
            uint preAlloc, string volumeName, bool caseSensitive = false, bool recycled = true,
            bool showRecycled = false)
        {
            if (!stream.CanWrite)
            {
                throw new IOException("Failed to format partition: Stream is not writable");
            }

            BlockHelper.ValidateBlockSize(blockSize);

            if (string.IsNullOrEmpty(volumeName) || volumeName.IndexOf('/') >= 0 || volumeName.IndexOf(':') >= 0)
            {
                throw new ArgumentException("Invalid volume name", nameof(volumeName));
            }

            if (volumeName.Length > Constants.MaxVolumeNameLength)
            {
                volumeName = volumeName.Substring(0, Constants.MaxVolumeNameLength);
            }

            var totalBlocksLong = (lastByte - firstByte) / blockSize;
            if (totalBlocksLong > uint.MaxValue)
            {
                throw new ArgumentException("Partition is too large for smart file system");
            }

            var totalBlocks = (uint)totalBlocksLong;

            // node index containers store block numbers shifted left by log2(block size) - 5 in 32 bits,
            // which limits partition size to 128 GB
            if (((ulong)totalBlocks << (SmartFileSystemHelper.Log2(blockSize) - Constants.BlckFAccuracy)) > uint.MaxValue)
            {
                throw new ArgumentException("Partition is too large for smart file system, max partition size is 128 GB");
            }
            var reservedStart = Math.Max(reserved, 1);
            var reservedEnd = Math.Max(preAlloc, 1);
            var bitmapBlocks = (uint)Bitmap.CalculateBitmapBlocks(totalBlocks, blockSize);
            var blocksInBitmap = (uint)BitmapBlock.BlocksInBitmap(blockSize);

            var startFree = reservedStart + Constants.BlocksAdmin + bitmapBlocks;
            if (totalBlocks <= startFree + reservedEnd + Constants.BlocksAdmin)
            {
                throw new ArgumentException("Partition is too small for smart file system");
            }

            var volume = new Volume
            {
                Stream = stream,
                PartitionOffset = firstByte,
                BlockSize = blockSize,
                TotalBlocks = totalBlocks
            };

            var date = DateTime.Now;

            var blockAdminSpace = reservedStart;
            var blockRoot = reservedStart + 1;
            var blockHashTable = blockRoot + 1;
            var blockTransaction = blockRoot + 2;
            var blockExtentBNodeRoot = blockRoot + 3;
            var blockObjectNodeRoot = blockRoot + 4;
            var blockRecycled = blockRoot + 5;
            var blockBitmapBase = blockAdminSpace + Constants.BlocksAdmin;

            var recycledHash = HashHelper.Hash(Constants.RecycledName, caseSensitive);

            // admin space container
            var adminSpaceContainer = new AdminSpaceContainer(blockSize)
            {
                OwnBlock = blockAdminSpace,
                Bits = Constants.BlocksAdmin
            };
            adminSpaceContainer.AdminSpaces[0].Space = blockAdminSpace;
            adminSpaceContainer.AdminSpaces[0].Bits = recycled ? 0xFE000000 : 0xFC000000;
            await Disk.WriteAdminSpaceContainer(volume, adminSpaceContainer);

            // root object container
            var rootContainer = new ObjectContainer
            {
                OwnBlock = blockRoot,
                Parent = 0,
                RootInfo = new RootInfo
                {
                    FreeBlocks = totalBlocks - startFree - reservedEnd,
                    DateCreated = date
                }
            };
            rootContainer.Objects.Add(new SfsObject
            {
                ObjectNode = Constants.RootNode,
                Protection = (uint)(ProtectionBits.Read | ProtectionBits.Write | ProtectionBits.Executable | ProtectionBits.Delete),
                DateModified = date,
                Bits = Constants.OTypeDir,
                HashTable = blockHashTable,
                FirstDirBlock = recycled ? blockRecycled : 0,
                Name = volumeName
            });
            await Disk.WriteBlock(volume, blockRoot, ObjectContainerBuilder.Build(rootContainer, blockSize));

            // root hash table
            var hashTable = new HashTableBlock(blockSize)
            {
                OwnBlock = blockHashTable,
                Parent = Constants.RootNode
            };
            if (recycled)
            {
                hashTable.HashEntries[HashHelper.HashChain(recycledHash, blockSize)] = Constants.RecycledNode;
            }

            await Disk.WriteHashTable(volume, hashTable);

            // transaction ok block
            await Disk.WriteBlock(volume, blockTransaction,
                TransactionBlockBuilder.BuildOk(new TransactionOkBlock { OwnBlock = blockTransaction }, blockSize));

            // extent b-tree root
            var extentRoot = BNodeContainer.CreateLeaf();
            extentRoot.OwnBlock = blockExtentBNodeRoot;
            await Disk.WriteBNodeContainer(volume, extentRoot);

            // object node root, object node 0 doesn't exist and nodes 3 to 6 are reserved
            var nodeContainer = NodeContainer.CreateLeaf(blockSize, 1);
            nodeContainer.OwnBlock = blockObjectNodeRoot;
            nodeContainer.ObjectNodes[0].Data = blockRoot;
            if (recycled)
            {
                nodeContainer.ObjectNodes[1].Data = blockRecycled;
                nodeContainer.ObjectNodes[1].Hash16 = recycledHash;
            }
            else
            {
                nodeContainer.ObjectNodes[1].Data = Constants.ReservedNode;
            }

            for (var i = 2; i <= 5; i++)
            {
                nodeContainer.ObjectNodes[i].Data = Constants.ReservedNode;
            }

            await Disk.WriteNodeContainer(volume, nodeContainer);

            // recycled directory
            if (recycled)
            {
                var recycledContainer = new ObjectContainer
                {
                    OwnBlock = blockRecycled,
                    Parent = Constants.RootNode
                };
                recycledContainer.Objects.Add(new SfsObject
                {
                    ObjectNode = Constants.RecycledNode,
                    Protection = (uint)(ProtectionBits.Read | ProtectionBits.Write),
                    DateModified = date,
                    Bits = (byte)(Constants.OTypeDir | Constants.OTypeUndeletable | Constants.OTypeQuickDir |
                                  (showRecycled ? 0 : Constants.OTypeHidden)),
                    HashTable = 0,
                    FirstDirBlock = 0,
                    Name = Constants.RecycledName
                });
                await Disk.WriteObjectContainer(volume, recycledContainer);
            }

            // bitmap blocks, blocks before start free and reserved blocks at end are used
            var endFree = totalBlocks - reservedEnd;
            for (uint i = 0; i < bitmapBlocks; i++)
            {
                var bitmapBlock = new BitmapBlock(blockSize) { OwnBlock = blockBitmapBase + i };
                var firstBlockInBitmap = i * blocksInBitmap;
                for (var w = 0; w < bitmapBlock.Map.Length; w++)
                {
                    uint value = 0;
                    for (var b = 0; b < 32; b++)
                    {
                        var block = (ulong)firstBlockInBitmap + (ulong)(w * 32 + b);
                        if (block >= startFree && block < endFree)
                        {
                            value |= 0x80000000U >> b;
                        }
                    }

                    bitmapBlock.Map[w] = value;
                }

                await Disk.WriteBitmapBlock(volume, bitmapBlock);
            }

            // root blocks at first and last block of partition
            var rootBlock = new RootBlock
            {
                OwnBlock = 0,
                Version = Constants.StructureVersion,
                SequenceNumber = 0,
                DateCreated = date,
                Bits = (byte)((caseSensitive ? Constants.RootBitsCaseSensitive : 0) |
                              (recycled ? Constants.RootBitsRecycled : 0)),
                FirstByte = (ulong)firstByte,
                LastByte = (ulong)lastByte,
                TotalBlocks = totalBlocks,
                BlockSize = (uint)blockSize,
                BitmapBase = blockBitmapBase,
                AdminSpaceContainer = blockAdminSpace,
                RootObjectContainer = blockRoot,
                ExtentBNodeRoot = blockExtentBNodeRoot,
                ObjectNodeRoot = blockObjectNodeRoot
            };
            await Disk.WriteBlock(volume, 0, RootBlockBuilder.Build(rootBlock, blockSize));

            rootBlock.OwnBlock = totalBlocks - 1;
            await Disk.WriteBlock(volume, totalBlocks - 1, RootBlockBuilder.Build(rootBlock, blockSize));

            await stream.FlushAsync();
        }
    }
}
