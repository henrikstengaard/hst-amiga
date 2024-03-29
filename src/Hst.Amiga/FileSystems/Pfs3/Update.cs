﻿namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;

    public static class Update
    {
/* --> part of update
 * marks a directory or anodeblock dirty. Nothing happens if it already
 * was dirty. If it wasn't, the block will be reallocated and marked dirty.
 * If the reallocation fails, an error is displayed.
 *
 * result: TRUE = was clean; FALSE: was already dirty
 * 
 * LOCKing the block until next packet proves to be too restrictive,
 * so unlock afterwards.
 */
        public static async Task<bool> MakeBlockDirty(CachedBlock blk, globaldata g)
        {
#if DEBUG
            Pfs3Logger.Instance.Debug(
                $"Update: MakeBlockDirty, block nr = {blk.blocknr}, block type '{(blk.blk == null ? "null" : blk.blk.GetType().Name)}'");
#endif
            uint blocknr;
            ushort oldlock;

            if (!blk.changeflag)
            {
                g.dirty = true;
                oldlock = blk.used;
                Cache.LOCK(blk, g);

                blocknr = Allocation.AllocReservedBlock(g);
                if (blocknr != 0)
                {
                    blk.oldblocknr = blk.blocknr;
                    blk.blocknr = blocknr;
                    await UpdateBlocknr(blk, blocknr, g);
                }
                else
                {
// #ifdef BETAVERSION
//                     ErrorMsg(AFS_BETA_WARNING_2, NULL, g);
// #endif
                    blk.changeflag = true;
                }

                blk.used = oldlock; // unlock block
                return true;
            }
            else
            {
                return false;
            }
        }

        public static async Task UpdateBlocknr(CachedBlock blk, uint newblocknr, globaldata g)
        {
            switch (blk.blk.id)
            {
                case Constants.DBLKID: /* dirblock */
                    await UpdateDBLK(blk, newblocknr, g);
                    break;

                case Constants.ABLKID: /* anodeblock */
                    await UpdateABLK(blk, newblocknr, g);
                    break;

                case Constants.IBLKID: /* indexblock */
                    await UpdateIBLK(blk, newblocknr, g);
                    break;

                case Constants.BMBLKID: /* bitmapblock */
                    await UpdateBMBLK(blk, newblocknr, g);
                    break;

                case Constants.BMIBLKID: /* bitmapindexblock */
                    UpdateBMIBLK(blk, newblocknr, g);
                    break;

                case Constants.EXTENSIONID: /* rootblockextension */
                    await UpdateRBlkExtension(blk, newblocknr, g);
                    break;

                case Constants.DELDIRID: /* deldir */
                    await UpdateDELDIR(blk, newblocknr, g);
                    break;

                case Constants.SBLKID: /* superblock */
                    UpdateSBLK(blk, newblocknr, g);
                    break;
            }
        }

        public static async Task UpdateDBLK(CachedBlock blk, uint newblocknr, globaldata g)
        {
            // update dir block dictionary from old to new block nr
            if (g.currentvolume.dirblks.ContainsKey(blk.oldblocknr))
            {
                g.currentvolume.dirblks.Remove(blk.oldblocknr);
                g.currentvolume.dirblks.Add(newblocknr, blk);
            }
#if DEBUG
            Pfs3Logger.Instance.Debug($"Update: UpdateDBLK, oldblocknr = {blk.oldblocknr}, newblocknr = {newblocknr}");
#endif
            CachedBlock dblk = blk;
            canode anode = new canode();
            uint oldblocknr = dblk.oldblocknr;

            Cache.LOCK(blk, g);

            /* get old anode (all 1-block anodes) */
            await anodes.GetAnode(anode, dblk.dirblock.anodenr, g);
            while (anode.blocknr != oldblocknr && anode.next != 0)
            {
                //anode.next purely safety
                await anodes.GetAnode(anode, anode.next, g);
            }

            /* change it.. */
            if (anode.blocknr != oldblocknr)
            {
#if DEBUG
                Pfs3Logger.Instance.Debug(
                    $"Update: UpdateDBLK, anode.blocknr = {anode.blocknr}, dblk.blocknr = {dblk.blocknr}");
#endif
                // DB(Trace(4, "UpdateDBLK", "anode.blocknr=%ld, dblk->blocknr=%ld\n",
                //     anode.blocknr, dblk->blocknr));
                // ErrorMsg (AFS_ERROR_CACHE_INCONSISTENCY, NULL, g);
                throw new IOException("AFS_ERROR_CACHE_INCONSISTENCY");
            }

            /* This must happen AFTER anode correction, because Update() could be called,
             * causing trouble (invalid checkpoint: dirblock uptodate, anode not)
             */
            blk.changeflag = true;
            anode.blocknr = newblocknr;
            await anodes.SaveAnode(anode, anode.nr, g);

            //Macro.ReHash(blk, g.currentvolume.dirblks, Constants.HASHM_DIR, g);
        }

        public static async Task UpdateABLK(CachedBlock blk, uint newblocknr, globaldata g)
        {
            // update anode block dictionary from old to new block nr
            if (g.currentvolume.anblks.ContainsKey(blk.oldblocknr))
            {
                g.currentvolume.anblks.Remove(blk.oldblocknr);
                g.currentvolume.anblks.Add(newblocknr, blk);
            }
            
            //struct cindexblock *index;
            uint indexblknr, indexoffset, temp;
            var andata = g.glob_anodedata;

            blk.changeflag = true;
            temp = blk.ANodeBlock.seqnr;
            indexblknr = temp / andata.indexperblock;
            indexoffset = temp % andata.indexperblock;

            /* this one should already be in the cache */
            var index = await anodes.GetIndexBlock((ushort)indexblknr, g);
            if (index == null)
            {
                throw new IOException("UpdateABLK, GetIndexBlock returned NULL!");
            }

            // DBERR(if (!index) ErrorTrace(5,"UpdateABLK", "GetIndexBlock returned NULL!"));

#if DEBUG
            Pfs3Logger.Instance.Debug($"Update: UpdateABLK, oldblocknr = {blk.oldblocknr}, newblocknr = {newblocknr}");
#endif
            if (index.IndexBlock.index[indexoffset] != blk.oldblocknr)
            {
                throw new IOException(
                    $"Update anode block at index offset {indexoffset} doesn't have expected old block nr {blk.oldblocknr} but instead block nr {index.IndexBlock.index[indexoffset]}");
            }

            index.IndexBlock.index[indexoffset] = (int)newblocknr;
            await MakeBlockDirty(index, g);
            //Macro.ReHash(blk, g.currentvolume.anblks, Constants.HASHM_ANODE, g);
        }

        public static async Task UpdateIBLK(CachedBlock blk, uint newblocknr, globaldata g)
        {
            // update index block dictionary from old to new block nr
            if (g.currentvolume.indexblks.ContainsKey(blk.oldblocknr))
            {
                g.currentvolume.indexblks.Remove(blk.oldblocknr);
                g.currentvolume.indexblks.Add(newblocknr, blk);
            }
            
            // struct cindexblock *superblk;
            uint temp;
            var andata = g.glob_anodedata;

            blk.changeflag = true;
            if (g.SuperMode)
            {
                temp = Init.divide(blk.IndexBlock.seqnr, andata.indexperblock);
                var superblk = await anodes.GetSuperBlock((ushort)temp /* & 0xffff */, g);

                // DBERR(if (!superblk) ErrorTrace(5,"UpdateIBLK", "GetSuperBlock returned NULL!"));
                if (superblk == null)
                {
                    throw new IOException("UpdateIBLK, GetSuperBlock returned NULL!");
                }

#if DEBUG
                Pfs3Logger.Instance.Debug(
                    $"Update: UpdateIBLK, SuperMode, oldblocknr = {blk.oldblocknr}, newblocknr = {newblocknr}");
#endif
                if (superblk.IndexBlock.index[temp >> 16] != blk.oldblocknr)
                {
                    throw new IOException(
                        $"Update index block in super block at index offset {temp >> 16} doesn't have expected old block nr {blk.oldblocknr} but instead block nr {superblk.IndexBlock.index[temp >> 16]}");
                }

                superblk.IndexBlock.index[temp >> 16] = (int)newblocknr;
                await MakeBlockDirty(superblk, g);
            }
            else
            {
#if DEBUG
                Pfs3Logger.Instance.Debug(
                    $"Update: UpdateIBLK, small, oldblocknr = {blk.oldblocknr}, newblocknr = {newblocknr}");
#endif
                if (g.RootBlock.idx.small.indexblocks[blk.IndexBlock.seqnr] != blk.oldblocknr)
                {
                    throw new IOException(
                        $"Update index block in small at index offset {blk.IndexBlock.seqnr} doesn't have expected old block nr {blk.oldblocknr} but instead block nr {g.RootBlock.idx.small.indexblocks[blk.IndexBlock.seqnr]}");
                }

                g.RootBlock.idx.small.indexblocks[blk.IndexBlock.seqnr] = newblocknr;
                g.currentvolume.rootblockchangeflag = true;
            }
        }

        public static void UpdateSBLK(CachedBlock blk, uint newblocknr, globaldata g)
        {
            // update super block dictionary from old to new block nr
            if (g.currentvolume.superblks.ContainsKey(blk.oldblocknr))
            {
                g.currentvolume.superblks.Remove(blk.oldblocknr);
                g.currentvolume.superblks.Add(newblocknr, blk);
            }
            
            // blk->changeflag = TRUE;
            // blk->volume->rblkextension->changeflag = TRUE;
            // blk->volume->rblkextension->blk.superindex[((struct cindexblock *)blk)->blk.seqnr] = newblocknr;
            blk.changeflag = true;
            blk.volume.rblkextension.changeflag = true;
#if DEBUG
            Pfs3Logger.Instance.Debug($"Update: UpdateSBLK, oldblocknr = {blk.oldblocknr}, newblocknr = {newblocknr}");
#endif
            if (blk.volume.rblkextension.rblkextension.superindex[blk.IndexBlock.seqnr] != blk.oldblocknr)
            {
                throw new IOException(
                    $"Update super block at index offset {blk.IndexBlock.seqnr} doesn't have expected old block nr {blk.oldblocknr} but instead block nr {blk.volume.rblkextension.rblkextension.superindex[blk.IndexBlock.seqnr]}");
            }

            blk.volume.rblkextension.rblkextension.superindex[blk.IndexBlock.seqnr] = newblocknr;
        }

        public static async Task UpdateBMBLK(CachedBlock blk, uint newblocknr, globaldata g)
        {
            // update bitmap block dictionary from old to new block nr
            if (g.currentvolume.bmblks.ContainsKey(blk.oldblocknr))
            {
                g.currentvolume.bmblks.Remove(blk.oldblocknr);
                g.currentvolume.bmblks.Add(newblocknr, blk);
            }

            // struct cindexblock *indexblock;
            var bmb = blk;
            uint temp;
            var andata = g.glob_anodedata;

            blk.changeflag = true;
            var bitmapBlk = bmb.BitmapBlock;
            temp = Init.divide(bitmapBlk.seqnr, andata.indexperblock);
            var indexblock = await Allocation.GetBitmapIndex((ushort)temp /* & 0xffff */, g);

            // DBERR(if (!indexblock) ErrorTrace(5,"UpdateBMBLK", "GetBitmapIndex returned NULL!"));
            if (indexblock == null)
            {
                throw new IOException("UpdateBMBLK, GetBitmapIndex returned NULL!");
            }

#if DEBUG
            Pfs3Logger.Instance.Debug($"Update: UpdateBMBLK, oldblocknr = {blk.oldblocknr}, newblocknr = {newblocknr}");
#endif
            var indexBlockBlk = indexblock.IndexBlock;
            if (indexBlockBlk.index[temp >> 16] != blk.oldblocknr)
            {
                throw new IOException(
                    $"Update bitmap block at index offset {temp >> 16} doesn't have expected old block nr {blk.oldblocknr} but instead block nr {indexBlockBlk.index[temp >> 16]}");
            }

            indexBlockBlk.index[temp >> 16] = (int)newblocknr;
            await MakeBlockDirty(indexblock, g); /* recursion !! */
        }

        public static void UpdateBMIBLK(CachedBlock blk, uint newblocknr, globaldata g)
        {
            // update bitmap index block dictionary from old to new block nr
            if (g.currentvolume.bmindexblks.ContainsKey(blk.oldblocknr))
            {
                g.currentvolume.bmindexblks.Remove(blk.oldblocknr);
                g.currentvolume.bmindexblks.Add(newblocknr, blk);
            }
            
            // blk->changeflag = TRUE;
            // blk->volume->rootblk->idx.large.bitmapindex[((struct cindexblock *)blk)->blk.seqnr] = newblocknr;
            // blk->volume->rootblockchangeflag = TRUE;
            blk.changeflag = true;
#if DEBUG
            Pfs3Logger.Instance.Debug(
                $"Update: UpdateBMIBLK, oldblocknr = {blk.oldblocknr}, newblocknr = {newblocknr}");
#endif
            if (g.RootBlock.idx.large.bitmapindex[blk.IndexBlock.seqnr] != blk.oldblocknr)
            {
                throw new IOException(
                    $"Update bitmap index block at index offset {blk.IndexBlock.seqnr} doesn't have expected old block nr {blk.oldblocknr} but instead block nr {g.RootBlock.idx.large.bitmapindex[blk.IndexBlock.seqnr]}");
            }

            g.RootBlock.idx.large.bitmapindex[blk.IndexBlock.seqnr] = newblocknr;
            g.currentvolume.rootblockchangeflag = true;
        }

// #if VERSION23
        public static async Task UpdateRBlkExtension(CachedBlock blk, uint newblocknr, globaldata g)
        {
            // blk->changeflag = TRUE;
            // blk->volume->rootblk->extension = newblocknr;
            // blk->volume->rootblockchangeflag = TRUE;
            blk.changeflag = true;
#if DEBUG
            Pfs3Logger.Instance.Debug(
                $"Update: UpdateRBlkExtension, oldblocknr = {blk.oldblocknr}, newblocknr = {newblocknr}");
#endif
            if (g.RootBlock.Extension != blk.oldblocknr)
            {
                throw new IOException(
                    $"Update root extension block doesn't have expected old block nr {blk.oldblocknr} but instead block nr {g.RootBlock.Extension}");
            }

            g.RootBlock.Extension = newblocknr;
            g.currentvolume.rootblockchangeflag = true;
            await MakeBlockDirty(blk, g);
        }
// #endif

        public static async Task UpdateDELDIR(CachedBlock blk, uint newblocknr, globaldata g)
        {
            // update dir block dictionary from old to new block nr
            if (g.currentvolume.deldirblks.ContainsKey(blk.oldblocknr))
            {
                g.currentvolume.deldirblks.Remove(blk.oldblocknr);
                g.currentvolume.deldirblks.Add(newblocknr, blk);
            }
            
            // blk->changeflag = TRUE;
            // blk->volume->rblkextension->blk.deldir[((struct cdeldirblock *)blk)->blk.seqnr] = newblocknr;
            // MakeBlockDirty((struct cachedblock *)blk->volume->rblkextension, g);
            blk.changeflag = true;
#if DEBUG
            Pfs3Logger.Instance.Debug(
                $"Update: UpdateDELDIR, oldblocknr = {blk.oldblocknr}, newblocknr = {newblocknr}");
#endif
            if (blk.volume.rblkextension.rblkextension.deldir[blk.deldirblock.seqnr] != blk.oldblocknr)
            {
                throw new IOException(
                    $"Update deldir block at index offset {blk.deldirblock.seqnr} doesn't have expected old block nr {blk.oldblocknr} but instead block nr {blk.volume.rblkextension.rblkextension.deldir[blk.deldirblock.seqnr]}");
            }

            blk.volume.rblkextension.rblkextension.deldir[blk.deldirblock.seqnr] = newblocknr;
            await MakeBlockDirty(blk.volume.rblkextension, g);
        }

/* Update datestamp (copy from rootblock
 * Call before writing block (lru.c)
 */
        public static void UpdateDatestamp(CachedBlock blk, globaldata g)
        {
            // struct cdirblock *dblk = (struct cdirblock *)blk;
            // struct crootblockextension *rext = (struct crootblockextension *)blk;

            // switch (((UWORD *)blk->data)[0])
            switch (blk.blk.id)
            {
                case Constants.DBLKID: /* dirblock */
                case Constants.ABLKID: /* anodeblock */
                case Constants.IBLKID: /* indexblock */
                case Constants.BMBLKID: /* bitmapblock */
                case Constants.BMIBLKID: /* bitmapindexblock */
                case Constants.DELDIRID: /* deldir */
                case Constants.SBLKID: /* superblock */
                // dblk->blk.datestamp = g->currentvolume->rootblk->datestamp;
                // break;

                case Constants.EXTENSIONID: /* rootblockextension */
                    // rext->blk.datestamp = g->currentvolume->rootblk->datestamp;
                    blk.blk.datestamp = g.RootBlock.Datestamp;
                    break;
            }
        }

        public static async Task<bool> UpdateDisk(globaldata g)
        {
#if DEBUG
            Pfs3Logger.Instance.Debug("Update: UpdateDisk");
#endif
            // struct DateStamp time;
            var volume = g.currentvolume;
            var alloc_data = g.glob_allocdata;
            var andata = g.glob_anodedata;
            bool success;

            /*
             * Do update
             */
            if (volume != null && g.dirty && !g.softprotect)
            {
                /*
                 * For performance reasons avoid concurrent access to same physical
                 * device. Note that the lock can be broken safely, it's only used
                 * to avoid excessive seeking due to competing updates.
                 */
                // only needed for amiga to send request to scsi io command
                //lock_device_unit(g);

                g.uip = true;
                g.updateok = true;
                await Disk.UpdateDataCache(g); /* flush DiskRead DiskWrite cache */

// #if VERSION23
                /* make sure rootblockextension is reallocated */
                if (volume.rblkextension != null)
                {
                    await MakeBlockDirty(volume.rblkextension, g);
                }
// #endif

                /* commit user space free list */
                await Allocation.UpdateFreeList(g);

                /* remove empty dir, anode, index and superblocks */
                await RemoveEmptyDBlocks(volume, g);
                await RemoveEmptyABlocks(volume, g);
                await RemoveEmptyIBlocks(volume, g);
                RemoveEmptySBlocks(volume, g);

                /* update anode, dir, index and superblocks (not changed by UpdateFreeList) */
                // for (i = 0; i <= Constants.HASHM_DIR; i++)
                // {
                //     if (!await UpdateList(Macro.HeadOf(volume.dirblks[i]), g))
                //     {
                //         g.updateok = false;
                //     }
                // }
                if (!await UpdateList(volume.dirblks.Values, g))
                {
                    g.updateok = false;
                }

                // for (i = 0; i <= Constants.HASHM_ANODE; i++)
                // {
                //     if (!await UpdateList(Macro.HeadOf(volume.anblks[i]), g))
                //     {
                //         g.updateok = false;
                //     }
                // }
                if (!await UpdateList(volume.anblks.Values, g))
                {
                    g.updateok = false;
                }

                // if (!await UpdateList(Macro.HeadOf(volume.indexblks), g))
                // {
                //     g.updateok = false;
                // }
                if (!await UpdateList(volume.indexblks.Values, g))
                {
                    g.updateok = false;
                }

                // if (!await UpdateList(Macro.HeadOf(volume.superblks), g))
                // {
                //     g.updateok = false;
                // }
                if (!await UpdateList(volume.superblks.Values, g))
                {
                    g.updateok = false;
                }

// #if DELDIR
                // if (!await UpdateList(Macro.HeadOf(volume.deldirblks), g))
                // {
                //     g.updateok = false;
                // }
                if (!await UpdateList(volume.deldirblks.Values, g))
                {
                    g.updateok = false;
                }
// #endif

// #if VERSION23
                if (volume.rblkextension != null)
                {
                    var rext = volume.rblkextension;

                    /* reserved roving and anode roving */
                    var rext_blk = rext.rblkextension;
                    rext_blk.reserved_roving = alloc_data.res_roving;
                    rext_blk.rovingbit = (ushort)alloc_data.rovingbit;
                    rext_blk.curranseqnr = andata.curranseqnr;

                    /* volume datestamp */
                    //DateStamp(&time);
                    rext_blk.VolumeDate = DateTime.UtcNow;
                    // rext->blk.volume_date[0] = (UWORD)time.ds_Days;
                    // rext->blk.volume_date[1] = (UWORD)time.ds_Minute;
                    // rext->blk.volume_date[2] = (UWORD)time.ds_Tick;
                    rext_blk.datestamp = g.RootBlock.Datestamp;

                    if (!await UpdateDirtyBlock(rext, g))
                    {
                        g.updateok = false;
                    }
                }
// #endif

                /* commit reserved to be freed list */
                CommitReservedToBeFreed(g);

                /* update bitmap and bitmap index blocks */
                // if (!await UpdateList(Macro.HeadOf(volume.bmblks), g))
                // {
                //     g.updateok = false;
                // }
                if (!await UpdateList(volume.bmblks.Values, g))
                {
                    g.updateok = false;
                }

                // if (!await UpdateList(Macro.HeadOf(volume.bmindexblks), g))
                // {
                //     g.updateok = false;
                // }
                if (!await UpdateList(volume.bmindexblks.Values, g))
                {
                    g.updateok = false;
                }

                /* update root (MUST be done last) */
                if (g.updateok)
                {
                    var rootBlockBytes = RootBlockWriter.BuildBlock(g.RootBlock, g);
                    g.RootBlock.BlockBytes = rootBlockBytes;
                    await Disk.RawWrite(g.stream, rootBlockBytes, 1, Constants.ROOTBLOCK, g);

                    var reservedBitmapBlockBytes = BitmapBlockWriter.BuildBlock(g.RootBlock.ReservedBitmapBlock, g);
                    g.RootBlock.ReservedBitmapBlock.BlockBytes = reservedBitmapBlockBytes;
                    var blocks = (uint)(reservedBitmapBlockBytes.Length / g.blocksize);
                    await Disk.RawWrite(g.stream, reservedBitmapBlockBytes, blocks, Constants.ROOTBLOCK + 1, g);

                    g.RootBlock.Datestamp++;
                    volume.rootblockchangeflag = false;

                    /* make sure update is really done */
                    // only needed for amiga to send request to scsi io command
                    // UpdateAndMotorOff(g);
                    success = true;
                }
                else
                {
                    // ErrorMsg(AFS_ERROR_UPDATE_FAIL, NULL, g);
                    throw new IOException("AFS_ERROR_UPDATE_FAIL");
                }

                g.uip = false;

                // only needed for amiga to send request to scsi io command
                // unlock_device_unit(g);
            }
            else
            {
                if (volume != null && g.dirty && g.softprotect)
                {
                    // ErrorMsg (AFS_ERROR_UPDATE_FAIL, NULL, g);
                    throw new IOException("AFS_ERROR_UPDATE_FAIL");
                }

                success = true;
            }

            g.dirty = false;

            // EXIT("UpdateDisk");
            return success;
        }

/*
 * Empty Dirblocks 
 */
        public static async Task RemoveEmptyDBlocks(volumedata volume, globaldata g)
        {
            //struct cdirblock *blk, *next;
            //CachedBlock blk;
            canode anode = new canode();
            uint previous; //, i;

            // for (i = 0; i <= Constants.HASHM_DIR; i++)
            // {
            //     for (var node = Macro.HeadOf(volume.dirblks[i]); node != null; node = node.Next)
            //     {
            //         blk = node.Value;
            //         if (blk.dirblock != null && Macro.IsEmptyDBlk(blk, g) && !await IsFirstDBlk(blk, g) &&
            //             !Cache.ISLOCKED(blk, g))
            //         {
            //             previous = await GetAnodeOfDBlk(blk, anode, g);
            //             await anodes.RemoveFromAnodeChain(anode, previous, blk.dirblock.anodenr, g);
            //             Macro.MinRemove(blk, g);
            //             Allocation.FreeReservedBlock(blk.blocknr, g);
            //             Lru.ResToBeFreed(blk.oldblocknr, g);
            //             Lru.FreeLRU(blk, g);
            //         }
            //     }
            // }
            foreach (var blk in volume.dirblks)
            {
                if (blk.Value.dirblock != null && Macro.IsEmptyDBlk(blk.Value, g) && !await IsFirstDBlk(blk.Value, g) &&
                    !Cache.ISLOCKED(blk.Value, g))
                {
                    previous = await GetAnodeOfDBlk(blk.Value, anode, g);
                    await anodes.RemoveFromAnodeChain(anode, previous, blk.Value.dirblock.anodenr, g);
                    Macro.MinRemove(blk.Value, g);
                    Allocation.FreeReservedBlock(blk.Value.blocknr, g);
                    Lru.ResToBeFreed(blk.Value.oldblocknr, g);
                    Lru.FreeLRU(blk.Value, g);
                }
            }
        }

        public static async Task<uint> GetAnodeOfDBlk(CachedBlock blk, canode anode, globaldata g)
        {
            uint prev = 0;
            await anodes.GetAnode(anode, blk.dirblock.anodenr, g);
            while (anode.blocknr != blk.blocknr && anode.next != 0) //anode.next purely safety
            {
                prev = anode.nr;
                await anodes.GetAnode(anode, anode.next, g);
            }

            return prev;
        }

        public static async Task<bool> IsFirstDBlk(CachedBlock blk, globaldata g)
        {
            bool first;
            canode anode = new canode();

            await anodes.GetAnode(anode, blk.dirblock.anodenr, g);
            first = (anode.blocknr == blk.blocknr);

            return first;
        }

        public static bool IsFirstABlk(CachedBlock blk)
        {
            // #define IsFirstABlk(blk) (blk->blk.seqnr == 0)

            return blk.ANodeBlock.seqnr == 0;
        }

        public static bool IsFirstIBlk(CachedBlock blk)
        {
            //#define IsFirstIBlk(blk) (blk->blk.seqnr == 0)
            return blk.IndexBlock.seqnr == 0;
        }

        public static async Task<bool> UpdateList(LinkedListNode<CachedBlock> blk, globaldata g)
        {
            CachedBlock blk2;

            if (!g.updateok)
                return false;

            while (blk != null)
            {
                if (blk.Value.changeflag)
                {
                    Allocation.FreeReservedBlock(blk.Value.oldblocknr, g);
                    blk2 = blk.Value;
                    blk2.blk.datestamp = g.RootBlock.Datestamp;
                    blk.Value.oldblocknr = 0;
                    if (!(await Disk.RawWrite(g.stream, blk.Value.blk, g.currentvolume.rescluster, blk.Value.blocknr,
                            g)))
                    {
                        // goto update_error;
                        // ErrorMsg (AFS_ERROR_UPDATE_FAIL, NULL, g);
                        throw new IOException("AFS_ERROR_UPDATE_FAIL");
                    }

                    blk.Value.changeflag = false;
                }

                blk = blk.Next;
            }

            return true;
        }

        public static async Task<bool> UpdateList(IEnumerable<CachedBlock> list, globaldata g)
        {
            if (!g.updateok)
                return false;

            foreach (var blk in list)
            {
                if (blk.changeflag)
                {
                    Allocation.FreeReservedBlock(blk.oldblocknr, g);
                    blk.blk.datestamp = g.RootBlock.Datestamp;
                    blk.oldblocknr = 0;
                    if (!(await Disk.RawWrite(g.stream, blk.blk, g.currentvolume.rescluster, blk.blocknr,
                            g)))
                    {
                        // goto update_error;
                        // ErrorMsg (AFS_ERROR_UPDATE_FAIL, NULL, g);
                        throw new IOException("AFS_ERROR_UPDATE_FAIL");
                    }

                    blk.changeflag = false;
                }
            }

            return true;
        }

        public static void CommitReservedToBeFreed(globaldata g)
        {
            var alloc_data = g.glob_allocdata;

            int i;
            for (i = 0; i < alloc_data.rtbf_index; i++)
            {
                if (alloc_data.reservedtobefreed[i] != 0)
                {
                    Allocation.FreeReservedBlock(alloc_data.reservedtobefreed[i], g);
                    alloc_data.reservedtobefreed[i] = 0;
                }
            }

            alloc_data.rtbf_index = 0;
        }

        public static async Task<bool> UpdateDirtyBlock(CachedBlock blk, globaldata g)
        {
            // ULONG error;

            if (!g.updateok)
                return false;

            if (blk.changeflag)
            {
                Allocation.FreeReservedBlock(blk.oldblocknr, g);
                blk.oldblocknr = 0;
                if (!await Disk.RawWrite(g.stream, blk.blk, g.currentvolume.rescluster, blk.blocknr, g))
                {
                    // ErrorMsg (AFS_ERROR_UPDATE_FAIL, NULL, g);
                    throw new IOException("AFS_ERROR_UPDATE_FAIL");
                }
            }

            blk.changeflag = false;
            return true;
        }

        public static async Task RemoveEmptyABlocks(volumedata volume, globaldata g)
        {
            // canodeblock
            CachedBlock blk;
            uint indexblknr, indexoffset, i;
            // struct cindexblock *index;
            var andata = g.glob_anodedata;

            foreach (var node in volume.anblks)
            {
                blk = node.Value;
                if (blk.changeflag && blk.ANodeBlock != null && !IsFirstABlk(blk) && IsEmptyABlk(blk, g) &&
                    !Cache.ISLOCKED(blk, g))
                {
                    var anodeblock = blk.ANodeBlock;
                    indexblknr = anodeblock.seqnr / andata.indexperblock;
                    indexoffset = anodeblock.seqnr % andata.indexperblock;

                    /* kill the block */
                    Macro.MinRemove(blk, g);
                    Allocation.FreeReservedBlock(blk.blocknr, g);
                    Lru.ResToBeFreed(blk.oldblocknr, g);
                    Lru.FreeLRU(blk, g);

                    /* and remove the reference (this one should already be in the cache) */
                    var index = await anodes.GetIndexBlock((ushort)indexblknr, g);
                    if (index == null)
                    {
                        // DBERR(if (!index) ErrorTrace(5, "RemoveEmptyABlocks", "GetIndexBlock returned NULL!"))
                        throw new IOException("RemoveEmptyABlocks, GetIndexBlock returned NULL!");
                    }

                    index.IndexBlock.index[indexoffset] = 0;
                }
            }

            // for (i = 0; i <= Constants.HASHM_ANODE; i++)
            // {
            //     for (var node = Macro.HeadOf(volume.anblks[i]); node != null; node = node.Next)
            //     {
            //         blk = node.Value;
            //         if (blk.changeflag && blk.ANodeBlock != null && !IsFirstABlk(blk) && IsEmptyABlk(blk, g) &&
            //             !Cache.ISLOCKED(blk, g))
            //         {
            //             var anodeblock = blk.ANodeBlock;
            //             indexblknr = anodeblock.seqnr / andata.indexperblock;
            //             indexoffset = anodeblock.seqnr % andata.indexperblock;
            //
            //             /* kill the block */
            //             Macro.MinRemove(blk, g);
            //             Allocation.FreeReservedBlock(blk.blocknr, g);
            //             Lru.ResToBeFreed(blk.oldblocknr, g);
            //             Lru.FreeLRU(blk, g);
            //
            //             /* and remove the reference (this one should already be in the cache) */
            //             var index = await anodes.GetIndexBlock((ushort)indexblknr, g);
            //             if (index == null)
            //             {
            //                 // DBERR(if (!index) ErrorTrace(5, "RemoveEmptyABlocks", "GetIndexBlock returned NULL!"))
            //                 throw new IOException("RemoveEmptyABlocks, GetIndexBlock returned NULL!");
            //             }
            //
            //             index.IndexBlock.index[indexoffset] = 0;
            //             index.changeflag = true;
            //         }
            //     }
            // }
        }

        public static bool IsEmptyABlk(CachedBlock ablk, globaldata g)
        {
            // canodeblock
            anode[] anodes;
            uint j;
            bool found = false;
            var andata = g.glob_anodedata;

            /* zoek bezette anode */
            anodes = ablk.ANodeBlock.nodes;
            for (j = 0; j < andata.anodesperblock && !found; j++)
                found |= (anodes[j].blocknr != 0);

            found = !found;
            return found; /* not found -> empty */
        }

/*
 * Empty block check
 */
        public static async Task RemoveEmptyIBlocks(volumedata volume, globaldata g)
        {
            //struct cindexblock *blk, *next;
            CachedBlock blk;

            // for (var node = Macro.HeadOf(volume.indexblks); node != null; node = node.Next)
            // {
            //     blk = node.Value;
            //     if (blk.changeflag && !IsFirstIBlk(blk) && IsEmptyIBlk(blk, g) && !Cache.ISLOCKED(blk, g))
            //     {
            //         await UpdateIBLK(blk, 0, g);
            //         Macro.MinRemove(blk, g);
            //         Allocation.FreeReservedBlock(blk.blocknr, g);
            //         Lru.ResToBeFreed(blk.oldblocknr, g);
            //         Lru.FreeLRU(blk, g);
            //     }
            // }
            foreach (var node in volume.indexblks)
            {
                blk = node.Value;
                if (blk.changeflag && !IsFirstIBlk(blk) && IsEmptyIBlk(blk, g) && !Cache.ISLOCKED(blk, g))
                {
                    await UpdateIBLK(blk, 0, g);
                    Macro.MinRemove(blk, g);
                    Allocation.FreeReservedBlock(blk.blocknr, g);
                    Lru.ResToBeFreed(blk.oldblocknr, g);
                    Lru.FreeLRU(blk, g);
                }
            }
        }

        public static void RemoveEmptySBlocks(volumedata volume, globaldata g)
        {
            //struct cindexblock *blk, *next;
            CachedBlock blk;

            // for (var node = Macro.HeadOf(volume.superblks); node != null; node = node.Next)
            // {
            //     blk = node.Value;
            //     if (blk.changeflag && !IsFirstIBlk(blk) && IsEmptyIBlk(blk, g) && !Cache.ISLOCKED(blk, g))
            //     {
            //         UpdateSBLK(blk, 0, g);
            //         Macro.MinRemove(blk, g);
            //         Allocation.FreeReservedBlock(blk.blocknr, g);
            //         Lru.ResToBeFreed(blk.oldblocknr, g);
            //         Lru.FreeLRU(blk, g);
            //     }
            // }
            foreach (var node in volume.superblks)
            {
                blk = node.Value;
                if (blk.changeflag && !IsFirstIBlk(blk) && IsEmptyIBlk(blk, g) && !Cache.ISLOCKED(blk, g))
                {
                    UpdateSBLK(blk, 0, g);
                    Macro.MinRemove(blk, g);
                    Allocation.FreeReservedBlock(blk.blocknr, g);
                    Lru.ResToBeFreed(blk.oldblocknr, g);
                    Lru.FreeLRU(blk, g);
                }
            }
        }

        public static bool IsEmptyIBlk(CachedBlock blk, globaldata g)
        {
            // ULONG *index, i;
            bool found = false;
            var andata = g.glob_anodedata;

            var index = blk.IndexBlock.index;
            for (var i = 0; i < andata.indexperblock; i++)
            {
                found = index[i] != 0;
                if (found)
                    break;
            }

            found = !found;
            return found;
        }
    }
}