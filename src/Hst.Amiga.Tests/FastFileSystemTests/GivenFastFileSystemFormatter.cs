namespace Hst.Amiga.Tests.FastFileSystemTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Core.Extensions;
    using Extensions;
    using FileSystems.FastFileSystem;
    using FileSystems.FastFileSystem.Blocks;
    using RigidDiskBlocks;
    using Xunit;
    using Constants = FileSystems.FastFileSystem.Constants;
    using File = System.IO.File;
    using FileMode = System.IO.FileMode;

    public class GivenFastFileSystemFormatter
    {
        private const long DISK_SIZE_10_MB = 1024L * 1024 * 10; 
        private const long DISK_SIZE_100_MB = 1024L * 1024 * 100; 
        private const long DISK_SIZE_32_GB = 1024L * 1024 * 1024 * 32; 
        private readonly byte[] dos3DosType = { 0x44, 0x4f, 0x53, 0x3 };

        [Fact]
        public async Task WhenFormattingDoubleDensityFloppyDiskFileThenRootBlockAndBitmapBlocksAreCreated()
        {
            var adfPath = "dos.adf";
            const uint lowCyl = FloppyDiskConstants.DoubleDensity.LowCyl;
            const uint highCyl = FloppyDiskConstants.DoubleDensity.HighCyl;
            const uint reservedBlocks = FloppyDiskConstants.DoubleDensity.ReservedBlocks;
            const uint surfaces = FloppyDiskConstants.DoubleDensity.Heads;
            const uint blocksPerTrack = FloppyDiskConstants.DoubleDensity.Sectors;
            const uint blockSize = FloppyDiskConstants.BlockSize;
            const uint fileSystemBlockSize = FloppyDiskConstants.BlockSize;

            // arrange - create double density floppy disk
            await using var adfStream = File.Open(adfPath, FileMode.Create, FileAccess.ReadWrite);
            adfStream.SetLength(FloppyDiskConstants.DoubleDensity.Size);

            // act - format first partition
            await FastFileSystemFormatter.Format(adfStream, lowCyl, highCyl, reservedBlocks,
                surfaces, blocksPerTrack, blockSize, fileSystemBlockSize, dos3DosType, "Workbench");

            // arrange - calculate root block offset
            var rootBlockOffset = OffsetHelper.CalculateRootBlockOffset(lowCyl, highCyl,
                reservedBlocks, surfaces, blocksPerTrack, FloppyDiskConstants.FileSystemBlockSize);

            // arrange - calculate partition start offset
            var blocksPerCylinder = surfaces * blocksPerTrack;
            var cylinderSize = blocksPerCylinder * fileSystemBlockSize;

            // assert - dos type is present at partition start offset
            adfStream.Seek(0, SeekOrigin.Begin);
            var blockBytes = await Amiga.Disk.ReadBlock(adfStream, (int)fileSystemBlockSize);
            for (var i = 0; i < dos3DosType.Length; i++)
            {
                Assert.Equal(dos3DosType[i], blockBytes[i]);
            }

            // assert - root block is present at root block offset and matches disk name
            adfStream.Seek(rootBlockOffset * fileSystemBlockSize, SeekOrigin.Begin);
            var rootBlockBytes = await Amiga.Disk.ReadBlock(adfStream, (int)fileSystemBlockSize);
            var rootBlock = RootBlockParser.Parse(rootBlockBytes);
            Assert.Equal("Workbench", rootBlock.DiskName);

            // calculate number of blocks partition contains
            var cylinders = highCyl - lowCyl + 1;
            var blocks = (cylinderSize * cylinders) / fileSystemBlockSize;

            // calculate number of bitmaps each bitmap block represents
            var bitmapsPerBitmapBlockCount =
                FileSystems.FastFileSystem.BlockHelper.CalculateBitmapsPerBitmapBlockCount(fileSystemBlockSize);

            // calculate number of bitmap blocks are required for all blocks
            var bitmapBlocksCount = Math.Ceiling((double)blocks / bitmapsPerBitmapBlockCount);

            // assert bitmap blocks
            for (var i = 0; i < bitmapBlocksCount; i++)
            {
                // assert - root block has bitmap block offset
                Assert.Equal(rootBlock.BitmapBlockOffsets[i], rootBlockOffset + i + 1);

                // seek bitmap block offset in hdf stream
                adfStream.Seek((rootBlockOffset + 1 + i) * fileSystemBlockSize, SeekOrigin.Begin);

                // read bitmap block from hdf stream
                var bitmapBlockBytes = await Amiga.Disk.ReadBlock(adfStream, (int)fileSystemBlockSize);
                var bitmapBlock = BitmapBlockParser.Parse(bitmapBlockBytes);

                // create expected blocks free map, used blocks are set false and free blocks are set true:
                // - root block
                // - bitmap blocks
                // note reserved blocks in beginning of partition are not part of bitmap blocks
                var expectedBlocksFreeMap = new bool[bitmapsPerBitmapBlockCount];
                for (var b = 0; b < bitmapsPerBitmapBlockCount; b++)
                {
                    var blockOffset = reservedBlocks + (bitmapsPerBitmapBlockCount * i) + b;
                    expectedBlocksFreeMap[b] = !((blockOffset >= rootBlockOffset &&
                                                  blockOffset <= rootBlockOffset + bitmapBlocksCount) ||
                                                 blockOffset >= blocks + reservedBlocks);
                }

                var expectedMapEntries = expectedBlocksFreeMap.ChunkBy(Constants.BitmapsPerULong)
                    .Select(x => MapBlockHelper.ConvertBlockFreeMapToUInt32(x.ToArray())).ToArray();

                Assert.Equal(expectedMapEntries, bitmapBlock.Map);
            }
        }

        [Theory]
        [InlineData(DISK_SIZE_10_MB, 512)]
        [InlineData(DISK_SIZE_10_MB, 1024)]
        [InlineData(DISK_SIZE_100_MB, 512)]
        [InlineData(DISK_SIZE_100_MB, 1024)]
        [InlineData(DISK_SIZE_32_GB, 512)]
        public async Task WhenFormattingHardDiskFileThenRootBlockAndBitmapBlocksAreCreated(long diskSize, int fileSystemBlockSize)
        {
            // arrange: create stream for testing
            var stream = new BlockMemoryStream();

            // arrange - create hdf file with 1 partition using DOS3 dos type 
            var rigidDiskBlock = RigidDiskBlock
                .Create(diskSize)
                .AddFileSystem(dos3DosType, Encoding.ASCII.GetBytes(
                    "$VER: FastFileSystem 1.0 (12/12/22) ")) // dummy fast file system used for testing
                .AddPartition("DH0", bootable: true, fileSystemBlockSize: fileSystemBlockSize);

            // arrange: create stream and write rigid disk block
            await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);
            
            // act - format first partition using fast file system formatter
            var partition = rigidDiskBlock.PartitionBlocks.First();
            await FastFileSystemFormatter.FormatPartition(stream, partition, "Workbench");

            // arrange - calculate root block offset
            var rootBlockOffset = (long)OffsetHelper.CalculateRootBlockOffset(partition.LowCyl, partition.HighCyl,
                partition.Reserved, partition.Surfaces, partition.BlocksPerTrack, (uint)fileSystemBlockSize);

            // arrange - calculate partition start offset
            var blocksPerCylinder = rigidDiskBlock.Heads * rigidDiskBlock.Sectors;
            var cylinderSize = blocksPerCylinder * rigidDiskBlock.BlockSize;
            var partitionStartOffset = (long)(cylinderSize * partition.LowCyl);

            // assert - dos type is present at partition start offset
            stream.Seek(partitionStartOffset, SeekOrigin.Begin);
            var blockBytes = await Amiga.Disk.ReadBlock(stream, (int)partition.FileSystemBlockSize);
            for (var i = 0; i < dos3DosType.Length; i++)
            {
                Assert.Equal(dos3DosType[i], blockBytes[i]);
            }

            // assert - root block is present at root block offset and matches disk name
            stream.Seek(partitionStartOffset + rootBlockOffset * partition.FileSystemBlockSize, SeekOrigin.Begin);
            var rootBlockBytes = await Amiga.Disk.ReadBlock(stream, (int)partition.FileSystemBlockSize);
            var rootBlock = RootBlockParser.Parse(rootBlockBytes);
            Assert.Equal("Workbench", rootBlock.DiskName);

            // calculate number of blocks partition contains
            var cylinders = partition.HighCyl - partition.LowCyl + 1;
            var blocks = ((long)cylinderSize * cylinders) / partition.FileSystemBlockSize;

            // calculate number of bitmaps each bitmap block represents
            var bitmapsPerBitmapBlockCount =
                FileSystems.FastFileSystem.BlockHelper.CalculateBitmapsPerBitmapBlockCount(partition
                    .FileSystemBlockSize);

            // calculate number of bitmap blocks are required for all blocks
            var bitmapBlocksCount = Math.Ceiling((double)blocks / bitmapsPerBitmapBlockCount);

            // arrange - calculate bitmap block offsets per bitmap extension block
            var bitmapBlockOffsetsPerBitmapExtensionBlock =
                FileSystems.FastFileSystem.BlockHelper.CalculateBitmapBlockOffsetsPerBitmapExtensionBlock(partition
                    .FileSystemBlockSize);

            // arrange - calculate bitmap extension blocks count            
            var bitmapExtensionBlocksCount = bitmapBlocksCount > Constants.MaxBitmapBlockPointersInRootBlock
                ? Convert.ToInt32(Math.Ceiling((bitmapBlocksCount - Constants.MaxBitmapBlockPointersInRootBlock) / bitmapBlockOffsetsPerBitmapExtensionBlock))
                : 0;

            // arrange - read root bitmap blocks from stream
            var rootBitmapBlocks = (await ReadBitmapBlocks(stream, partitionStartOffset,
                rootBlock.BitmapBlockOffsets.Select(x => x),
                (int)partition.FileSystemBlockSize)).ToList();

            // assert - first 25 bitmap block offsets in root block is after root block offset
            for (var i = 0; i < bitmapBlocksCount && i < Constants.MaxBitmapBlockPointersInRootBlock; i++)
            {
                Assert.Equal(rootBlock.BitmapBlockOffsets[i], rootBlockOffset + i + 1);
            }

            // arrange - read bitmap extension blocks from stream
            var bitmapExtensionBlocks = (await ReadBitmapExtensionBlocks(stream, partitionStartOffset,
                rootBlock.BitmapExtensionBlocksOffset, (int)partition.FileSystemBlockSize)).ToList();

            // assert - calculated bitmap extension blocks count is equal to bitmap extension blocks read
            Assert.Equal(bitmapExtensionBlocksCount, bitmapExtensionBlocks.Count);
            
            // arrange - concatenate root bitmap blocks and bitmap blocks from bitmap extension blocks
            var bitmapBlocks = rootBitmapBlocks.Concat(bitmapExtensionBlocks.SelectMany(x => x.BitmapBlocks)).ToList();

            // assert - calculated bitmap blocks count is equal to bitmap blocks read
            Assert.Equal(bitmapBlocksCount, bitmapBlocks.Count);

            // arrange - calculate used blocks start and end offset
            var usedBlocksStartOffset = rootBlockOffset;
            var usedBlocksEndOffset = usedBlocksStartOffset + bitmapBlocksCount + bitmapExtensionBlocksCount;

            // arrange - calculate max block offset
            var maxBlockOffset = blocks + partition.Reserved;

            // assert - bitmap block maps
            for (var i = 0; i < bitmapBlocksCount; i++)
            {
                var bitmapBlock = bitmapBlocks[i];

                // arrange - create expected blocks free map, used blocks are set false and free blocks are set true:
                // - root block
                // - bitmap blocks
                // - bitmap extension blocks
                // note reserved blocks in beginning of partition are not part of bitmap blocks
                var expectedBlocksFreeMap = new bool[bitmapsPerBitmapBlockCount];
                for (var b = 0; b < bitmapsPerBitmapBlockCount; b++)
                {
                    var blockOffset = partition.Reserved + (bitmapsPerBitmapBlockCount * i) + b;
                    var isBlockFree = (blockOffset < usedBlocksStartOffset || blockOffset > usedBlocksEndOffset) &&
                                      blockOffset < maxBlockOffset;
                    expectedBlocksFreeMap[b] = isBlockFree;
                }

                // arrange - create map
                var expectedMapEntries = expectedBlocksFreeMap.ChunkBy(Constants.BitmapsPerULong)
                    .Select(x => MapBlockHelper.ConvertBlockFreeMapToUInt32(x.ToArray())).ToArray();

                // assert - bitmap block map is equal to expected bitmap block map
                Assert.Equal(expectedMapEntries, bitmapBlock.Map);
            }
        }

        [Theory]
        [InlineData(512)]
        [InlineData(1024)]
        public async Task WhenFormatting1GbPartitionAfter30GbInHardDiskFileThenRootBlockAndBitmapBlocksAreCreated(int fileSystemBlockSize)
        {
            // arrange - block size
            const uint blockSize = 512U;
            
            // arrange - create rigid disk block with size 64gb
            var rigidDiskBlock = RigidDiskBlock.Create(64.GB());

            // arrange - create partition 1 with size 30gb
            var partitionBlock1 =
                PartitionBlock.Create(rigidDiskBlock, DosTypeHelper.FormatDosType("DOS3"), "DH0", 30.GB(), 
                    fileSystemBlockSize: fileSystemBlockSize);
            rigidDiskBlock.PartitionBlocks = rigidDiskBlock.PartitionBlocks.Concat(new[] { partitionBlock1 }).ToList();

            // arrange - create partition 2 with size 1gb
            var partitionBlock2 =
                PartitionBlock.Create(rigidDiskBlock, DosTypeHelper.FormatDosType("DOS3"), "DH0", 1.GB(), 
                    fileSystemBlockSize: fileSystemBlockSize);
            rigidDiskBlock.PartitionBlocks = rigidDiskBlock.PartitionBlocks.Concat(new[] { partitionBlock2 }).ToList();

            // act - format second partition using fast file system formatter formatter
            var stream = new BlockMemoryStream();
            await FastFileSystemFormatter.FormatPartition(stream, partitionBlock2, "Work");

            // assert - stream has block written at boot block offset 
            var bootBlockOffset = (long)partitionBlock2.LowCyl * rigidDiskBlock.Heads * rigidDiskBlock.Sectors * 512;
            Assert.True(stream.Blocks.ContainsKey(bootBlockOffset));

            // assert - stream has block written at root block offset 
            var rootBlockOffset = OffsetHelper.CalculateRootBlockOffset(partitionBlock2.LowCyl, partitionBlock2.HighCyl,
                partitionBlock2.Reserved, partitionBlock2.Surfaces, partitionBlock2.BlocksPerTrack, blockSize);
            Assert.True(stream.Blocks.ContainsKey(bootBlockOffset + (rootBlockOffset * 512)));
        }

        private static async Task<IEnumerable<BitmapBlock>> ReadBitmapBlocks(Stream stream, long partitionStartOffset,
            IEnumerable<uint> bitmapBlockOffsets, int blockSize)
        {
            var bitmapBlocks = new List<BitmapBlock>();

            foreach (var blockBitmapBlockOffset in bitmapBlockOffsets)
            {
                if (blockBitmapBlockOffset == 0)
                {
                    continue;
                }

                // seek bitmap block offset in hdf stream
                stream.Seek(partitionStartOffset + (long)blockBitmapBlockOffset * blockSize,
                    SeekOrigin.Begin);

                // read bitmap block from hdf stream
                var bitmapBlockBytes = await Amiga.Disk.ReadBlock(stream, blockSize);
                var bitmapBlock = BitmapBlockParser.Parse(bitmapBlockBytes);

                bitmapBlocks.Add(bitmapBlock);
            }

            return bitmapBlocks;
        }

        private static async Task<IEnumerable<BitmapExtensionBlock>> ReadBitmapExtensionBlocks(Stream stream,
            long partitionStartOffset, long bitmapExtensionBlocksOffset, int blockSize)
        {
            var bitmapExtensionBlocks = new List<BitmapExtensionBlock>();

            while (bitmapExtensionBlocksOffset != 0)
            {
                // seek bitmap extension block offset in stream
                stream.Seek(partitionStartOffset + (long)bitmapExtensionBlocksOffset * blockSize,
                    SeekOrigin.Begin);

                // read bitmap extension block from stream
                var bitmapExtensionBlockBytes = await Amiga.Disk.ReadBlock(stream, blockSize);
                var bitmapExtensionBlock = BitmapExtensionBlockParser.Parse(bitmapExtensionBlockBytes);

                bitmapExtensionBlock.BitmapBlocks = await ReadBitmapBlocks(stream, partitionStartOffset,
                    bitmapExtensionBlock.BitmapBlockOffsets, blockSize);

                bitmapExtensionBlocksOffset = bitmapExtensionBlock.NextBitmapExtensionBlockPointer;

                bitmapExtensionBlocks.Add(bitmapExtensionBlock);
            }

            return bitmapExtensionBlocks;
        }
    }
}