namespace Hst.Amiga.Tests.FastFileSystemTests
{
    using System;
    using FileSystems.FastFileSystem;
    using Xunit;

    public class GivenMapBlockHelper
    {
        [Fact]
        public void WhenBlocksAreAllocatedThenBytesDoesntHaveBitSet()
        {
            // arrange - create map for 32 blocks with all block set as free
            var blockFreeMap = new bool[32];
            for (var i = 0; i < blockFreeMap.Length; i++)
            {
                blockFreeMap[i] = true;
            }

            // arrange - set block 15 & 16 as allocated
            blockFreeMap[14] = false;
            blockFreeMap[15] = false;

            // act - convert block free map to byte array
            var mapEntry = MapBlockHelper.ConvertBlockFreeMapToUInt32(blockFreeMap);
            
            // bits: 11111111111111110011111111111111

            // assert - map entry has bit for block 15 & 16 set to false 
            Assert.Equal(UInt32.MaxValue - (1 << 14) - (1 << 15), mapEntry);
        }
        
        [Fact]
        public void WhenFirstBlockIsAllocatedThenByteDoesntHaveBitSet()
        {
            // arrange - create map for 32 blocks with all block set as free
            var blockFreeMap = new bool[32];
            for (var i = 0; i < blockFreeMap.Length; i++)
            {
                blockFreeMap[i] = true;
            }

            // arrange - indicate block 1 is free
            blockFreeMap[0] = false;

            // act - convert block free map to map entry
            var mapEntry = MapBlockHelper.ConvertBlockFreeMapToUInt32(blockFreeMap);

            // assert - map entry doesn't have bit for block 1 set 
            Assert.Equal(UInt32.MaxValue - 1, mapEntry);
        }

        private readonly byte[] bits = { 1, 2, 4, 8, 16, 32, 64, 128 };

        [Fact]
        public void WhenBlockIsAllocatedThenMappedByteDoesntHaveBitSet()
        {
            for (var bit = 0; bit < 8; bit++)
            {
                // arrange - create map for 8 blocks with block set allocated for bit 
                var blockFreeMap = new bool[8];
                for (var block = 0; block < blockFreeMap.Length; block++)
                {
                    blockFreeMap[block] = block != bit;
                }
                
                // act - convert block free map to map entry
                var mapEntry = MapBlockHelper.ConvertBlockFreeMapToUInt32(blockFreeMap);

                // assert - bit is not set in map entry
                Assert.Equal(255U - bits[bit], mapEntry);
            }
        }
        
        [Fact]
        public void WhenBlocksInBlockFreeMapAreSetUsedThenMapEntryDoesntHaveBitsSet()
        {
            for (var bit = 1; bit < 8; bit++)
            {
                // arrange - create block free map for 8 blocks with blocks set used (true) 
                var map = new bool[8];
                for (var block = 0; block < map.Length; block++)
                {
                    map[block] = block != bit;
                }
                
                // arrange - always set block 1 to used
                map[0] = false;
                
                // act - convert block free map to map entry
                var mapEntry = MapBlockHelper.ConvertBlockFreeMapToUInt32(map);

                // assert - bits are not set in map bytes
                Assert.Equal(255U - bits[0] - bits[bit], mapEntry);
            }
        }

        [Fact]
        public void WhenConvertUInt32ToBlockFreeMapThen()
        {
            // arrange - create expected block free map with block 15 & 16 used (false) and rest free (true)
            //           3         2         1
            //        21098765432109876543210987654321
            // bits:  11111111111111110011111111111111
            // bytes: <-0xff-><-0xff-><-0x3f-><-0xff->
            var mapEntry = UInt32.MaxValue - (1 << 14) - (1 << 15);
            
            // act - convert bytes to block free map
            var blockFreeMap = MapBlockHelper.ConvertUInt32ToBlockFreeMap(mapEntry);
            
            // assert - create expected block free map with block 15 & 16 as allocated
            var expectedBlockFreeMap = new bool[32];
            for (var i = 0; i < expectedBlockFreeMap.Length; i++)
            {
                expectedBlockFreeMap[i] = true;
            }
            expectedBlockFreeMap[14] = false; // set block 15 used
            expectedBlockFreeMap[15] = false; // set block 16 used

            // assert - block free map matches expected block free map
            Assert.Equal(expectedBlockFreeMap, blockFreeMap);
        }

        [Fact]
        public void WhenConvertMapEntryWithBlock3And5FreeToBlockFreeMapThenMapsMatch()
        {
            // arrange - create expected block free map with block 3 & 5 free (true) and rest used (false)
            //           3         2         1
            // blocks: 21098765432109876543210987654321
            // bits:   00000000000000000000000000010100
            var expectedBlockFreeMap = new bool[32];
            for (var i = 0; i < expectedBlockFreeMap.Length; i++)
            {
                expectedBlockFreeMap[i] = false;
            }
            expectedBlockFreeMap[2] = true; // set block 3 free
            expectedBlockFreeMap[4] = true; // set block 5 free

            // arrange - map entry 20 = block 3 & 5 free (bits 4 & 16 is true) and rest used (false) 
            // 1 left shift 2 = block 3
            // 1 left shift 4 = block 5
            var mapEntry = (uint)((1 << 2) | (1 << 4));
            
            // act - convert map entry to block free map
            var blockFreeMap = MapBlockHelper.ConvertUInt32ToBlockFreeMap(mapEntry);

            // assert - block free map matches expected block free map
            Assert.Equal(expectedBlockFreeMap, blockFreeMap);
        }
        
