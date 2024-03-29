﻿namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;
    using RigidDiskBlocks;

    public static class Pfs3Formatter
    {
        /// <summary>
        /// formats partition with professional file system (PFS3AIO v19.2)
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="partitionBlock"></param>
        /// <param name="diskName"></param>
        /// <exception cref="IOException"></exception>
        /// <exception cref="Exception"></exception>
        public static async Task FormatPartition(Stream stream, PartitionBlock partitionBlock, string diskName)
        {
#if DEBUG
            Pfs3Logger.Instance.Debug($"Pfs3Formatter: Formatting partition cylinders {partitionBlock.LowCyl} - {partitionBlock.HighCyl} with disk name '{diskName}'");
#endif
            if (!stream.CanWrite)
            {
                throw new IOException("Failed to format partition: Stream is not writable");
            }
            
            // format.c, FDSFormat()

            // g->firstblock = g->dosenvec->de_LowCyl * geom->dg_CylSectors;
            // g->lastblock = (g->dosenvec->de_HighCyl + 1) *  geom->dg_CylSectors - 1;
            
            var g = Init.CreateGlobalData(partitionBlock.Sectors, partitionBlock.BlocksPerTrack,
                partitionBlock.Surfaces, partitionBlock.LowCyl, partitionBlock.HighCyl, partitionBlock.NumBuffer,
                partitionBlock.Mask);
            g.stream = stream;
            
            /* remove error-induced soft protect */
            if (g.softprotect)
            {
                throw new IOException("ERROR_DISK_WRITE_PROTECTED");
            }

            if (g.softprotect && g.protectkey == ~0)
            {
                g.softprotect = false;
                g.protectkey = 0;
            }

            /* update dos envec and geom */
            // GetDriveGeometry (g);
            // ShowVersion (g);

            /* issue 00118: disk cannot exceed MAX_DISK_SIZE */
            if (g.TotalSectors > Constants.MAXDISKSIZE)
            {
                throw new Exception("totalSectors: ERROR_OBJECT_TOO_LARGE");
            }

            // Only 512, 1024, 2048 and 4096 block sizes are supported.
            // Last two require new large partition mode.
            if (g.blocksize < 512 || g.blocksize > 4096)
            {
                throw new Exception("sectorSize: ERROR_BAD_NUMBER");
            }
            
            // err = MakeBootBlock (g);
            // if (err != 0) {
            //     *error = err;
            //     return DOSFALSE;
            // }
            await MakeBootBlock(g);

            // if (!(rootblock = MakeRootBlock (diskname, g))) {
            //     *error = ERROR_NO_FREE_STORE;
            //     return DOSFALSE;
            // }
            RootBlock rootBlock;
            if ((rootBlock = MakeRootBlock(diskName, g)) == null)
            {
                throw new Exception("ERROR_NO_FREE_STORE");
            }

            g.RootBlock = rootBlock;

            /*  make volumedata BEFORE rext ! (bug 00135) */
            // g->currentvolume = volume = MakeVolumeData (rootblock, g);
            volumedata volume;
            g.currentvolume = volume = await Volume.MakeVolumeData(rootBlock, g);

            /* add extension */
            // if (!(rext = MakeFormatRBlkExtension (rootblock, g))) {
            //     *error = ERROR_NO_FREE_STORE;
            //     return DOSFALSE;			// rootblock extension could not be created
            // }
            g.currentvolume.rblkextension = MakeFormatRBlkExtension(rootBlock, g);
            rootBlock.Options |= RootBlock.DiskOptionsEnum.MODE_EXTENSION;

            await Init.InitModules(g.currentvolume, true, g);

            await MakeBitmap(g);

            uint i;
            uint? prev = null;
            do
            {
                i = await anodes.AllocAnode(0, g);
                if (prev.HasValue && prev.Value == i)
                {
                    throw new IOException("Format failed: Alloc node 0");
                }
                prev = i;
            } while (i < Constants.ANODE_ROOTDIR - 1);

            await MakeRootDir(g);

            // #if DELDIR
            //  /* add deldir */
            //  SetDeldir(2, g);
            //  rootblock->options |= MODE_DELDIR | MODE_SUPERDELDIR;
            //  g->dirty = TRUE;
            // #endif

            await Directory.SetDeldir(2, g);
            rootBlock.Options |= RootBlock.DiskOptionsEnum.MODE_DELDIR | RootBlock.DiskOptionsEnum.MODE_SUPERDELDIR;
            g.dirty = true;

            //
            await Update.UpdateDisk(g);
            Volume.FreeVolumeResources(volume, g);
            g.currentvolume = null;
            //
            // return DOSTRUE;
#if DEBUG
            Pfs3Logger.Instance.Debug($"Pfs3Formatter: Formatting done");
#endif
        }

/*
 * creates & writes the two bootblocks
 */
        public static async Task MakeBootBlock(globaldata g)
        {
            // struct bootblock *bbl;
            // ULONG error;

            var bbl = new BootBlock
            {
                disktype = Constants.ID_PFS_DISK
            };
            
            // if (!(bbl = AllocBufmem (2 * BLOCKSIZE, g)))
            //     return ERROR_NO_FREE_STORE;

// #if ACCESS_DETECT
// 	if (!detectaccessmode((UBYTE*)bbl, g))
// 		return ERROR_OBJECT_TOO_LARGE;
// #endif

            // memset (bbl, 0, 2*BLOCKSIZE);
            // bbl->disktype = ID_PFS_DISK;
            // error = RawWrite ((UBYTE *)bbl, 2, BOOTBLOCK1, g);
            var buffer = await BootBlockWriter.MakeBootBlock(bbl, (int)g.blocksize);
            if (!await Disk.RawWrite(g.stream, buffer, 2, Constants.BOOTBLOCK1, g))
            {
                throw new IOException("BOOTBLOCK write error");
            }
            // FreeBufmem (bbl, g);
            // return error;
        }
        
        public static RootBlock MakeRootBlock(string diskName, globaldata g)
        {
            var rootBlock = new RootBlock
            {
                DiskType = Constants.ID_PFS_DISK,
                Datestamp = 1,
                CreationDate = DateTime.UtcNow,
                Protection = 0xf0,
                FirstReserved = 2,
                DiskName = diskName,
                DiskSize = g.TotalSectors,
                Options = RootBlock.DiskOptionsEnum.MODE_HARDDISK | RootBlock.DiskOptionsEnum.MODE_SPLITTED_ANODES |
                          RootBlock.DiskOptionsEnum.MODE_DIR_EXTENSION |
                          RootBlock.DiskOptionsEnum.MODE_SIZEFIELD | RootBlock.DiskOptionsEnum.MODE_DATESTAMP |
                          RootBlock.DiskOptionsEnum.MODE_EXTROVING |
                          RootBlock.DiskOptionsEnum.MODE_LONGFN
            };

            // determine reserved blocksize
            uint resblocksize = 1024;
            if (g.TotalSectors > Constants.MAXSMALLDISK)
            {
                rootBlock.Options |= RootBlock.DiskOptionsEnum.MODE_SUPERINDEX;
                g.SuperMode = true;
                if (g.TotalSectors > Constants.MAXDISKSIZE1K)
                {
                    resblocksize = 2048;
                    if (g.TotalSectors > Constants.MAXDISKSIZE2K)
                    {
                        resblocksize = 4096;
                    }

                    rootBlock.DiskType = Constants.ID_PFS2_DISK;
                    //NormalErrorMsg(AFS_WARNING_EXPERIMENTAL_DISK, NULL, 1);
                }
            }

            Init.Initialize(g);
            rootBlock.Options |= RootBlock.DiskOptionsEnum.MODE_HARDDISK;

            Lru.InitLRU(g, (ushort)resblocksize);

            // Use large disk modes if block size is larger than 1024
            if (g.blocksize > resblocksize)
            {
                resblocksize = g.blocksize;
                rootBlock.DiskType = Constants.ID_PFS2_DISK;
                //NormalErrorMsg(AFS_WARNING_EXPERIMENTAL_DISK, NULL, 1);
            }

            var resCluster = resblocksize / g.blocksize;
            rootBlock.ReservedBlksize = (ushort)resblocksize;

            var numReserved = CalcNumReserved(g, resblocksize);

            rootBlock.LastReserved = (uint)(resCluster * numReserved + rootBlock.FirstReserved - 1);
            rootBlock.ReservedFree = (uint)numReserved;
            rootBlock.BlocksFree = (uint)(g.TotalSectors - resCluster * numReserved - rootBlock.FirstReserved);
            rootBlock.AlwaysFree = rootBlock.BlocksFree / 20;

            MakeReservedBitmap(rootBlock, numReserved, g); // sets reserved_free & rblkcluster too

            return rootBlock;
        }

        public static async Task MakeBitmap(globaldata g)
        {
            var alloc_data = g.glob_allocdata;

            /* use no_bmb as calculated by InitAllocation */
            for (uint i = 0; i < alloc_data.no_bmb; i++)
            {
                await Allocation.NewBitmapBlock(i, g);
            }
        }

        public static async Task MakeRootDir(globaldata g)
        {
            CachedBlock blk;
            uint blocknr, anodenr;

            blocknr = Allocation.AllocReservedBlock(g);
            anodenr = await anodes.AllocAnode(0, g);
            blk = await Directory.MakeDirBlock(blocknr, anodenr, anodenr, 0, g);
        }

/* Create rootblockextension. Needed for AFS 2.3 options.
 */
        public static bool MakeRBlkExtension (globaldata g)
        {
            volumedata volume = g.currentvolume;
            uint blocknr;

            if ((blocknr = Allocation.AllocReservedBlock(g)) == 0)
                return false;

            g.RootBlock.Extension = blocknr;
            if ((volume.rblkextension = MakeFormatRBlkExtension(g.RootBlock, g)) == null)
            {
                Allocation.FreeReservedBlock (blocknr, g);
                return false;
            }

            g.RootBlock.Options |= RootBlock.DiskOptionsEnum.MODE_EXTENSION;
            volume.rootblockchangeflag = true;
            return true;
        }
        
        public static CachedBlock MakeFormatRBlkExtension(RootBlock rootBlock, globaldata g)
        {
            // if (!(rext = AllocBufmem (sizeof(struct cachedblock) + rbl->reserved_blksize, g)))
            // return FALSE;
            //memset (rext, 0, sizeof(struct cachedblock) + rbl->reserved_blksize);

            var rext = new CachedBlock()
            {
                volume = g.currentvolume,
                blocknr = (uint)rootBlock.Extension,
                changeflag = true,
                blk = new rootblockextension
                {
                    id = Constants.EXTENSIONID,
                    pfs2version = (Constants.VERNUM << 16) + Constants.REVNUM,
                    RootDate = rootBlock.CreationDate,
                    fnsize = g.fnsize = 107 // original default 32
                }
            };

            g.dirty = true;
            return rext;
        }

        private static readonly uint[][] schijf = { 
            new uint[]{20480,20},
            new uint[]{51200,30},
            new uint[]{512000,40},
            new uint[]{1048567,50},
            new uint[]{10000000,70},
            new uint[]{0xffffffff,80}
        };
    
        public static uint CalcNumReserved(globaldata g, uint resblocksize)
        {
            uint taken, i;

            var temp = g.TotalSectors * (g.blocksize / 512);
            temp /= (resblocksize / 512);
            taken = 0;

            for (i = 0; temp > schijf[i][0]; i++)
            {
                taken += schijf[i][0]/schijf[i][1];
                temp -= schijf[i][0];
            }
            taken += temp/schijf[i][1];
            taken += 10;
            taken = Math.Min(Constants.MAXNUMRESERVED, taken);
            taken = (uint)((taken + 31) & ~0x1f);		/* multiple of 32 */

            return taken;
        }
        
        /// <summary>
        /// Calculate number of reserved blocks used for root block and reserved bitmap block
        /// </summary>
        /// <param name="rbl"></param>
        /// <param name="numReserved"></param>
        /// <returns></returns>
        public static int CalculateRootBlockAndReservedBitmapBlockCount(RootBlock rbl, uint numReserved)
        {
            /* calculate number of 1024 byte blocks */
            var numblocks = 1;
            for (var i = 125; i < numReserved / 32; i += 256)
            {
                numblocks++;
            }
            
            // convert to number of reserved blocks and allocate
            return ((1024 * numblocks) + rbl.ReservedBlksize - 1) / rbl.ReservedBlksize;
        }
        
        /* makes reserved bitmap and allocates rootblockextension */
        public static void MakeReservedBitmap(RootBlock rbl, uint numReserved, globaldata g)
        {
            // struct bitmapblock *bmb;
            // struct rootblock *newrootblock;
            // int *bitmap, numblocks, i, last, cluster, rescluster;

            /* calculate number of 1024 byte blocks */
            var numblocks = CalculateRootBlockAndReservedBitmapBlockCount(rbl, numReserved);
            rbl.ReservedFree -= (uint)numblocks;

            // convert to number of sectors
            var rescluster = rbl.ReservedBlksize / g.blocksize;
            var cluster = rbl.RblkCluster = (ushort)(rescluster * numblocks);

            /* reallocate rootblock */
            //var newrootblock = AllocBufmemR(cluster << g.blockshift, g);
            //memset (newrootblock, 0, cluster << BLOCKSHIFT);
            //memcpy (newrootblock, *rbl, BLOCKSIZE);
            //FreeBufmem(*rbl, g);
            //*rbl = newrootblock;
            //bmb = (bitmapblock_t *)(*rbl+1);		/* bitmap directly behind rootblock */
            var reservedLongsPerBmb = (int)(numReserved / 32 + 1);
            var bmb = new BitmapBlock(reservedLongsPerBmb);

            /* init bitmapblock header */
            bmb.id = Constants.BMBLKID;
            bmb.seqnr = 0;

            /* fill bitmap */
            //bitmap = bmb->bitmap;
            for (var i = 0; i < reservedLongsPerBmb; i++)
            {
                // *bitmap++ = ~0; // ~ operator performs bitwise complement =  contain the highest possible value (signed to unsigned conversion)
                bmb.bitmap[i] = uint.MaxValue;
            }

            /* the last border */
            uint last = 0;
            for (var i = 0; i < numReserved % 32; i++)
            {
                last |= 0x80000000 >> i;
            }
            bmb.bitmap[bmb.bitmap.Length - 1] = last;

            /* allocate taken blocks + rootblock extension (de + 1)
             * The reserved area starts with the rootblock.
             * Convert numblocks from 1K blocks to actual reserved area blocks
             * */
            for (var i = 0; i < numblocks + 1; i++)
            {
                bmb.bitmap[i / 32] ^= 0x80000000 >> (i % 32);
            }

            /* rootblock extension position */
            rbl.Extension = rbl.FirstReserved + cluster;
            rbl.ReservedFree--;

            // bitmap directly behind rootblock
            rbl.ReservedBitmapBlock = bmb;
        }
    }
}