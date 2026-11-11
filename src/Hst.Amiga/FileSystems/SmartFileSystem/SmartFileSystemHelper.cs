namespace Hst.Amiga.FileSystems.SmartFileSystem
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;
    using Core.Converters;
    using RigidDiskBlocks;
    using BlockHelper = Blocks.BlockHelper;

    public static class SmartFileSystemHelper
    {
        /// <summary>
        /// Get partition start and end byte offsets (end exclusive) and file system block size from partition block.
        /// </summary>
        public static (long FirstByte, long LastByte, int BlockSize) GetGeometry(PartitionBlock partitionBlock)
        {
            var sectorSize = partitionBlock.BlockSize == 0 ? 512 : (long)partitionBlock.BlockSize;
            var bytesPerCylinder = (long)partitionBlock.Surfaces * partitionBlock.BlocksPerTrack * sectorSize;
            var firstByte = partitionBlock.LowCyl * bytesPerCylinder;
            var lastByte = (partitionBlock.HighCyl + 1L) * bytesPerCylinder;
            var blockSize = partitionBlock.FileSystemBlockSize != 0
                ? (int)partitionBlock.FileSystemBlockSize
                : (int)(sectorSize * Math.Max(1, partitionBlock.Sectors));
            return (firstByte, lastByte, blockSize);
        }

        public static void ValidateDosType(byte[] dosType)
        {
            if (dosType == null || dosType.Length < 4)
            {
                return;
            }

            var value = BigEndianConverter.ConvertBytesToUInt32(dosType);
            if (value == Constants.DosTypeSfs2)
            {
                throw new NotSupportedException("Smart file system dos type SFS\\2 is not supported");
            }
        }

        /// <summary>
        /// Mount smart file system volume from partition block.
        /// </summary>
        public static async Task<Volume> Mount(Stream stream, PartitionBlock partitionBlock)
        {
            ValidateDosType(partitionBlock.DosType);
            var geometry = GetGeometry(partitionBlock);
            return await Mount(stream, geometry.FirstByte, geometry.LastByte - geometry.FirstByte, geometry.BlockSize);
        }

        /// <summary>
        /// Mount smart file system volume in stream.
        /// </summary>
        /// <param name="stream">Stream with smart file system volume.</param>
        /// <param name="partitionOffset">Byte offset of partition in stream.</param>
        /// <param name="partitionSize">Size of partition in bytes.</param>
        /// <param name="blockSize">File system block size.</param>
        public static async Task<Volume> Mount(Stream stream, long partitionOffset, long partitionSize, int blockSize)
        {
            BlockHelper.ValidateBlockSize(blockSize);

            var totalBlocks = (uint)(partitionSize / blockSize);
            var volume = new Volume
            {
                Stream = stream,
                PartitionOffset = partitionOffset,
                BlockSize = blockSize,
                TotalBlocks = totalBlocks,
                ShiftsBlock32 = Log2(blockSize) - Constants.BlckFAccuracy
            };

            var rootBlockBytes = await Disk.ReadBlock(volume, 0);
            var rootBlock = RootBlockParser.Parse(rootBlockBytes);
            if (rootBlock.Id == Constants.DosTypeSfs2)
            {
                throw new NotSupportedException("Smart file system dos type SFS\\2 is not supported");
            }

            Disk.CheckBlock(rootBlockBytes, 0, Constants.RootBlockId);

            if (rootBlock.Version != Constants.StructureVersion)
            {
                throw new IOException($"Smart file system structure version {rootBlock.Version} is not supported, only version {Constants.StructureVersion} is supported");
            }

            if (rootBlock.BlockSize != blockSize)
            {
                throw new IOException($"Smart file system block size {rootBlock.BlockSize} doesn't match partition block size {blockSize}");
            }

            if (rootBlock.TotalBlocks != totalBlocks || (rootBlock.LastByte - rootBlock.FirstByte) / (ulong)blockSize != totalBlocks)
            {
                throw new IOException($"Smart file system total blocks {rootBlock.TotalBlocks} doesn't match partition size with {totalBlocks} blocks");
            }

            volume.RootBlock = rootBlock;
            volume.CaseSensitive = rootBlock.CaseSensitive;

            // second root block at last block of partition
            try
            {
                var secondRootBlockBytes = await Disk.ReadBlock(volume, totalBlocks - 1);
                Disk.CheckBlock(secondRootBlockBytes, totalBlocks - 1, Constants.RootBlockId);
            }
            catch (IOException e)
            {
                volume.Logs.Add($"WARNING: Second root block is invalid: {e.Message}");
            }

            await Transactions.CheckForTransaction(volume);

            await Bitmap.Load(volume);

            var rootInfo = await Disk.ReadRootInfo(volume);
            if (rootInfo.FreeBlocks != volume.FreeBlocks)
            {
                volume.Logs.Add($"WARNING: Free blocks {rootInfo.FreeBlocks} in root info doesn't match {volume.FreeBlocks} free blocks in bitmap");
                if (!volume.IsReadOnly)
                {
                    var freeBlocks = volume.FreeBlocks;
                    await Disk.UpdateRootInfo(volume, x => x.FreeBlocks = freeBlocks);
                }
            }

            volume.RovingPointer = CalculateRovingPointer(volume);

            return volume;
        }

        /// <summary>
        /// Calculate initial roving pointer, where search for free data blocks starts. A reserve of blocks after the
        /// bitmap blocks is kept for admin spaces.
        /// </summary>
        public static uint CalculateRovingPointer(Volume volume)
        {
            var blocks512 = (ulong)volume.TotalBlocks * (ulong)(volume.BlockSize / 512);
            var reserve = (ulong)Math.Sqrt(blocks512);
            reserve = (reserve << 2) + reserve;

            if (reserve > blocks512 / 100)
            {
                reserve = blocks512 / 100;
            }

            if (reserve < Constants.BlocksAdmin)
            {
                reserve = Constants.BlocksAdmin;
                if (reserve > volume.TotalBlocks >> 1)
                {
                    reserve = 0;
                }
            }

            var rovingPointer = volume.RootBlock.BitmapBase + (ulong)volume.BitmapBlocks.Length + reserve;
            return rovingPointer >= volume.TotalBlocks ? 0 : (uint)rovingPointer;
        }

        public static int Log2(int value)
        {
            var log = 0;
            while ((1 << log) < value)
            {
                log++;
            }

            return log;
        }
    }
}
