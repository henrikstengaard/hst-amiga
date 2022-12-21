namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Blocks;
    using Exceptions;

    public static class Bitmap
    {
        public static async Task AdfReadBitmap(Volume vol, uint nBlock, RootBlock root)
        {
            int i;

            var mapSize = (uint)(nBlock / (vol.OffsetsPerBitmapBlock * 32));
            if (nBlock % (vol.OffsetsPerBitmapBlock * 32) != 0)
                mapSize++;
            vol.BitmapSize = mapSize;
            vol.BitmapTable = new BitmapBlock[mapSize];
            vol.BitmapBlocks = new uint[mapSize];
            vol.BitmapBlocksChg = new bool[mapSize];

            for (i = 0; i < mapSize; i++)
            {
                vol.BitmapBlocksChg[i] = false;
                vol.BitmapTable[i] = new BitmapBlock(vol.FileSystemBlockSize);
            }

            var j = 0;
            i = 0;
            uint nSect;
            while (i < Constants.BM_SIZE && root.BitmapBlockOffsets[i] != 0)
            {
                vol.BitmapBlocks[j] = nSect = root.BitmapBlockOffsets[i];
                Disk.ThrowExceptionIfSectorNumberInvalid(vol, nSect);

                vol.BitmapTable[j] = await Disk.ReadBitmapBlock(vol, nSect);
                j++;
                i++;
            }

            nSect = root.BitmapExtensionBlocksOffset;
            while (nSect != 0)
            {
                var bmExt = await Disk.ReadBitmapExtensionBlock(vol, nSect);
                i = 0;
                while (i < vol.OffsetsPerBitmapBlock && j < mapSize)
                {
                    nSect = bmExt.BitmapBlockOffsets[i];
                    Disk.ThrowExceptionIfSectorNumberInvalid(vol, nSect);

                    vol.BitmapBlocks[j] = nSect;

                    vol.BitmapTable[j] = await Disk.ReadBitmapBlock(vol, nSect);
                    i++;
                    j++;
                }

                nSect = bmExt.NextBitmapExtensionBlockPointer;
            }
        }

        public static async Task AdfUpdateBitmap(Volume vol)
        {
            var root = await Disk.ReadRootBlock(vol, vol.RootBlockOffset);

            root.BitmapFlags = Constants.BM_INVALID;
            await Disk.WriteRootBlock(vol, vol.RootBlockOffset, root);

            for (var i = 0; i < vol.BitmapSize; i++)
                if (vol.BitmapBlocksChg[i])
                {
                    await Disk.WriteBitmapBlock(vol, vol.BitmapBlocks[i], vol.BitmapTable[i]);
                    vol.BitmapBlocksChg[i] = false;
                }

            root.BitmapFlags = Constants.BM_VALID;
            root.Date = DateTime.Now;

            await Disk.WriteRootBlock(vol, vol.RootBlockOffset, root);
        }

        public static uint AdfGet1FreeBlock(Volume vol)
        {
            var block = AdfGetFreeBlocks(vol, 1);
            return block.Any() ? block[0] : uint.MaxValue; // -1
        }

        public static uint[] AdfGetFreeBlocks(Volume vol, uint nbSect)
        {
            var sectList = new List<uint>();
            var block = vol.RootBlockOffset;

            var i = 0;
            var diskFull = false;
            while (i < nbSect && !diskFull)
            {
                if (AdfIsBlockFree(vol, block))
                {
                    sectList.Add(block);
                    i++;
                }

                if (block + vol.FirstBlock == vol.LastBlock)
                {
                    block = 2;
                }
                else if (block == vol.RootBlockOffset - 1)
                {
                    diskFull = true;
                }
                else
                {
                    block++;
                }
            }

            if (diskFull)
            {
                throw new DiskFullException("Disk full");
            }

            for (var j = 0; j < nbSect; j++)
            {
                AdfSetBlockUsed(vol, sectList[j]);
            }

            return i == nbSect ? sectList.ToArray() : Array.Empty<uint>();
        }
        
        private static readonly uint[] BitMask =
        {
            0x1, 0x2, 0x4, 0x8,
            0x10, 0x20, 0x40, 0x80,
            0x100, 0x200, 0x400, 0x800,
            0x1000, 0x2000, 0x4000, 0x8000,
            0x10000, 0x20000, 0x40000, 0x80000,
            0x100000, 0x200000, 0x400000, 0x800000,
            0x1000000, 0x2000000, 0x4000000, 0x8000000,
            0x10000000, 0x20000000, 0x40000000, 0x80000000
        };

        public static void AdfSetBlockUsed(Volume vol, uint nSect)
        {
            var sectOfMap = nSect - vol.Reserved;
            var block = sectOfMap / (vol.OffsetsPerBitmapBlock * 32);
            var indexInMap = (sectOfMap / 32) % vol.OffsetsPerBitmapBlock;

            var oldValue = vol.BitmapTable[block].Map[indexInMap];

            vol.BitmapTable[block].Map[indexInMap] = oldValue & ~BitMask[sectOfMap % 32];
            vol.BitmapBlocksChg[block] = true;
        }

        public static bool AdfIsBlockFree(Volume vol, uint nSect)
        {
            var sectOfMap = nSect - vol.Reserved;
            var block = sectOfMap / (vol.OffsetsPerBitmapBlock * 32);
            var indexInMap = (sectOfMap / 32) % vol.OffsetsPerBitmapBlock;

            return (vol.BitmapTable[block].Map[indexInMap] & BitMask[sectOfMap % 32]) != 0;
        }

        public static void AdfSetBlockFree(Volume vol, uint nSect)
        {
            var sectOfMap = nSect - vol.Reserved;
            var block = sectOfMap / (vol.OffsetsPerBitmapBlock * 32);
            var indexInMap = (sectOfMap / 32) % vol.OffsetsPerBitmapBlock;

            var oldValue = vol.BitmapTable[ block ].Map[ indexInMap ];
            vol.BitmapTable[ block ].Map[ indexInMap ] = oldValue | BitMask[ sectOfMap % 32 ];
            vol.BitmapBlocksChg[ block ] = true;
        }
    }
}