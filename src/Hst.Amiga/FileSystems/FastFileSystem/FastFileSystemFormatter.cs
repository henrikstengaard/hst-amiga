﻿namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Blocks;
    using Core.Extensions;
    using RigidDiskBlocks;

    public static class FastFileSystemFormatter
    {
        /// <summary>
        /// format partition with fast file system
        /// </summary>
        public static async Task FormatPartition(Stream stream, PartitionBlock partitionBlock,
            string diskName)
        {
            await Format(stream, partitionBlock.LowCyl, partitionBlock.HighCyl, partitionBlock.Reserved,
                partitionBlock.Surfaces, partitionBlock.BlocksPerTrack, partitionBlock.BlockSize,
                partitionBlock.FileSystemBlockSize, partitionBlock.DosType, diskName);
        }

        /// <summary>
        /// format disk with fast file system
        /// </summary>
        public static async Task Format(Stream stream, uint lowCyl, uint highCyl, uint reserved, uint surfaces,
            uint blocksPerTrack, uint blockSize, uint fileSystemBlockSize, byte[] dosType, string diskName)
        {
            var rootBlockOffset =
                OffsetHelper.CalculateRootBlockOffset(lowCyl, highCyl,
                    reserved, surfaces, blocksPerTrack, fileSystemBlockSize);

            // create root block
            var rootBlock = new RootBlock((int)fileSystemBlockSize)
            {
                BitmapBlocksOffset = rootBlockOffset + 1,
                Name = diskName,
                Extension = rootBlockOffset
            };

            var bitmapBlocks = BlockHelper.CreateBitmapBlocks(lowCyl, highCyl, surfaces, blocksPerTrack, 
                blockSize, fileSystemBlockSize).ToList();
            var bitmapExtensionBlocks =
                BlockHelper.CreateBitmapExtensionBlocks(
                        bitmapBlocks.Skip(Constants.MaxBitmapBlockPointersInRootBlock).ToList(),
                        fileSystemBlockSize)
                    .ToList();

            rootBlock.BitmapBlocks = bitmapBlocks.Take(Constants.MaxBitmapBlockPointersInRootBlock).ToList();
            rootBlock.BitmapExtensionBlocks = bitmapExtensionBlocks;

            OffsetHelper.SetRootBlockOffsets(rootBlock);

            // create bitmap of blocks allocated by root block, bitmap blocks and bitmap extension blocks
            var bitmaps = new Dictionary<uint, bool>
            {
                { rootBlockOffset, false }
            };

            foreach (var bitmapBlock in bitmapBlocks)
            {
                bitmaps[bitmapBlock.Offset] = false;
            }

            foreach (var bitmapExtensionBlock in bitmapExtensionBlocks)
            {
                bitmaps[bitmapExtensionBlock.Offset] = false;
            }

            BlockHelper.UpdateBitmaps(bitmapBlocks, bitmaps, reserved, fileSystemBlockSize);

            // calculate partition start offset
            var partitionStartByteOffset = (long)lowCyl * surfaces * blocksPerTrack * blockSize;

            // write boot block with dos type at partition start
            var bootBlockBytes = new byte[blockSize * reserved];
            Array.Copy(dosType, 0, bootBlockBytes, 0, dosType.Length);
            stream.Seek(partitionStartByteOffset, SeekOrigin.Begin);
            await stream.WriteBytes(bootBlockBytes);

            // build root block bytes
            var rootBlockBytes = RootBlockBuilder.Build(rootBlock, (int)fileSystemBlockSize);

            // write root block
            var rootBlockByteOffset = partitionStartByteOffset + (long)rootBlockOffset * fileSystemBlockSize;
            stream.Seek(rootBlockByteOffset, SeekOrigin.Begin);
            await stream.WriteBytes(rootBlockBytes);

            // write bitmap blocks
            foreach (var bitmapBlock in rootBlock.BitmapBlocks)
            {
                // seek bitmap block offset
                var bitmapBlockByteOffset = partitionStartByteOffset +
                                            (long)bitmapBlock.Offset * fileSystemBlockSize;
                stream.Seek(bitmapBlockByteOffset, SeekOrigin.Begin);

                // build and write bitmap block
                var bitmapBlockBytes = BitmapBlockBuilder.Build(bitmapBlock, (int)fileSystemBlockSize);
                await stream.WriteBytes(bitmapBlockBytes);
            }

            if (!rootBlock.BitmapExtensionBlocks.Any())
            {
                return;
            }

            // write bitmap extension blocks
            foreach (var bitmapExtensionBlock in rootBlock.BitmapExtensionBlocks)
            {
                // seek bitmap extension block offset
                var bitmapExtensionBlockByteOffset = partitionStartByteOffset +
                                                     (long)bitmapExtensionBlock.Offset *
                                                     fileSystemBlockSize;
                stream.Seek(bitmapExtensionBlockByteOffset, SeekOrigin.Begin);

                // build and write bitmap extension block
                var bitmapExtensionBlockBytes =
                    BitmapExtensionBlockBuilder.Build(bitmapExtensionBlock,
                        fileSystemBlockSize);
                await stream.WriteBytes(bitmapExtensionBlockBytes);

                // write bitmap blocks
                foreach (var bitmapBlock in bitmapExtensionBlock.BitmapBlocks)
                {
                    // seek bitmap block offset
                    var bitmapBlockByteOffset = partitionStartByteOffset +
                                                (long)bitmapBlock.Offset *
                                                fileSystemBlockSize;
                    stream.Seek(bitmapBlockByteOffset, SeekOrigin.Begin);

                    // build and write bitmap block
                    var bitmapBlockBytes = BitmapBlockBuilder.Build(bitmapBlock, (int)fileSystemBlockSize);
                    await stream.WriteBytes(bitmapBlockBytes);
                }
            }
        }
    }
}