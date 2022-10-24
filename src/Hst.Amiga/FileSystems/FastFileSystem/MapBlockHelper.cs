namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using System.Collections.Generic;
    using Blocks;

    public static class MapBlockHelper
    {
        /// <summary>
        /// Convert map entry to block free map. One bit is used per block. If the bit is set 1, the block is free. Blocks set 0 are used.
        /// UInt:   4294918143
        /// Bytes:  |- FF -||- FF -||- 3F -||- FF -|
        /// Converts to
        ///           3         2         1 
        /// Blocks: 21098765432109876543210987654321
        /// Bits:   11111111111111110011111111111111
        /// </summary>
        /// <param name="mapEntry"></param>
        /// <returns></returns>
        public static bool[] ConvertUInt32ToBlockFreeMap(uint mapEntry)
        {
            var blockFreeMap = new List<bool>();
            for (var bitOffset = 0; bitOffset < 32; bitOffset++)
            {
                blockFreeMap.Add((mapEntry & (1 << bitOffset)) != 0);
            }
            return blockFreeMap.ToArray();
        }

        /// <summary>
        /// Convert block free map to map entry. One bit is used per block. If the bit is set 1, the block is free. Blocks set 0 are used.
        ///           3         2         1 
        /// Blocks: 21098765432109876543210987654321
        /// Bits:   11111111111111110011111111111111
        /// Converts to
        /// UInt:   4294918143
        /// Bytes:  |- FF -||- FF -||- 3F -||- FF -|
        /// </summary>
        /// <param name="blockFreeMap"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static uint ConvertBlockFreeMapToUInt32(bool[] blockFreeMap, int offset = 0)
        {
            uint mapEntry = 0;
            for (var i = 0; i < Constants.BitmapsPerULong && offset + i < blockFreeMap.Length; i++)
            {
                if (!blockFreeMap[offset + i])
                {
                    continue;
                }
                
                mapEntry |= 1U << i;
            }
            return mapEntry;
        }

        /// <summary>
        /// set block state in bitmap block
        /// </summary>
        /// <param name="bitmapBlock"></param>
        /// <param name="block"></param>
        /// <param name="state"></param>
        public static void SetBlock(BitmapBlock bitmapBlock, int block, BitmapBlock.BlockState state)
        {
            var mapEntryOffset = Convert.ToInt32(Math.Floor((double)block / 32));

            if (mapEntryOffset >= bitmapBlock.Map.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(block), $"Block {block} is out of range");
            }
            
            var mapEntryBlockOffset = block % 32;
            
            bitmapBlock.Map[mapEntryOffset] = state == BitmapBlock.BlockState.Free
                ? bitmapBlock.Map[mapEntryOffset] | (uint)(1 << mapEntryBlockOffset)
                : bitmapBlock.Map[mapEntryOffset] ^ (uint)(1 << mapEntryBlockOffset);
        }
    }
}