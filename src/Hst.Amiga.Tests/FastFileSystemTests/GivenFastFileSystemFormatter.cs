namespace Hst.Amiga.Tests.FastFileSystemTests
{
    using System;
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
    using File = System.IO.File;
    using FileMode = System.IO.FileMode;

    public class GivenFastFileSystemFormatter
    {
        private readonly byte[] dos3DosType = { 0x44, 0x4f, 0x53, 0x3 };

        [Fact]
        public async Task WhenFloppyDiskFileThenRootBlockAndBitmapBlocksAreCreated()
        {
            var adfPath = "dos.adf";
            const uint lowCyl = FloppyDiskConstants.DoubleDensity.LowCyl;
            const uint highCyl = FloppyDiskConstants.DoubleDensity.HighCyl;
            const uint reservedBlocks = FloppyDiskConstants.DoubleDensity.ReservedBlocks; 
            const uint surfaces = FloppyDiskConstants.DoubleDensity.Heads;
            const uint blocksPerTrack = FloppyDiskConstants.DoubleDensity.Sectors;
            const uint fileSystemBlockSize = FloppyDiskConstants.BlockSize;
            
            // arrange - create double density floppy disk
            await using var adfStream = File.Open(adfPath, FileMode.Create, FileAccess.ReadWrite);
            adfStream.SetLength(FloppyDiskConstants.DoubleDensity.Size);
            
            // act - format first partition
            await FastFileSystemFormatter.Format(adfStream, lowCyl, highCyl, reservedBlocks, 
                surfaces, blocksPerTrack, fileSystemBlockSize, dos3DosType, "Workbench");

            // arrange - calculate root block offset
            var rootBlockOffset = OffsetHelper.CalculateRootBlockOffset(lowCyl, highCyl,
                reservedBlocks, surfaces, blocksPerTrack);

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
            var rootBlock = RootBlockReader.Parse(rootBlockBytes);
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
                var bitmapBlock = BitmapBlockReader.Parse(bitmapBlockBytes);

                // create expected blocks free map, used blocks are set false and free blocks are set true:
                // - root block
                // - bitmap blocks
                // note reserved blocks in beginning of partition are not part of bitmap blocks
                var expectedBlocksFreeMap = new bool[bitmapsPerBitmapBlockCount];
                for (var b = 0; b < bitmapsPerBitmapBlockCount; b++)
                {
                    var blockOffset = reservedBlocks + (bitmapsPerBitmapBlockCount * i) + b;
                    expectedBlocksFreeMap[b] = !((blockOffset >= rootBlockOffset &&
                                                  blockOffset <= rootBlockOffset + bitmapBlocksCount) || blockOffset >= blocks + reservedBlocks);
                }

                var expectedMapEntries = expectedBlocksFreeMap.ChunkBy(Constants.BitmapsPerULong)
                    .Select(x => MapBlockHelper.ConvertBlockFreeMapToUInt32(x.ToArray())).ToArray();

                Assert.Equal(expectedMapEntries, bitmapBlock.Map);
            }
        }
        
        [Fact]
        public async Task WhenFormattingHardDiskFileThenRootBlockAndBitmapBlocksAreCreated()
        {
            var path = "dos3.hdf";

            // arrange - create 10mb hdf file with 1 partition using DOS3 dos type 
            var rigidDiskBlock = await RigidDiskBlock
                .Create(10.MB().ToUniversalSize())
                .AddFileSystem(dos3DosType,
                    Encoding.ASCII.GetBytes(
                        "$VER: FastFileSystem 1.0 (12/12/22) ")) // dummy fast file system used for testing
                .AddPartition("DH0", bootable: true)
                .WriteToFile(path);

            var partition = rigidDiskBlock.PartitionBlocks.First();

            // act - format first partition
            await using var hdfStream = File.Open(path, FileMode.Create, FileAccess.ReadWrite);
            await FastFileSystemFormatter.FormatPartition(hdfStream, partition, "Workbench");

            // arrange - calculate root block offset
            var rootBlockOffset = OffsetHelper.CalculateRootBlockOffset(partition.LowCyl, partition.HighCyl,
                partition.Reserved, partition.Surfaces, partition.BlocksPerTrack);

            // arrange - calculate partition start offset
            var blocksPerCylinder = rigidDiskBlock.Heads * rigidDiskBlock.Sectors;
            var cylinderSize = blocksPerCylinder * rigidDiskBlock.BlockSize;
            var partitionStartOffset = cylinderSize * partition.LowCyl;

            // assert - dos type is present at partition start offset
            hdfStream.Seek(partitionStartOffset, SeekOrigin.Begin);
            var blockBytes = await Amiga.Disk.ReadBlock(hdfStream, (int)partition.FileSystemBlockSize);
            for (var i = 0; i < dos3DosType.Length; i++)
            {
                Assert.Equal(dos3DosType[i], blockBytes[i]);
            }

            // assert - root block is present at root block offset and matches disk name
            hdfStream.Seek(partitionStartOffset + rootBlockOffset * partition.FileSystemBlockSize, SeekOrigin.Begin);
            var rootBlockBytes = await Amiga.Disk.ReadBlock(hdfStream, (int)partition.FileSystemBlockSize);
            var rootBlock = RootBlockReader.Parse(rootBlockBytes);
            Assert.Equal("Workbench", rootBlock.DiskName);

            // calculate number of blocks partition contains
            var cylinders = partition.HighCyl - partition.LowCyl + 1;
            var blocks = (cylinderSize * cylinders) / partition.FileSystemBlockSize;

            // calculate number of bitmaps each bitmap block represents
            var bitmapsPerBitmapBlockCount =
                FileSystems.FastFileSystem.BlockHelper.CalculateBitmapsPerBitmapBlockCount(partition
                    .FileSystemBlockSize);

            // calculate number of bitmap blocks are required for all blocks
            var bitmapBlocksCount = Math.Ceiling((double)blocks / bitmapsPerBitmapBlockCount);
            
            // assert bitmap blocks
            for (var i = 0; i < bitmapBlocksCount; i++)
            {
                // assert - root block has bitmap block offset
                Assert.Equal(rootBlock.BitmapBlockOffsets[i], rootBlockOffset + i + 1);
                
                // seek bitmap block offset in hdf stream
                hdfStream.Seek(partitionStartOffset + (rootBlockOffset + 1 + i) * partition.FileSystemBlockSize,
                    SeekOrigin.Begin);
                
                // read bitmap block from hdf stream
                var bitmapBlockBytes = await Amiga.Disk.ReadBlock(hdfStream, (int)partition.FileSystemBlockSize);
                var bitmapBlock = BitmapBlockReader.Parse(bitmapBlockBytes);

                // create expected blocks free map, used blocks are set false and free blocks are set true:
                // - root block
                // - bitmap blocks
                // note reserved blocks in beginning of partition are not part of bitmap blocks
                var expectedBlocksFreeMap = new bool[bitmapsPerBitmapBlockCount];
                for (var b = 0; b < bitmapsPerBitmapBlockCount; b++)
                {
                    var blockOffset = partition.Reserved + (bitmapsPerBitmapBlockCount * i) + b;
                    expectedBlocksFreeMap[b] = !((blockOffset >= rootBlockOffset &&
                                                  blockOffset <= rootBlockOffset + bitmapBlocksCount) || blockOffset >= blocks + partition.Reserved);
                }
                
                var expectedMapEntries = expectedBlocksFreeMap.ChunkBy(Constants.BitmapsPerULong)
                    .Select(x => MapBlockHelper.ConvertBlockFreeMapToUInt32(x.ToArray())).ToArray();
                
                Assert.Equal(expectedMapEntries, bitmapBlock.Map);
            }
        }
    }
}