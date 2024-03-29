﻿namespace Hst.Amiga.RigidDiskBlocks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Core.Extensions;
    using Extensions;
    using FileSystems;

    public static class PartitionBlockReader
    {
        public static async Task<IEnumerable<PartitionBlock>> Read(RigidDiskBlock rigidDiskBlock, Stream stream, bool ignoreChecksum = false)
        {
            if (rigidDiskBlock.PartitionList == BlockIdentifiers.EndOfBlock)
            {
                return Enumerable.Empty<PartitionBlock>();
            }


            // get partition list block and set partition number to 1
            var partitionList = rigidDiskBlock.PartitionList;

            var partitionBlocks = new List<PartitionBlock>();

            do
            {
                // calculate partition block offset
                var partitionBlockOffset = rigidDiskBlock.BlockSize * partitionList;

                // seek partition block offset
                stream.Seek(partitionBlockOffset, SeekOrigin.Begin);

                // read block
                var blockBytes = await Disk.ReadBlock(stream, (int)rigidDiskBlock.BlockSize);

                // read partition block
                var partitionBlock = await Parse(blockBytes, (int)rigidDiskBlock.BlockSize, ignoreChecksum);

                // fail, if partition block is null
                if (partitionBlock == null)
                {
                    throw new IOException("Invalid partition block");
                }

                partitionBlocks.Add(partitionBlock);

                // get next partition list block and increase partition number
                partitionList = partitionBlock.NextPartitionBlock;
            } while (partitionList > 0 && partitionList != BlockIdentifiers.EndOfBlock);

            rigidDiskBlock.PartitionBlocks = partitionBlocks;

            return partitionBlocks;
        }

        public static async Task<PartitionBlock> Parse(byte[] blockBytes, int blockSize, bool ignoreChecksum = false)
        {
            var blockStream = new MemoryStream(blockBytes);

            var identifier = BitConverter.ToUInt32(await blockStream.ReadBytes(4), 0);
            if (!identifier.Equals(BlockIdentifiers.PartitionBlock))
            {
                return null;
            }

            var size = await blockStream.ReadBigEndianUInt32(); // Size of the structure for checksums
            var checksum = await blockStream.ReadBigEndianInt32(); // Checksum of the structure
            var hostId = await blockStream.ReadBigEndianUInt32(); // SCSI Target ID of host, not really used 
            var nextPartitionBlock = await blockStream.ReadBigEndianUInt32(); // Block number of the next PartitionBlock
            var flags = await blockStream.ReadBigEndianUInt32(); // Part Flags (NOMOUNT and BOOTABLE)

            // skip reserved
            blockStream.Seek(4 * 2, SeekOrigin.Current);

            var devFlags = await blockStream.ReadBigEndianUInt32(); // Preferred flags for OpenDevice
            var driveNameLength =
                (await blockStream.ReadBytes(1)).FirstOrDefault(); //  Preferred DOS device name: BSTR form
            var driveName = await blockStream.ReadString(driveNameLength); // # Preferred DOS device name: BSTR form

            if (driveNameLength < 31)
            {
                await blockStream.ReadBytes(31 - driveNameLength);
            }

            // skip reserved
            blockStream.Seek(4 * 15, SeekOrigin.Current);

            var sizeOfVector = await blockStream.ReadBigEndianUInt32(); // Size of Environment vector
            var sizeBlock = await blockStream.ReadBigEndianUInt32(); // Size of the blocks in 32 bit words, usually 128
            var secOrg = await blockStream.ReadBigEndianUInt32(); // Not used; must be 0
            var surfaces = await blockStream.ReadBigEndianUInt32(); // Number of heads (surfaces)
            var sectors = await blockStream.ReadBigEndianUInt32(); // Disk sectors per block, used with SizeBlock, usually 1
            var blocksPerTrack = await blockStream.ReadBigEndianUInt32(); // Blocks per track. drive specific
            var reserved = await blockStream.ReadBigEndianUInt32(); // DOS reserved blocks at start of partition.
            var preAlloc = await blockStream.ReadBigEndianUInt32(); // DOS reserved blocks at end of partition
            var interleave = await blockStream.ReadBigEndianUInt32(); // Not used, usually 0
            var lowCyl = await blockStream.ReadBigEndianUInt32(); // First cylinder of the partition
            var highCyl = await blockStream.ReadBigEndianUInt32(); // Last cylinder of the partition
            var numBuffer = await blockStream.ReadBigEndianUInt32(); // Initial # DOS of buffers.
            var bufMemType = await blockStream.ReadBigEndianUInt32(); // Type of mem to allocate for buffers
            var maxTransfer = await blockStream.ReadBigEndianUInt32(); // Max number of bytes to transfer at a time
            var mask = await blockStream.ReadBigEndianUInt32(); // Address Mask to block out certain memory
            var bootPriority = await blockStream.ReadBigEndianInt32(); // Boot priority for autoboot
            var dosType = await blockStream.ReadBytes(4); // # Dostype of the file system
            var baud = await blockStream.ReadBigEndianUInt32(); // Baud rate for serial handler
            var control = await blockStream.ReadBigEndianUInt32(); // Control word for handler/filesystem 
            var bootBlocks = await blockStream.ReadBigEndianUInt32(); // Number of blocks containing boot code 

            // skip reserved
            blockStream.Seek(4 * 12, SeekOrigin.Current);
            
            // calculate size of partition in bytes
            var partitionSize = (long)(highCyl - lowCyl + 1) * surfaces * blocksPerTrack * blockSize;

            var calculatedChecksum = ChecksumHelper.CalculateChecksum(blockBytes, 8, (int)size * SizeOf.Long);

            if (!ignoreChecksum && checksum != calculatedChecksum)
            {
                throw new Exception("Invalid partition block checksum");
            }

            var fileSystemBlockSize = sizeBlock * SizeOf.ULong * sectors;

            return new PartitionBlock
            {
                BlockBytes = blockBytes,
                Checksum = checksum,
                HostId = hostId,
                NextPartitionBlock = nextPartitionBlock,
                Flags = flags,
                DevFlags = devFlags,
                DriveName = driveName,
                SizeOfVector = sizeOfVector,
                SizeBlock = sizeBlock,
                SecOrg = secOrg,
                Surfaces = surfaces,
                Sectors = sectors,
                BlocksPerTrack = blocksPerTrack,
                Reserved = reserved,
                PreAlloc = preAlloc,
                Interleave = interleave,
                LowCyl = lowCyl,
                HighCyl = highCyl,
                NumBuffer = numBuffer,
                BufMemType = bufMemType,
                MaxTransfer = maxTransfer,
                Mask = mask,
                BootPriority = bootPriority,
                DosType = dosType,
                Baud = baud,
                Control = control,
                BootBlocks = bootBlocks,
                PartitionSize = partitionSize,
                FileSystemBlockSize = fileSystemBlockSize,
            };
        }
    }
}