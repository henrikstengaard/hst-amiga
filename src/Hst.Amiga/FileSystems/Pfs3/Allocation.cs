namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Blocks;

    public static class Allocation
    {
/*
 * the following routines (NewBitmapBlock & NewBitmapIndexBlock are
 * primarily (read only) used by Format
 */
        public static async Task<CachedBlock> NewBitmapBlock(uint seqnr, globaldata g)
        {
            CachedBlock blok;
            CachedBlock indexblock;
            var volume = g.currentvolume;
            var andata = g.glob_anodedata;
            var alloc_data = g.glob_allocdata;
            uint indexblnr, blocknr, indexoffset;
            uint i;
            ushort oldlock;

            /* get indexblock */
            indexblnr = seqnr / andata.indexperblock;
            indexoffset = seqnr % andata.indexperblock;
            if ((indexblock = await GetBitmapIndex((ushort)indexblnr, g)) == null)
            {
                if ((indexblock = await NewBitmapIndexBlock((ushort)indexblnr, g)) == null)
                {
                    return null;
                }
            }

            oldlock = indexblock.used;
            Cache.LOCK(indexblock, g);
            if ((blok = await Lru.AllocLRU(g)) == null || (blocknr = AllocReservedBlock(g)) == 0)
            {
                return null;
            }

            indexblock.IndexBlock.index[indexoffset] = (int)blocknr;

            blok.volume = volume;
            blok.blocknr = blocknr;
            blok.used = 0;
            var blok_blk = new BitmapBlock((int)g.glob_allocdata.longsperbmb)
            {
                id = Constants.BMBLKID,
                seqnr = seqnr
            };
            blok.blk = blok_blk;
            blok.changeflag = true;

            /* fill bitmap */
            var bitmap = blok_blk.bitmap;
            for (i = 0; i < alloc_data.longsperbmb; i++)
            {
                // bitmap[i] = ~0;
                bitmap[i] = UInt32.MaxValue; //  hexadecimal 0xFFFFFFFF
            }

            Macro.MinAddHead(volume.bmblks, blok);
            await Update.MakeBlockDirty(indexblock, g);
            indexblock.used = oldlock; // unlock;

            return blok;
        }

        public static async Task<CachedBlock> NewBitmapIndexBlock(ushort seqnr, globaldata g)
        {
            CachedBlock blok;
            var volume = g.currentvolume;

            if (seqnr > (g.SuperMode ? Constants.MAXBITMAPINDEX : Constants.MAXSMALLBITMAPINDEX) ||
                (blok = await Lru.AllocLRU(g)) == null)
            {
                return null;
            }

            if ((g.RootBlock.idx.large.bitmapindex[seqnr] = AllocReservedBlock(g)) == 0)
            {
                Lru.FreeLRU(blok, g);
                return null;
            }

            volume.rootblockchangeflag = true;

            blok.volume = volume;
            blok.blocknr = volume.rootblk.idx.large.bitmapindex[seqnr];
            blok.used = 0;
            blok.blk = new indexblock(g)
            {
                id = Constants.BMIBLKID,
                seqnr = seqnr
            };
            blok.changeflag = true;
            Macro.MinAddHead(volume.bmindexblks, blok);

            return blok;
        }

        /*
         * AllocReservedBlock
         */
        public static uint AllocReservedBlock(globaldata g)
        {
            var vol = g.currentvolume;
            var alloc_data = g.glob_allocdata;
            var bitmap = alloc_data.res_bitmap.bitmap;
            //uint free = (uint)g.RootBlock.ReservedFree;
            uint blocknr;
            int i, j;

            // ENTER("AllocReservedBlock");
#if DEBUG
            Pfs3Logger.Instance.Debug($"Allocation: AllocReservedBlock Enter");
#endif

            /* Check if allocation possible 
             * (really necessary?)
             */
            if (g.RootBlock.ReservedFree == 0)
            {
                return 0;
            }

            j = (int)(31 - alloc_data.res_roving % 32);
            for (i = (int)(alloc_data.res_roving / 32); i < (alloc_data.numreserved + 31) / 32; i++, j = 31)
            {
                if (bitmap[i] != 0)
                {
                    uint field = bitmap[i];
                    for (; j >= 0; j--)
                    {
                        if ((field & (1 << j)) != 0)
                        {
                            blocknr = (uint)(g.RootBlock.FirstReserved + (i * 32 + (31 - j)) * vol.rescluster);
                            if (blocknr <= g.RootBlock.LastReserved)
                            {
                                bitmap[i] &= (uint)~(1 << j);
                                g.currentvolume.rootblockchangeflag = true;
                                g.dirty = true;
                                g.RootBlock.ReservedFree--;
                                alloc_data.res_roving = (uint)(32 * i + (31 - j));
                                // DB(Trace(10,"AllocReservedBlock","allocated %ld\n", blocknr));
#if DEBUG
                                Pfs3Logger.Instance.Debug($"Allocation: AllocReservedBlock, allocated block nr = {blocknr}");
#endif
                                return blocknr;
                            }
                        }
                    }
                }
            }

            /* end of bitmap reached. Reset roving pointer and try again 
            */
            if (alloc_data.res_roving != 0)
            {
                alloc_data.res_roving = 0;
                blocknr = AllocReservedBlock(g);
            }
            else
                blocknr = 0;

            // EXIT("AllocReservedBlock");
#if DEBUG
            Pfs3Logger.Instance.Debug($"Allocation: AllocReservedBlock Exit, block nr = {blocknr}");
#endif
            return blocknr;
        }

        public static void FreeReservedBlock(uint blocknr, globaldata g)
        {
            /*
             * frees reserved block, or does nothing if blocknr = 0
             */
            if (blocknr != 0 && blocknr <= g.RootBlock.LastReserved)
            {
                var alloc_data = g.glob_allocdata;
                var bitmap = alloc_data.res_bitmap.bitmap;
                var t = (blocknr - g.RootBlock.FirstReserved) / g.currentvolume.rescluster;
                bitmap[t / 32] |= 0x80000000U >> (int)(t % 32);
                g.RootBlock.ReservedFree++;
                g.currentvolume.rootblockchangeflag = true;
            }
        }

        public static async Task<CachedBlock> GetBitmapIndex(ushort nr, globaldata g)
        {
            uint blocknr;
            CachedBlock indexblk;
            var volume = g.currentvolume;

            /* check cache */
            for (var node = Macro.HeadOf(volume.bmindexblks); node != null; node = node.Next)
            {
                indexblk = node.Value;
                if (indexblk.blk is indexblock indexBlock && indexBlock.seqnr == nr)
                {
                    Lru.MakeLRU(indexblk, g);
                    return indexblk;
                }
            }

            /* not in cache, put it in */
            if (nr > (g.SuperMode ? Constants.MAXBITMAPINDEX : Constants.MAXSMALLBITMAPINDEX) ||
                (blocknr = volume.rootblk.idx.large.bitmapindex[nr]) == 0 ||
                (indexblk = await Lru.AllocLRU(g)) == null)
            {
                return null;
            }

            // DB(Trace(10,"GetBitmapIndex", "seqnr = %ld blocknr = %lx\n", nr, blocknr));
#if DEBUG
            Pfs3Logger.Instance.Debug($"Allocation: GetBitmapIndex seqnr = {nr}, blocknr = {blocknr}");
#endif
            
            IBlock blk;
            if ((blk = await Disk.RawRead<indexblock>(g.currentvolume.rescluster, blocknr, g)) == null)
            {
                Lru.FreeLRU(indexblk, g);
                return null;
            }

            indexblk.blk = blk;

            if (indexblk.blk.id == Constants.BMIBLKID)
            {
                indexblk.volume = volume;
                indexblk.blocknr = blocknr;
                indexblk.used = 0;
                indexblk.changeflag = false;
                Macro.MinAddHead(volume.bmindexblks, indexblk);
            }
            else
            {
                // ULONG args[5];
                // args[0] = indexblk->blk.id;
                // args[1] = BMIBLKID;
                // args[2] = blocknr;
                // args[3] = nr;
                // args[4] = 0;
                Lru.FreeLRU(indexblk, g);
                // ErrorMsg (AFS_ERROR_DNV_WRONG_INDID, args, g);
                return null;
            }

            Cache.LOCK(indexblk, g);
            return indexblk;
        }

/*
 * Update bitmap
 */
        public static async Task UpdateFreeList(globaldata g)
        {
            CachedBlock bitmap = null;
            ushort i;
            uint longnr, blocknr, bmseqnr, newbmseqnr, bmoffset, bitnr;
            var alloc_data = g.glob_allocdata;

            /* sort the free list */
            // not done right now

            /* free all blocks in list */
            bmseqnr = UInt32.MaxValue;
            for (i = 0; i < alloc_data.tobefreed_index; i++)
            {
                for (blocknr = alloc_data.tobefreed[i][Constants.TBF_BLOCKNR];
                     blocknr < alloc_data.tobefreed[i][Constants.TBF_SIZE] +
                     alloc_data.tobefreed[i][Constants.TBF_BLOCKNR];
                     blocknr++)
                {
                    /* now free block blocknr */
                    bitnr = blocknr - alloc_data.bitmapstart;
                    longnr = bitnr / 32;
                    newbmseqnr = longnr / alloc_data.longsperbmb;
                    bmoffset = longnr % alloc_data.longsperbmb;
                    if (newbmseqnr != bmseqnr)
                    {
                        bmseqnr = newbmseqnr;
                        bitmap = await GetBitmapBlock(bmseqnr, g);
                    }

                    bitmap.BitmapBlock.bitmap[bmoffset] |= (uint)(1 << (int)(31 - (bitnr % 32)));
                    await Update.MakeBlockDirty(bitmap, g);
                }

                alloc_data.clean_blocksfree += alloc_data.tobefreed[i][Constants.TBF_SIZE];
            }

            /* update global data */
            /* alloc_data.alloc_available should already be equal blocksfree - alwaysfree */
            alloc_data.tobefreed_index = 0;
            alloc_data.tbf_resneed = 0;
            g.RootBlock.BlocksFree = alloc_data.clean_blocksfree;
            g.currentvolume.rootblockchangeflag = true;
        }

/* this routine is analogous GetAnodeBlock()
 * GetBitmapIndex is analogous GetIndexBlock()
 */
        public static async Task<CachedBlock> GetBitmapBlock(uint seqnr, globaldata g)
        {
            uint blocknr = 0, temp;
            CachedBlock bmb = null;
            CachedBlock indexblock;
            var volume = g.currentvolume;
            var andata = g.glob_anodedata;

            /* check cache */
            for (var node = Macro.HeadOf(volume.bmblks); node != null; node = node.Next)
            {
                bmb = node.Value;
                if (bmb.BitmapBlock.seqnr == seqnr)
                {
                    Lru.MakeLRU(bmb, g);
                    return bmb;
                }
            }

            /* not in cache, put it in */
            /* get the indexblock */
            temp = Init.divide(seqnr, andata.indexperblock);
            if ((indexblock = await GetBitmapIndex((ushort)temp /* & 0xffff */, g)) == null)
                return null;

            /* get blocknr */
            var indexBlockBlk = indexblock.IndexBlock;
            if ((blocknr = (uint)indexBlockBlk.index[temp >> 16]) == 0 || (bmb = await Lru.AllocLRU(g)) == null)
            {
                return null;
            }

            // DB(Trace(10,"GetBitmapBlock", "seqnr = %ld blocknr = %lx\n", seqnr, blocknr));
#if DEBUG
            Pfs3Logger.Instance.Debug($"Allocation: GetBitmapBlock seqnr = {seqnr}, blocknr = {blocknr}");
#endif
            /* read it */
            bmb.blk = await Disk.RawRead<BitmapBlock>(g.currentvolume.rescluster, blocknr, g);
            if (bmb.blk == null)
            {
                Lru.FreeLRU(bmb, g);
                return null;
            }

            /* check it */
            if (bmb.blk.id != Constants.BMBLKID)
            {
                // ULONG args[2];
                // args[0] = bmb->blk.id;
                // args[1] = blocknr;
                Lru.FreeLRU(bmb, g);
                // ErrorMsg (AFS_ERROR_DNV_WRONG_BMID, args, g);
                return null;
            }

            /* initialize it */
            bmb.volume = volume;
            bmb.blocknr = blocknr;
            bmb.used = 0;
            bmb.changeflag = false;
            Macro.MinAddHead(volume.bmblks, bmb);

            return bmb;
        }

/*
 * Free all blocks in an anodechain? Use FreeBlocksAC(achain,ULONG_MAX,freetype,g)
 */

/*
 * Frees blocks allocated with AllocateBlocks. Freed blocks are added
 * to tobefreed list, and are not actually freed until UpdateFreeList
 * is called. Frees from END of anodelist.
 *
 * 'freetype' specifies if the anodes involved should be freed (freeanodes) or
 * not (keepanodes). In keepanodes mode the whole file (size >= filesize) should
 * be deleted, since otherwise the anodes cannot consistently be kept (dual
 * definition). In freeanodes mode the leading anode is not freed.
 *
 * VERSION23: uses tobedone fields. Because freeing blocks is idem-potent, fully
 * repeating an interrupted operation after reboot is ok. The blocks should not
 * be added to the blocksfree counter twice, however (see DoPostoned())
 * 
 * -> all references that indicate the freed blocks must have been
 *  done (atomically).
 */
        public static async Task FreeBlocksAC(anodechain achain, uint size, freeblocktype freetype, globaldata g)
        {
            anodechainnode chnode, tail;
            uint freeing;
            uint i, t = 0;
            bool empty = false;
            // crootblockextension* rext;
            uint blocksdone = 0;
            var alloc_data = g.glob_allocdata;

            // ENTER("FreeBlocksAC");

            i = alloc_data.tobefreed_index;
            tail = null;

            /* store operation tobedone */
            var rext = g.currentvolume.rblkextension;
            var rext_blk = rext.rblkextension;
            if (rext != null)
            {
                if (freetype == freeblocktype.keepanodes)
                    rext_blk.tobedone.operation_id = Constants.PP_FREEBLOCKS_KEEP;
                else
                    rext_blk.tobedone.operation_id = Constants.PP_FREEBLOCKS_FREE;

                rext_blk.tobedone.argument1 = achain.head.an.nr;
                rext_blk.tobedone.argument2 = size;
                rext_blk.tobedone.argument3 = 0; /* blocks done (FREEBLOCKS_KEEP) */
                await Update.MakeBlockDirty(rext, g);
            }

            /* check if tobefreedcache is sufficiently large,
             * otherwise updatedisk
             */
            for (chnode = achain.head; chnode.next != null; t++)
                chnode = chnode.next;

            if ((i > 0) && (t + i >= Constants.TBF_CACHE_SIZE - 1))
            {
                await Update.UpdateDisk(g);
                i = alloc_data.tobefreed_index;
            }

            // if (size)
            //     goto l1;            
            var l1 = size != 0;

            /* reverse order freeloop */
            while (size != 0 && !empty)
            {
                if (!l1)
                {
                    /* Get chainnode to free from */
                    chnode = achain.head;
                    while (chnode.next != tail)
                        chnode = chnode.next;
                }

                // l1: 
                /* get blocks to free */
                if (chnode.an.clustersize <= size)
                {
                    freeing = chnode.an.clustersize;
                    chnode.an.clustersize = 0;

                    tail = chnode;
                    empty = tail == achain.head;
                }
                else
                {
                    /* anode is partially freed;
                     * should only be possible in freeanodes mode 
                     */
                    freeing = size;
                    chnode.an.clustersize -= size;
                    chnode.an.next = 0;
                    if (freetype == freeblocktype.freeanodes)
                        await anodes.SaveAnode(chnode.an, chnode.an.nr, g);
                }

                /* and put them in the tobefreed list */
                if (freeing != 0)
                {
                    alloc_data.tobefreed[i][Constants.TBF_BLOCKNR] = chnode.an.blocknr + chnode.an.clustersize;
                    alloc_data.tobefreed[i++][Constants.TBF_SIZE] = freeing;
                    alloc_data.tbf_resneed += 3 + freeing / (32 * alloc_data.longsperbmb);
                    alloc_data.alloc_available += freeing;
                    size -= freeing;
                    blocksdone += freeing;

                    /* free anode if it is empty, we're supposed to and it is not the head */
                    if (!empty && freetype == freeblocktype.freeanodes && chnode.an.clustersize == 0)
                    {
                        await anodes.FreeAnode(chnode.an.nr, g);
                        // FreeMemP(chnode, g);
                    }
                }

                /* check if intermediate update is needed 
                 * (tobefreed cache full, low on reserved blocks etc)
                 */
                if (i >= Constants.TBF_CACHE_SIZE || Macro.IsUpdateNeeded(Constants.RTBF_POSTPONED_TH, g))
                {
                    alloc_data.tobefreed_index = i;
                    g.dirty = true;
                    if (rext != null && freetype == freeblocktype.freeanodes)
                    {
                        /* make anodechain consistent */
                        await RestoreAnodeChain(achain, empty, tail, g);
                        tail = null;

                        /* postponed op: finish operation later */
                        rext_blk.tobedone.argument2 = size;
                    }
                    else
                        /* postponed op: repeat operation later, but don't increase blocks free twice */
                        rext_blk.tobedone.argument3 = blocksdone;

                    await Update.MakeBlockDirty(rext, g);
                    await Update.UpdateDisk(g);
                    i = alloc_data.tobefreed_index;
                }
            }

            /* restore anode chain (both cached and on disk) */
            if (freetype == freeblocktype.freeanodes)
                await RestoreAnodeChain(achain, empty, tail, g);

            /* cancel posponed operation */
            if (rext != null)
            {
                rext_blk.tobedone.operation_id = 0;
                rext_blk.tobedone.argument1 = 0;
                rext_blk.tobedone.argument2 = 0;
                rext_blk.tobedone.argument3 = 0;
                await Update.MakeBlockDirty(rext, g);
            }

            /* update tobefreed index */
            g.dirty = true;
            alloc_data.tobefreed_index = i;

            // EXIT("FreeBlocksAC");
        }

        /* local function of FreeBlocksAC
 * restore anodechain (freeanode mode only)
 */
        public static async Task RestoreAnodeChain(anodechain achain, bool empty, anodechainnode tail, globaldata g)
        {
            anodechainnode chnode;

            if (empty)
            {
                achain.head.next = null;
                achain.head.an.clustersize = 0;
                // achain.head.an.blocknr = ~0L;
                achain.head.an.blocknr = UInt32.MaxValue;
                achain.head.an.next = 0;
                await anodes.SaveAnode(achain.head.an, achain.head.an.nr, g);
            }
            else
            {
                chnode = achain.head;
                while (chnode.next != tail)
                    chnode = chnode.next;

                chnode.next = null;
                chnode.an.next = 0;
                await anodes.SaveAnode(chnode.an, chnode.an.nr, g);
            }
        }

/*
 * AllocateBlocksAC
 * Allocate blocks to end of (cached) anodechain.
 * If ref != NULL then online directory update enabled.
 * Make sure the state is valid before you call this function!
 * Returns success
 * if FAIL, then a part of the needed blocks could already have been allocated
 */
        public static async Task<bool> AllocateBlocksAC(anodechain achain, uint size, fileinfo ref_, globaldata g)
        {
            uint nr, field, i = 0, j = 0, blocknr, blocksdone = 0;
            uint extra;
            uint oldfilesize = 0;
            uint bmseqnr = 0;
            ushort bmoffset = 0, oldlocknr = 0;
            CachedBlock bitmap; // cbitmapblock_t
            anodechainnode chnode;
            var vol = g.currentvolume;
            var alloc_data = g.glob_allocdata;
            bool extend = false, updateroving = true;

            //ENTER("AllocateBlocksAC");
#if DEBUG
            Pfs3Logger.Instance.Debug($"Allocation: AllocateBlocksAC Enter");
#endif

            /* Check if allocation possible */
            if (alloc_data.alloc_available < size)
                return false;

            /* check for sufficient clean freespace (freespace that doesn't overlap
             * with the current state on disk)
             */
            if (alloc_data.clean_blocksfree < size)
            {
                await Update.UpdateDisk(g);
            }

            // #if VERSION23
        	/* remember filesize in order to be able to cancel */
            if (ref_ != null)
            {
                oldfilesize = Directory.GetDEFileSize(ref_.direntry, g);
            }
            // #endif

            /* count number of fragments and decide on fileextend preallocation
             * get anode to expand
             */
            chnode = achain.head;
            for (i = 0; chnode.next != null; i++)
                chnode = chnode.next;

            extra = Math.Min(256, i * 8);
            if (chnode.an.blocknr != 0 && chnode.an.blocknr != UInt32.MaxValue) // != -1
            {
                i = chnode.an.blocknr + chnode.an.clustersize - alloc_data.bitmapstart;
                nr = i / 32;
                i %= 32;
                j = (uint)(1L << (31 - (int)i));
                bmseqnr = nr / alloc_data.longsperbmb;
                bmoffset = (ushort)(nr % alloc_data.longsperbmb);
                bitmap = await GetBitmapBlock(bmseqnr, g);
                var bitmapBlk = bitmap.BitmapBlock;
                if (bitmapBlk == null)
                {
                    throw new IOException($"Allocation: Cached block nr {bitmap.blocknr} in chnode allocate returns null as bitmap block, type is '{(bitmap.blk == null ? "null" : bitmap.blk.GetType().Name)}'");
                }
                field = bitmapBlk.bitmap[bmoffset];

                /* block directly behind file free ? */
                if ((field & j) != 0)
                {
                    extend = true;

                    /* if the position we want to allocate does not corresponds to the
                     * rovingpointer, the rovingpointer should not be updated
                     */
                    if (nr != g.RootBlock.RovingPtr)
                        updateroving = false;
                }
            }


            /* Get bitmap to allocate from */
            if (!extend)
            {
                nr = g.RootBlock.RovingPtr;
                bmseqnr = nr / alloc_data.longsperbmb;
                bmoffset = (ushort)(nr % alloc_data.longsperbmb);
                i = alloc_data.rovingbit;
                j = (uint)(1L << (31 - (int)i));
            }

            /* Allocate */
            while (size != 0)
            {
                /* scan all bitmapblocks */
                bitmap = await GetBitmapBlock(bmseqnr, g);
                oldlocknr = bitmap.used;
                
                /* find all empty fields */
                while (bmoffset < alloc_data.longsperbmb)
                {
#if DEBUG
                    var bitmapBlocksInCache = g.currentvolume.bmblks.Select(x => x.blocknr).ToList();
                    Pfs3Logger.Instance.Debug($"Allocation: AllocateBlocksAC scan bitmap block nr {bitmap.blocknr} ({bitmap.GetHashCode()}), size = {size}, bmseqnr = {bmseqnr}, bmoffset = {bmoffset}, bitmap blocks in cache '{string.Join(", ", bitmapBlocksInCache)}'");
#endif
                    var bitmapBlk = bitmap.BitmapBlock;
                    if (bitmapBlk == null)
                    {
                        throw new IOException($"Allocation: Cached block nr {bitmap.blocknr} in allocate returns null as bitmap block, type is '{(bitmap.blk == null ? "null" : bitmap.blk.GetType().Name)}'");
                    }
                    field = bitmapBlk.bitmap[bmoffset];
                    if (field != 0)
                    {
                        /* take all empty bits */
                        for (; i < 32; j >>= 1, i++)
                        {
                            if ((field & j) != 0)
                            {
                                /* block is available, calc blocknr */
                                blocknr = (bmseqnr * alloc_data.longsperbmb + bmoffset) * 32 + i +
                                          alloc_data.bitmapstart;

                                /* check in range */
                                if (blocknr >= vol.numblocks)
                                {
                                    bmoffset = (ushort)alloc_data.longsperbmb;
                                    continue;
                                }
                                /* take block */
                                else
                                {
                                    Macro.Lock(bitmap, g);

                                    /* uninitialized anode */
                                    if (chnode.an.blocknr == UInt32.MaxValue) // = -1
                                    {
                                        chnode.an.blocknr = blocknr;
                                        chnode.an.clustersize = 0;
                                        chnode.an.next = 0;
                                    }
                                    /* check blockconnect */
                                    else if (chnode.an.blocknr + chnode.an.clustersize != blocknr)
                                    {
                                        uint anodenr;

                                        chnode.next = new anodechainnode();
//                                         if (!(chnode.next = AllocMemP(anodechainnode), g)))
//                                         {
// // #if VERSION23
// 									        if (ref_ != null)
// 									        {
// 										        Directory.SetDEFileSize(ref_.direntry, oldfilesize, g);
// 										        await Update.MakeBlockDirty(ref_.dirblock, g);
// 									        }
// // #endif
//                                             /* undo allocation so far */
//                                             await FreeBlocksAC(achain, blocksdone, freeblocktype.freeanodes, g);
//                                             return false;
//                                         }

                                        anodenr = await anodes.AllocAnode(chnode.an.nr, g); /* should not go wrong! */
                                        chnode.an.next = anodenr;
                                        await anodes.SaveAnode(chnode.an, chnode.an.nr, g);
                                        chnode = chnode.next;
                                        chnode.an.nr = anodenr;
                                        chnode.an.blocknr = blocknr;
                                        chnode.an.clustersize = 0;
                                        chnode.an.next = 0;
                                    }
                                    
                                    bitmapBlk.bitmap[bmoffset] &= ~j; /* remove block from freelist */
                                    chnode.an.clustersize++; /* to file  	  	  */
                                    await Update.MakeBlockDirty(bitmap, g);

                                    /* update counters */
                                    alloc_data.clean_blocksfree--;
                                    alloc_data.alloc_available--;
                                    blocksdone++;

                                    /* update reference */
                                    if (ref_ != null)
                                    {
                                        Directory.SetDEFileSize(ref_.dirblock.dirblock, ref_.direntry, Directory.GetDEFileSize(ref_.direntry, g) + Macro.BLOCKSIZE(g), g);
                                        if (Macro.IsUpdateNeeded(Constants.RTBF_POSTPONED_TH, g))
                                        {
                                            /* make state valid and update disk */
                                            await Update.MakeBlockDirty(ref_.dirblock, g);
                                            await anodes.SaveAnode(chnode.an, chnode.an.nr, g);
                                            if (bitmap.BitmapBlock == null)
                                            {
                                                // error cached bitmap block overwritten with anodeblock
                                                // as alloclru returns not locked bitmapblock and reuses
                                                // it for anodeblock
                                                throw new IOException("Bitmap block is null");
                                            }
                                            await Update.UpdateDisk(g);

                                            /* abort if running out of reserved blocks */
                                            if (g.RootBlock.ReservedFree <= Constants.RESFREE_THRESHOLD)
                                            {
// #if VERSION23
                                                ref_.direntry = Directory.SetDEFileSize(ref_.dirblock.dirblock, ref_.direntry, oldfilesize, g);
                                                await Update.MakeBlockDirty (ref_.dirblock, g);
// #endif
                                                await FreeBlocksAC(achain, blocksdone, freeblocktype.freeanodes, g);
                                                return false;
                                            }
                                        }
                                    }


                                    if (--size == 0)
                                        goto alloc_end;
                                }
                            }
                        }

                        i = 0;
                        j = (uint)(1L << 31);
                    }

                    bmoffset++;
                }

                bitmap.used = oldlocknr;

                /* get ready for next block */
                bmseqnr = (bmseqnr + 1) % (alloc_data.no_bmb);
                bmoffset = 0;
            }

            alloc_end:

            /* finish by saving anode and updating roving ptr */
            await anodes.SaveAnode(chnode.an, chnode.an.nr, g);

            /* add fileextension preallocation */
            if (updateroving)
            {
                if (extend)
                {
                    i += extra;
                    alloc_data.rovingbit = i % 32;
                    bmoffset += (ushort)(i / 32);
                    if (bmoffset >= alloc_data.longsperbmb)
                    {
                        bmoffset -= (ushort)alloc_data.longsperbmb;
                        bmseqnr = (bmseqnr + 1) % (alloc_data.no_bmb);
                    }
                }
                else
                {
                    alloc_data.rovingbit = i;
                }

                g.RootBlock.RovingPtr = bmseqnr * alloc_data.longsperbmb + bmoffset;
            }

            //EXIT("AllocateBlocksAC");
#if DEBUG
            Pfs3Logger.Instance.Debug($"Allocation: AllocateBlocksAC Exit");
#endif
            return true;
        }
    }
}