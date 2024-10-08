﻿namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;

    public class anodes
    {
        /**********************************************************************/
        /* indexblocks                                                        */
        /**********************************************************************/

        /*
         * get indexblock nr
         * returns NULL if failure
         */
        public static async Task<CachedBlock> GetIndexBlock(ushort nr, globaldata g)
        {
#if DEBUG
            Pfs3Logger.Instance.Debug($"anodes: GetIndexBlock, seqnr = {nr}");
#endif
            uint blocknr, temp;
            CachedBlock indexblk;
            CachedBlock superblk;
            var volume = g.currentvolume;
            var andata = g.glob_anodedata;

            /* check cache (can be empty) */
            // for (var node = volume.indexblks.First; node != null; node = node.Next)
            // {
            //     indexblk = node.Value;
            //     if (indexblk.IndexBlock.seqnr == nr)
            //     {
            //         Lru.MakeLRU(indexblk, g);
            //         return node.Value;
            //     }
            // }
            if (volume.indexblksBySeqNr.ContainsKey(nr))
            {
                indexblk = volume.indexblksBySeqNr[nr];
                Lru.MakeLRU(indexblk, g);
                return indexblk;
            }

            /* not in cache, put it in
	 * first, get blocknr
	 */
            if (g.SuperMode)
            {
                /* temp is chopped by auto cast */
                temp = Init.divide(nr, andata.indexperblock);
                if ((superblk = await GetSuperBlock((ushort)temp, g)) == null)
                {
                    //DBERR(ErrorTrace(5, "GetIndexBlock", "ERR: superblock not found. %lu %lu %08lx\n", nr, andata.indexperblock, temp));
                    return null;
                }

                if ((blocknr = (uint)superblk.IndexBlock.index[temp >> 16]) == 0)
                {
                    //DBERR(ErrorTrace(5, "GetIndexBlock", "ERR: super zero. %lu %lu %08lx\n", nr, andata.indexperblock, temp));
                    return null;
                }
            }
            else
            {
                if (nr > Constants.MAXSMALLINDEXNR || (blocknr = g.RootBlock.idx.small.indexblocks[nr]) == 0)
                    return null;
            }

            /* allocate space from cache */
            if ((indexblk = await Lru.AllocLRU(g)) == null)
            {
                //DBERR(ErrorTrace(5, "GetIndexBlock", "ERR: AllocLRU. %lu %lu %08lx %lu\n", nr, andata.indexperblock, temp, blocknr));
                return null;
            }

            //DBERR(ErrorTrace(10,"GetIndexBlock","seqnr = %lu blocknr = %lu\n", nr, blocknr));
#if DEBUG
            Pfs3Logger.Instance.Debug($"anodes: GetIndexBlock, seqnr = {nr}, block nr {blocknr}");
#endif

            // if (RawRead ((UBYTE*)&indexblk->blk, RESCLUSTER, blocknr, g) != 0) {
            //     FreeLRU ((struct cachedblock *)indexblk);
            //     return NULL;
            // }
            IBlock blk;
            if ((blk = await Disk.RawRead<indexblock>(g.currentvolume.rescluster, blocknr, g)) == null)
            {
                Lru.FreeLRU(indexblk, g);
                return null;
            }

            indexblk.blk = blk;

            if (blk.id == Constants.IBLKID)
            {
                indexblk.volume = volume;
                indexblk.blocknr = blocknr;
                indexblk.used = 0;
                indexblk.changeflag = false;
                // volume.indexblks.AddFirst(indexblk);
                Macro.AddToIndexes(volume.indexblks, volume.indexblksBySeqNr, indexblk);
            }
            else
            {
                // ULONG args[5];
                // args[0] = indexblk->blk.id;
                // args[1] = IBLKID;
                // args[2] = blocknr;
                // args[3] = nr;
                // args[4] = andata.indexperblock;
                Lru.FreeLRU(indexblk, g);
                // ErrorMsg (AFS_ERROR_DNV_WRONG_INDID, args, g);
                return null;
            }

            return indexblk;
        }

        public static async Task<CachedBlock> GetSuperBlock(ushort nr, globaldata g)
        {
#if DEBUG
            Pfs3Logger.Instance.Debug($"anodes: GetSuperBlock, seqnr = {nr}");
#endif
            uint blocknr;
            CachedBlock superblk;
            var volume = g.currentvolume;

            // DBERR(blocknr = 0xffdddddd);

            /* check supermode */
            if (!g.SuperMode)
            {
                // DBERR(ErrorTrace(1, "GetSuperBlock", "ERR: Illegally entered\n"));
                return null;
            }

            /* check cache (can be empty) */
            // for (var node = volume.superblks.First; node != null; node = node.Next)
            // {
            //     superblk = node.Value;
            //     var superblk_blk = superblk.IndexBlock;
            //     if (superblk_blk.seqnr == nr)
            //     {
            //         Lru.MakeLRU(superblk, g);
            //         return node.Value;
            //     }
            // }
            if (volume.superblksBySeqNr.ContainsKey(nr))
            {
                superblk = volume.superblksBySeqNr[nr];
                Lru.MakeLRU(superblk, g);
                return superblk;
            }            

            /* not in cache, put it in
             * first, get blocknr
             */
            if (nr > Constants.MAXSUPER || (blocknr = volume.rblkextension.rblkextension.superindex[nr]) == 0)
            {
                //DBERR(ErrorTrace(1, "GetSuperBlock", "ERR: out of bounds. %lu %lu\n", nr, blocknr));
                return null;
            }

            /* allocate space from cache */
            // if (!(superblk = (struct cindexblock *)AllocLRU(g))) {
            //     DBERR(ErrorTrace(1, "GetSuperBlock", "ERR: AllocLRU error. %lu %lu\n", nr, blocknr));
            //     return NULL;
            // }
            if ((superblk = await Lru.AllocLRU(g)) == null)
            {
                return null;
            }

            // DBERR(ErrorTrace(10,"GetSuperBlock","seqnr = %lu blocknr = %lu\n", nr, blocknr));
#if DEBUG
            Pfs3Logger.Instance.Debug($"anodes: GetSuperBlock, seqnr = {nr}, block nr = {blocknr}");
#endif

            // if (RawRead ((UBYTE*)&superblk->blk, RESCLUSTER, blocknr, g) != 0) {
            //     DBERR(ErrorTrace(1, "GetSuperBlock", "ERR: read error. %lu %lu\n", nr, blocknr));
            //     FreeLRU ((struct cachedblock *)superblk);
            //     return NULL;
            // }
            //var superblk = IndexBlockReader.Read(g.currentvolume.rescluster, blocknr);
            IBlock blk;
            if ((blk = await Disk.RawRead<indexblock>(g.currentvolume.rescluster, blocknr, g)) == null)
            {
                Lru.FreeLRU(superblk, g);
                return null;
            }

            superblk.blk = blk;

            if (superblk.blk.id == Constants.SBLKID)
            {
                superblk.volume = volume;
                superblk.blocknr = blocknr;
                superblk.used = 0;
                superblk.changeflag = false;
                // Macro.MinAddHead(volume.superblks, superblk);
                Macro.AddToIndexes(volume.superblks, volume.superblksBySeqNr, superblk);
            }
            else
            {
                // ULONG args[5];
                // args[0] = superblk->blk.id;
                // args[1] = SBLKID;
                // args[2] = blocknr;
                // args[3] = nr;
                // args[4] = 0;
                Lru.FreeLRU(superblk, g);
                // ErrorMsg (AFS_ERROR_DNV_WRONG_INDID, args, g);
                return null;
            }

            return superblk;
        }
        
        public static async Task<CachedBlock> NewSuperBlock(ushort seqnr, globaldata g)
        {
            CachedBlock blok;
            var volume = g.currentvolume;

            // DBERR(blok = NULL;)

            if ((seqnr > Constants.MAXSUPER) || (blok = await Lru.AllocLRU(g)) == null)
            {
                // DBERR(ErrorTrace(1, "NewSuperBlock", "ERR: out of bounds or LRU error. %lu %p\n", seqnr, blok));
                return null;
            }

            if ((volume.rblkextension.rblkextension.superindex[seqnr] = Allocation.AllocReservedBlock(g)) == 0)
            {
                // DBERR(ErrorTrace(1, "NewSuperBlock", "ERR: AllocReservedBlock. %lu %p\n", seqnr, blok));
                Lru.FreeLRU(blok, g);
                return null;
            }
 
            // DBERR(ErrorTrace(10,"NewSuperBlock", "seqnr = %lu block = %lu\n", seqnr, volume->rblkextension->blk.superindex[seqnr]));

            volume.rblkextension.changeflag = true;

            blok.volume     = volume;
            blok.blocknr    = volume.rblkextension.rblkextension.superindex[seqnr];
            blok.used       = 0;

            if (blok.IndexBlock == null)
            {
                blok.blk = new indexblock(g);
            }
            
            var blok_cblk = blok.IndexBlock;
            blok_cblk.id     = Constants.SBLKID;
            blok_cblk.seqnr  = seqnr;
            blok.changeflag = true;
            // Macro.MinAddHead(volume.superblks, blok);
            Macro.AddToIndexes(volume.superblks, volume.superblksBySeqNr, blok);

            return blok;
        }

        /* Find out how large the anblkbitmap must be, allocate it and
 * initialise it. Free any preexisting anblkbitmap
 *
 * The anode bitmap is used for allocating anodes. It has the
 * following properties:
 * - It is  maintained in memory only (not on disk). 
 * - Intialization is lazy: all anodes are marked as available
 * - When allocation anodes (see AllocAnode), this bitmap is used
 *   to find available anodes. It then checks with the actual
 *   anode (which should be 0,0,0 if available). If it isn't really
 *   available, the anodebitmap is updated, otherwise the anode is
 *   taken.
 */
        public static async Task MakeAnodeBitmap(bool formatting, globaldata g)
        {
            CachedBlock iblk;
            CachedBlock sblk;
            int i, j, s = 0;
            uint size;
            var andata = g.glob_anodedata;

            // if (andata.anblkbitmap)
            //     FreeMemP (andata.anblkbitmap, g);            

            /* count number of anodeblocks and allocate bitmap */
            if (formatting)
            {
                i = 0;
                s = 0;
                j = 1;
            }
            else
            {
                if (g.SuperMode)
                {
                    for (s = Constants.MAXSUPER; s >= 0 && g.currentvolume.rblkextension.rblkextension.superindex[s] == 0; s--)
                    {
                    }

                    if (s < 0)
                    {
                        //goto error;					
                        throw new Exception("AFS_ERROR_ANODE_ERROR");
                    }

                    sblk = await GetSuperBlock((ushort)s, g);

                    //DBERR(if (!sblk) ErrorTrace(1, "MakeAnodeBitmap", "ERR: GetSuperBlock returned NULL!. %ld\n", s));

                    var sblk_blk = sblk.IndexBlock;
                    for (i = andata.indexperblock - 1; i >= 0 && sblk_blk.index[i] == 0; i--)
                    {
                    }
                }
                else
                {
                    for (s = 0, i = Constants.MAXSMALLINDEXNR; i >= 0 && g.RootBlock.idx.small.indexblocks[(uint)i] == 0; i--)
                    {
                    }
                }

                if (i < 0)
                {
                    // goto error;
                    throw new Exception("AFS_ERROR_ANODE_ERROR");
                }

                iblk = await GetIndexBlock((ushort)(s * andata.indexperblock + i), g);

                //DBERR(if (!iblk) ErrorTrace(1, "MakeAnodeBitmap", "ERR: GetIndexBlock returned NULL!. %ld %ld\n", s, i));
                if (iblk == null)
                {
                    throw new IOException($"MakeAnodeBitmap: ERR: GetIndexBlock returned NULL!. {s} {i}\n");
                }

                var iblk_blk = iblk.IndexBlock;
                for (j = andata.indexperblock - 1; j >= 0 && iblk_blk.index[j] == 0; j--)
                {
                }
            }

            if (g.SuperMode)
            {
                andata.maxanseqnr =
                    (uint)(s * andata.indexperblock * andata.indexperblock + i * andata.indexperblock + j);
                size = (uint)(((s * andata.indexperblock + i + 1) * andata.indexperblock + 7) / 8);
            }
            else
            {
                andata.maxanseqnr = (uint)(i * andata.indexperblock + j);
                size = (uint)(((i + 1) * andata.indexperblock + 7) / 8);
            }

            andata.anblkbitmapsize = (uint)((size + 3) & ~3);
            //andata.anblkbitmap = AllocMemP(andata.anblkbitmapsize, g);
            //#define AllocMemP(size,g) ((g->allocmemp)(size,g))
            andata.anblkbitmap = new uint[andata.anblkbitmapsize];

            for (i = 0; i < andata.anblkbitmapsize / 4; i++)
            {
                andata.anblkbitmap[i] = 0xffffffff; /* all available */
            }
        }

/*
 * Retrieve an anode from disk
 */
        public static async Task GetAnode(canode anode, uint anodenr, globaldata g)
        {
            uint temp;
            ushort seqnr, anodeoffset;
            CachedBlock ablock;
            var andata = g.glob_anodedata;

            if(g.anodesplitmode)
            {
                var split = Macro.SplitAnodenr(anodenr);
                // anodenr_t *split = (anodenr_t *)&anodenr;
                seqnr = split.seqnr;
                anodeoffset = split.offset;
            }
            else
            {
                temp		 = Init.divide(anodenr, andata.anodesperblock);
                seqnr        = (ushort)temp;				// 1e block = 0
                anodeoffset  = (ushort)(temp >> 16);
            }
	
            ablock = await big_GetAnodeBlock(seqnr, g);
            if(ablock != null)
            {
                var ablock_blk = ablock.ANodeBlock;
                anode.clustersize = ablock_blk.nodes[anodeoffset].clustersize;
                anode.blocknr     = ablock_blk.nodes[anodeoffset].blocknr;
                anode.next        = ablock_blk.nodes[anodeoffset].next;
                anode.nr          = anodenr;
            }
            else
            {
                anode.clustersize = anode.next = 0;
                //anode.blocknr     = ~0UL;
                // ErrorMsg (AFS_ERROR_DNV_ALLOC_INFO, NULL);
                // DBERR(ErrorTrace(5,"GetAnode","ERR: anode = 0x%lx\n",anodenr));
                throw new IOException($"GetAnode: ERR: anode = {anodenr}");
            }
        }
        
/* saves and anode..
*/
        public static async Task SaveAnode(canode anode, uint anodenr, globaldata g)
        {
            // anode anode
            uint temp;
            ushort seqnr, anodeoffset;
            // struct canodeblock* ablock;
            var andata = g.glob_anodedata;
            
            if (g.anodesplitmode)
            {
                // anodenr_t* split = (anodenr_t*)&anodenr;
                var split = Macro.SplitAnodenr(anodenr);
                seqnr = split.seqnr;
                anodeoffset = split.offset;
            }
            else
            {
                temp = Init.divide(anodenr, andata.anodesperblock);
                seqnr = (ushort)temp; // 1e block = 0
                anodeoffset = (ushort)(temp >> 16);
            }

            anode.nr = anodenr;

            /* Save Anode */
            var ablock = await Macro.GetAnodeBlock(seqnr, g);
            if (ablock != null)
            {
                var anode_blk = ablock.ANodeBlock;
                anode_blk.nodes[anodeoffset].clustersize = anode.clustersize;
                anode_blk.nodes[anodeoffset].blocknr = anode.blocknr;
                anode_blk.nodes[anodeoffset].next = anode.next;
                await Update.MakeBlockDirty(ablock, g);
            }
            else
            {
                //DBERR(ErrorTrace(5, "SaveAnode", "ERR: anode = 0x%lx\n", anodenr));
                // ErrorMsg (AFS_ERROR_DNV_ALLOC_BLOCK, NULL);
                throw new Exception("AFS_ERROR_DNV_ALLOC_BLOCK");
            }
        }

/* allocates an anode and marks it as reserved
 * connect is anodenr to connect to (0 = no connection)
 */
        public static async Task<uint> AllocAnode(uint connect, globaldata g)
        {
#if DEBUG
            Pfs3Logger.Instance.Debug($"anodes: AllocAnode, connect = {connect}");
#endif
            int i, j, k = 0;
            CachedBlock ablock = null;
            anode[] anodes = null;
            bool found = false;
            uint seqnr = 0, field;

            var andata = g.glob_anodedata;

            if (connect != 0 && g.anodesplitmode)
            {
                /* try to place new anode in same block */
                ablock = await Init.big_GetAnodeBlock((ushort)(seqnr = connect >> 16), g);
                if (ablock != null)
                {
                    anodes = ablock.ANodeBlock.nodes;
                    for (k = andata.anodesperblock - 1; k > -1 && !found; k--)
                        found = (anodes[k].clustersize == 0 &&
                                 anodes[k].blocknr == 0 &&
                                 anodes[k].next == 0);
                }
            }
            else
            {
                for (i = andata.curranseqnr / 32; i < andata.maxanseqnr / 32 + 1; i++)
                {
                    // DBERR(if (i >= andata.anblkbitmapsize / 4 || i < 0)
                    // 	ErrorTrace(5, "AllocAnode","ERR: anblkbitmap out of bounds %lu >= %lu\n", i, andata.anblkbitmapsize / 4));

                    field = andata.anblkbitmap[i];
                    if (field != 0)
                    {
                        for (j = 31; j >= 0; j--)
                        {
                            if ((field & (1 << j)) != 0)
                            {
                                seqnr = (uint)(i * 32 + 31 - j);
                                ablock = await Init.big_GetAnodeBlock((ushort)seqnr, g);
                                if (ablock != null)
                                {
                                    anodes = ablock.ANodeBlock.nodes;
                                    for (k = 0; k < andata.reserved && !found; k++)
                                        found = (anodes[k].clustersize == 0 &&
                                                 anodes[k].blocknr == 0 &&
                                                 anodes[k].next == 0);

                                    if (found)
                                        goto found_it;
                                    else
                                        /* mark anodeblock as full */
                                        andata.anblkbitmap[i] &= (uint)(~(1 << j));
                                }
                                /* anodeblock does not exist */
                                else goto found_it;
                            }
                        }
                    }
                }

                seqnr = andata.maxanseqnr + 1;
            }

            found_it:

            if (!found)
            {
                /* give up connect mode and try again */
                if (connect != 0)
                {
                    return await AllocAnode(0, g);
                }

                /* start over if not started from start of list;
                 * else make new block
                 */
                if (andata.curranseqnr != 0)
                {
                    andata.curranseqnr = 0;
                    return await AllocAnode(0, g);
                }
                else
                {
                    if ((ablock = await big_NewAnodeBlock((ushort)seqnr, g)) == null)
                    {
                        return 0;
                    }
                    anodes = ablock.ANodeBlock.nodes;
                    k = 0;
                }
            }
            else
            {
                if (connect != 0)
                    k++;
                else
                    k--;
            }

            anodes[k].clustersize = 0;
            anodes[k].blocknr = 0xffffffff;
            anodes[k].next = 0;

            await Update.MakeBlockDirty(ablock, g);
            andata.curranseqnr = (ushort)seqnr;

            if (g.anodesplitmode)
                return (seqnr << 16 | (uint)k);
            else
                return (uint)(seqnr * andata.anodesperblock + k);
        }
        
/* MODE_BIG has indexblocks, and negative blocknrs indicating freenode
** blocks instead of anodeblocks
*/
        public static async Task<CachedBlock> big_GetAnodeBlock(ushort seqnr, globaldata g)
        {
#if DEBUG
            Pfs3Logger.Instance.Debug($"anodes: GetAnodeBlock, seqnr = {seqnr}");
#endif
            uint blocknr;
            uint temp;
            CachedBlock ablock;
            CachedBlock indexblock;
            var volume = g.currentvolume;
            var andata = g.glob_anodedata;

            temp = Init.divide(seqnr, andata.indexperblock);

            /* not in cache, put it in */
            /* get the indexblock */
            if ((indexblock = await GetIndexBlock((ushort)temp /*& 0xffff*/, g)) == null)
            {
                // DBERR(ErrorTrace(5, "GetAnodeBlock","ERR: index not found. %lu %lu %08lx\n", seqnr, andata.indexperblock, temp));
                return null;
            }

            /* get blocknr */
            if ((blocknr = (uint)indexblock.IndexBlock.index[temp >> 16]) == 0)
            {
                // DBERR(ErrorTrace(5,"GetAnodeBlock","ERR: index zero %lu %lu %08lx\n", seqnr, andata.indexperblock, temp));
                return null;
            }

            /* check cache */
            // ablock = Lru.CheckCache(volume.anblks, Constants.HASHM_ANODE, blocknr, g);
            ablock = Lru.CheckCache(volume.anblks, blocknr, g);
            if (ablock != null)
                return ablock;

            if ((ablock = await Lru.AllocLRU(g)) == null)
            {
                // DBERR(ErrorTrace(5,"GetAnodeBlock","ERR: alloclru failed\n"));
                return null;
            }

            // DBERR(ErrorTrace(10,"GetAnodeBlock", "seqnr = %lu blocknr = %lu\n", seqnr, blocknr));
#if DEBUG
            Pfs3Logger.Instance.Debug($"anodes: GetAnodeBlock, seqnr = {seqnr}, blocknr = {blocknr}");
#endif

            /* read it */
            IBlock blk;
            if ((blk = await Disk.RawRead<anodeblock>(g.currentvolume.rescluster, blocknr, g)) == null)
            {
                // DB(Trace(5,"GetAnodeBlock","Read ERR: seqnr = %lu blocknr = %lx\n", seqnr, blocknr));
                Lru.FreeLRU(ablock, g);
                return null;
            }

            ablock.blk = blk;

            /* check it */
            if (ablock.blk.id != Constants.ABLKID)
            {
                // ULONG args[2];
                // args[0] = ablock->blk.id;
                // args[1] = blocknr;
                Lru.FreeLRU(ablock, g);
                // ErrorMsg (AFS_ERROR_DNV_WRONG_ANID, args, g);
                return null;
            }

            /* initialize it */
            ablock.volume     = volume;
            ablock.blocknr    = blocknr;
            ablock.used       = 0;
            ablock.changeflag = false;
            // Macro.Hash(ablock, volume.anblks, Constants.HASHM_ANODE);
            Macro.Hash(ablock, volume.anblks);

            return ablock;
        }
        
        public static async Task<CachedBlock> big_NewAnodeBlock(ushort seqnr, globaldata g)
        {
            /* MODE_BIG has difference between anodeblocks and fnodeblocks*/

            CachedBlock blok;
            var volume = g.currentvolume;
            var andata = g.glob_anodedata;
            CachedBlock indexblock;
            uint indexblnr;
            int blocknr;
            ushort indexoffset, oldlock;

            /* get indexblock */
            indexblnr = (uint)(seqnr / andata.indexperblock);
            indexoffset = (ushort)(seqnr % andata.indexperblock);
            if ((indexblock = await GetIndexBlock((ushort)indexblnr, g)) == null) {
                if ((indexblock = await NewIndexBlock((ushort)indexblnr, g)) == null) {
                    // DBERR(ErrorTrace(10,"big_NewAnodeBlock","ERR: NewIndexBlock %lu %lu %lu %lu\n", seqnr, indexblnr, indexoffset, andata.indexperblock));
                    return null;
                }
            }

            oldlock = indexblock.used;
            Cache.LOCK(indexblock, g);
            if ((blok = await Lru.AllocLRU(g)) == null || (blocknr = (int)Allocation.AllocReservedBlock(g)) == 0 ) {
                // DBERR(ErrorTrace(10,"big_NewAnodeBlock","ERR: AllocLRU/AllocReservedBlock %lu %lu %lu\n", seqnr, indexblnr, indexoffset));
                indexblock.used = oldlock;         // unlock block
                return null;
            }

            // DBERR(ErrorTrace(10,"big_NewAnodeBlock", "seqnr = %lu block = %lu\n", seqnr, blocknr));

            indexblock.IndexBlock.index[indexoffset] = blocknr;
            
            // CHANGE: Original pfs3 code adds new anode block to index block, but the index block does not
            // set change flag to true indicating it contains changes. The change flag is now set to ensure
            // the cached index block is properly marked changed to avoid it gets overwritten by other blocks
            // as it appears unchanged with changed flag set to false.
            indexblock.changeflag = true;

            blok.volume     = volume;
            blok.blocknr    = (uint)blocknr;
            blok.used       = 0;
            blok.blk = new anodeblock(g)
            {
                id = Constants.ABLKID,
                seqnr = seqnr
            };
            blok.changeflag = true;
            // Macro.Hash(blok, volume.anblks, Constants.HASHM_ANODE);
            Macro.Hash(blok, volume.anblks);
            await Update.MakeBlockDirty(indexblock, g);
            indexblock.used = oldlock;         // unlock block

            ReallocAnodeBitmap(seqnr, g);
            return blok;
        }
        
        public static async Task<CachedBlock> NewIndexBlock(ushort seqnr, globaldata g)
        {
            CachedBlock blok;
            CachedBlock superblok = null;
            var volume = g.currentvolume;
            var andata = g.glob_anodedata;
            uint superblnr = 0;
            int blocknr;
            ushort superoffset = 0;

            if (g.SuperMode)
            {
                superblnr = (uint)(seqnr / andata.indexperblock);
                superoffset = (ushort)(seqnr % andata.indexperblock);
                if ((superblok = await GetSuperBlock((ushort)superblnr, g)) == null)
                {
                    if ((superblok = await NewSuperBlock((ushort)superblnr, g)) == null)
                    {
                        // DBERR(ErrorTrace(1, "NewIndexBlock", "ERR: Super not found. %lu %lu %lu %lu\n", seqnr, andata.indexperblock, superblnr, superoffset));
                        return null;
                    }
                    // else
                    // {
                    //     DBERR(ErrorTrace(1, "NewIndexBlock", "OK. %lu %lu %lu %lu\n", seqnr, andata.indexperblock, superblnr, superoffset));		
                    // }
                }

                Cache.LOCK(superblok, g);
            }
            else if (seqnr > Constants.MAXSMALLINDEXNR) {
                return null;
            }

            if ((blok = await Lru.AllocLRU(g)) == null || (blocknr = (int)Allocation.AllocReservedBlock(g)) == 0)
            {
                // DBERR(ErrorTrace(1, "NewIndexBlock", "ERR: AllocLRU/AllocReservedBlock. %lu %lu %lu %lu\n", seqnr, blocknr, superblnr, superoffset));
                if (blok != null)
                    Lru.FreeLRU(blok, g);
                return null;
            }

            // DBERR(ErrorTrace(10,"NewIndexBlock", "seqnr = %lu block = %lu\n", seqnr, blocknr));

            if (g.SuperMode) {
                superblok.IndexBlock.index[superoffset] = blocknr;
                await Update.MakeBlockDirty(superblok, g);
            } else {
                g.RootBlock.idx.small.indexblocks[seqnr] = (uint)blocknr;
                volume.rootblockchangeflag = true;
            }

            blok.volume     = volume;
            blok.blocknr    = (uint)blocknr;
            blok.used       = 0;
            // var blok_blk = blok.IndexBlock;
            blok.blk = new indexblock(g)
            {
                id = Constants.IBLKID,
                seqnr = seqnr
            };
            blok.changeflag = true;
            // Macro.MinAddHead(volume.indexblks, blok);
            Macro.AddToIndexes(volume.indexblks, volume.indexblksBySeqNr, blok);

            return blok;
        }
        
/* test if new anodeseqnr causes change in anblkbitmap */
        public static void ReallocAnodeBitmap(uint newseqnr, globaldata g)
        {
            uint newsize;
            int t;
            var andata = g.glob_anodedata;

            if (newseqnr > andata.maxanseqnr)
            {
                andata.maxanseqnr = newseqnr;
                newsize = ((newseqnr/andata.indexperblock + 1) * andata.indexperblock + 7) / 8;
                if (newsize > andata.anblkbitmapsize)
                {
                    newsize = (uint)((newsize + 3) & ~3);   /* longwords */
                    // newbitmap = AllocMemP (newsize, g);
                    var newbitmap = new uint[newsize];
                    for (t = 0; t < newsize / 4; t++)
                        newbitmap[t] = 0xffffffff;
                    //memcpy (newbitmap, andata.anblkbitmap, andata.anblkbitmapsize);
                    //FreeMemP (andata.anblkbitmap, g);
                    andata.anblkbitmap = newbitmap;
                    andata.anblkbitmapsize = newsize;
                }
            }
        }
        
/* Remove anode from anodechain
 * If previous==0, anode->next becomes head.
 * Otherwise previous->next becomes anode->next.
 * Anode is freed.
 *
 * Arguments:
 * anode = anode to be removed
 * previous = previous in chain; or 0 if anode is head
 * head = anodenr of head of list
 */
        public static async Task RemoveFromAnodeChain(canode anode, uint previous, uint head, globaldata g)
        {
            canode sparenode = new canode();

            if(previous != 0)
            {
                await GetAnode(sparenode, previous, g);
                sparenode.next = anode.next;
                await SaveAnode(sparenode, sparenode.nr, g);
                await FreeAnode(anode.nr, g);
            }
            else
            {
                /* anode is head of list (check both tails here) */
                if (anode.next != 0)
                {
                    /* There is a next entry -> becomes head */
                    await GetAnode(sparenode, anode.next, g);
                    await SaveAnode(sparenode, head, g); // overwrites [anode]
                    await FreeAnode(anode.next, g);  
                }
                else
                {
                    /* No anode->next: Free list. */
                    await FreeAnode(head, g);
                }
            }
        }
        
/*
 * frees an anode for later reuse
 * universal version
 */
        public static async Task FreeAnode(uint anodenr, globaldata g)
        {
            canode anode = new canode();
            var andata = g.glob_anodedata;

            /* don't kill reserved anodes */
            if (anodenr < Constants.ANODE_USERFIRST) 
            {
                //anode.blocknr = (uint)~0L;
                anode.blocknr = UInt32.MaxValue;
            }

            await SaveAnode(anode, anodenr, g);
            andata.anblkbitmap[(anodenr>>16)/32] |= (uint)(1 << (31 - (int)(((anodenr>>16) % 32))));
        }
        
/*
 * makes anodechain
 */
        public static async Task<anodechain> MakeAnodeChain(uint anodenr, globaldata g)
        {
            // struct anodechain *ac;
            // struct anodechainnode *node, *newnode;

            // ENTER("MakeAnodeChain");
            // if (!(ac = AllocMemP(sizeof(struct anodechain), g)))
            // return NULL;
            var ac = new anodechain
            {
                refcount = 0
            };

            var node = ac.head;
            await GetAnode(node.an, anodenr, g);
            while (node.an != null && node.an.next != 0)
            {
                // if (!(newnode = AllocMemP(sizeof(struct anodechainnode), g)))
                // goto failure;
                var newnode = new anodechainnode();
                node.next = newnode;
                await GetAnode(newnode.an, node.an.next, g);
                node = newnode;
            }

            Macro.MinAddHead(g.currentvolume.anodechainlist, ac);
            return ac;
	
            // failure:
            // FreeAnodeChain(ac, g);  
            // return NULL;
        }
        
        /*
 * Get anodechain of anodenr, making it if necessary.
 * Returns chain or NULL for failure
 */
        public static async Task<anodechain> GetAnodeChain(uint anodenr, globaldata g)
        {
            anodechain ac;

            if ((ac = FindAnodeChain(anodenr, g)) == null)
                ac = await MakeAnodeChain(anodenr, g);
            if (ac != null)
                ac.refcount++;

            return ac;
        }

        /*
 * search anodechain. 
 * Return anodechain found, or 0 if not found
 */
        public static anodechain FindAnodeChain (uint anodenr, globaldata g)
        {
            anodechain chain;

            // ENTER("FindAnodeChain");
            for (var node = Macro.HeadOf(g.currentvolume.anodechainlist); node != null; node = node.Next)
            {
                chain = node.Value;
                if (chain.head.an.nr == anodenr)
                    return chain;
            }

            return null;
        }
        
/*
 * Tries to fetch the block that follows after anodeoffset. Returns
 * success and anodeoffset is updated.
 */
        public static async Task<Tuple<bool, uint>> NextBlock(canode anode, uint anodeoffset, globaldata g)
        {
            anodeoffset++;
            return await CorrectAnode(anode, anodeoffset, g);
        }
        
/* 
 * Correct anodeoffset overflow
 */
        /// <summary>
        /// Note: anodeoffset can be changed in method and async doesn't allow ref,
        /// therefore both boolean and updated anodeoffset is returned
        /// </summary>
        /// <param name="anode"></param>
        /// <param name="anodeoffset"></param>
        /// <param name="g"></param>
        /// <returns></returns>
        public static async Task<Tuple<bool, uint>> CorrectAnode (canode anode, uint anodeoffset, globaldata g)
        {
            while(anodeoffset >= anode.clustersize)
            {
                if (anode.next == 0)
                {
                    return new Tuple<bool, uint>(false, anodeoffset);
                }

                anodeoffset -= anode.clustersize;
                await GetAnode(anode, anode.next, g);
            }

            return new Tuple<bool, uint>(true, anodeoffset);
        }
        
        /* 
 * Correct anodeoffset overflow. Corrects anodechainnode pointer pointed to by acnode.
 * Returns success. If correction was not possible, acnode will be the tail of the
 * anodechain. Anodeoffset is updated to point to a block within the current
 * anodechainnode.
 */
        public static bool CorrectAnodeAC(ref anodechainnode acnode, ref uint anodeoffset, globaldata g)
        {
            while (anodeoffset >= acnode.an.clustersize)
            {
                if (acnode.next == null)
                    return false;

                anodeoffset -= acnode.an.clustersize;
                acnode = acnode.next;
            }

            return true;
        }

/*
 * Called when a reference to an anodechain ceases to exist
 */
        public static void DetachAnodeChain(anodechain chain, globaldata g)
        {
            chain.refcount--;
            if (chain.refcount == 0)
                FreeAnodeChain(chain, g);
        }
        
/*
 * Free an anodechain. Anodechain will be removed from list if it is
 * in the list.
 */
        public static void FreeAnodeChain(anodechain chain, globaldata g)
        {
            // struct anodechainnode *node, *nextnode;
            //
            // ENTER("FreeAnodeChain");
            // for (node=chain->head.next; node; node=nextnode)
            // {
            //     nextnode = node->next;
            //     FreeMemP (node, g);
            // }

            if (chain != null)
            {
                Macro.MinRemove(chain, g); // remove from any list
            }
            //
            // FreeMemP (chain, g);
        }
        
/*
 * Tries to fetch the block that follows after anodeoffset. Returns success,
 * updates anodechainpointer and anodeoffset. If failed, acnode will point to
 * the tail of the anodechain
 */
        public static bool NextBlockAC(ref anodechainnode acnode, ref uint anodeoffset, globaldata g)
        {
            anodeoffset++;
            return CorrectAnodeAC(ref acnode, ref anodeoffset, g);
        }        
    }
}