        [Fact]
        public void WhenConvertMapEntryWithBlock3And5UsedToBlockFreeMapThenMapsMatch()
        {
            // arrange - create expected block free map with block 3 & 5 used (bits 4 & 16 is false) and rest free (true)
            //           3         2         1
            // blocks: 21098765432109876543210987654321
            // bits:   11111111111111111111111111101011
            var expectedBlockFreeMap = new bool[32];
            for (var i = 0; i < expectedBlockFreeMap.Length; i++)
            {
                expectedBlockFreeMap[i] = true;
            }
            expectedBlockFreeMap[2] = false;
            expectedBlockFreeMap[4] = false;

            // arrange - map entry uint max value - 20 = block 3 & 5 used (bits 4 & 16 is false) and rest free (true)
            // 1 left shift 2 = block 3
            // 1 left shift 4 = block 5
            var mapEntry = UInt32.MaxValue - (1 << 2) - (1 << 4);
            
            // act - convert map entry to block free map
            var blockFreeMap = MapBlockHelper.ConvertUInt32ToBlockFreeMap(mapEntry);

            // assert - block free map matches expected block free map
            Assert.Equal(expectedBlockFreeMap, blockFreeMap);
        }

        [Fact]
        public void WhenSetBlock1UsedInBitmapBlockThenMapEntryHasBitSetToFalse()
        {
            // arrange - block 1
            const int block = 1;
            
            // arrange - create bitmap block with 127 maps
            var bitmapBlock = new BitmapBlock(512);
            
            // act - set block 1 used
            MapBlockHelper.SetBlock(bitmapBlock, block - 1, BitmapBlock.BlockState.Used);
            
            // assert - map entry 0 has block 1 set to used (bit 1 set to 0)
            //           3         2         1
            // blocks: 21098765432109876543210987654321
            // bits:   11111111111111111111111111111110
            Assert.Equal(uint.MaxValue - 1, bitmapBlock.Map[0]);
        }

        [Fact]
        public void WhenSetBlock1FreeInBitmapBlockThenMapEntryHasBitSetToTrue()
        {
            // arrange - block 1
            const int block = 1;
            
            // arrange - create bitmap block with 127 maps
            var bitmapBlock = new BitmapBlock(512);
            
            // arrange - set all blocks used for map entry 0
            bitmapBlock.Map[0] = 0;
            
            // act - set block 1 free
            MapBlockHelper.SetBlock(bitmapBlock, block - 1, BitmapBlock.BlockState.Free);
            
            // assert - map entry 0 has block 1 set to free (bit 1 set to 1)
            //           3         2         1
            // blocks: 21098765432109876543210987654321
            // bits:   00000000000000000000000000000001
            Assert.Equal(1U, bitmapBlock.Map[0]);
        }
        
        [Fact]
        public void WhenSetBlock105UsedInBitmapBlockThenMapEntryHasBitSetToFalse()
        {
            // arrange - block, map entry and block offset
            const int block = 105;
            const int mapEntryOffset = 3; // 105 / 32
            const int blockOffset = 9; // 105 % 32
            
            // arrange - create bitmap block with 127 maps
            var bitmapBlock = new BitmapBlock(512);
            
            // act - set block 105 used
            MapBlockHelper.SetBlock(bitmapBlock, block - 1, BitmapBlock.BlockState.Used);
            
            // assert - map entry 3 has block 9 set to used (bit 9 set to 0)
            //           3         2         1
            // blocks: 21098765432109876543210987654321
            // bits:   11111111111111111111111011111111
            Assert.Equal(uint.MaxValue - (1 << (blockOffset - 1)), bitmapBlock.Map[mapEntryOffset]);
        }
        
        [Fact]
        public void WhenSetBlock105FreeInBitmapBlockThenMapEntryHasBitSetToTrue()
        {
            // arrange - block, map entry and block offset
            const int block = 105;
            const int mapEntryOffset = 3; // 105 / 32
            const int blockOffset = 9; // 105 % 32
            
            // arrange - create bitmap block with 127 maps
            var bitmapBlock = new BitmapBlock(512);
            
            // arrange - set all blocks used for map entry 0
            bitmapBlock.Map[mapEntryOffset] = 0;
            
            // act - set block 105 free
            MapBlockHelper.SetBlock(bitmapBlock, block - 1, BitmapBlock.BlockState.Free);
            
            // assert - map entry 3 has block 9 set to free (bit 9 set to 1)
            //           3         2         1
            // blocks: 21098765432109876543210987654321
            // bits:   00000000000000000000000100000000
            Assert.Equal((uint)(1 << (blockOffset - 1)), bitmapBlock.Map[mapEntryOffset]);
        }
    }
}