namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Blocks;

    public static class Macro
    {
        public static bool IsSameOI(objectinfo oi1, objectinfo oi2)
        {
            return oi1.file.direntry.Position == oi2.file.direntry.Position && 
                   oi1.file.dirblock.blocknr == oi2.file.dirblock.blocknr;
        }
        
        public static void MarkDataDirty(int i, globaldata g) => g.dc.ref_[i].dirty = true;
            
        // comment: de is struct direntry *
        //public static int COMMENT(direntry de) => de.startofname + de.Name.Length;
        
        public static bool IsRoot(objectinfo oi) => oi == null || oi.volume.root == 0;
        public static bool IsRootA(objectinfo oi) => oi.volume.root == 0;

        public static uint BLOCKSIZE(globaldata g) => g.blocksize;
        public static uint BLOCKSIZEMASK(globaldata g) => g.blocksize - 1;
        public static ushort BLOCKSHIFT(globaldata g) => g.blockshift;
        public static int DIRECTSIZE(globaldata g) => g.directsize;
        
        public static bool IsVolumeEntry(IEntry e) => e.ListEntry.type.flags.type == Constants.ETF_VOLUME;
        public static bool IsFileEntry(IEntry e) => e.ListEntry.type.flags.type == Constants.ETF_FILEENTRY;
        public static bool IsLockEntry(IEntry e) => e.ListEntry.type.flags.type == Constants.ETF_LOCK;
        public static bool SHAREDLOCK(IEntry e) => e.ListEntry.type.flags.access <= 1;

        // public static uint ANODENR(fileentry fe) => fe.anodenr;
        public static uint FIANODENR(fileinfo fi) => fi.direntry.anode;
        
        // #define IsRollover(oi) ((IPTR)(oi).file.direntry>2 && ((oi).file.direntry->type==ST_ROLLOVERFILE))
        public static bool IsRollover(objectinfo oi) =>
            oi.file.direntry != null && oi.file.direntry.type == Convert.ToSByte(Constants.ST_ROLLOVERFILE);

        public static int MKBADDR(uint x) => (int)x >> 2;
        
        public static void Lock(CachedBlock blk, globaldata g) => blk.used = g.locknr;
        public static void UnlockAll(globaldata g) => g.locknr++;
        public static bool IsLocked(CachedBlock blk, globaldata g) => blk.used == g.locknr;

        /// <summary>
        /// Note: first get dirblock from cached block: CachedBlock.direntry
        /// </summary>
        /// <param name="blok"></param>
        /// <returns></returns>
        // public static direntry FIRSTENTRY(dirblock blk) => DirEntryReader.Read(blk.entries, 0);

        /* get next directory entry */
        //public static int NEXTENTRY(direntry de) => ((struct direntry*)((UBYTE*)(de) + (de)->next))
        // public static direntry NEXTENTRY(dirblock blk, direntry de)
        // {
        //     return DirEntryReader.Read(blk.entries, de.Offset + de.next);
        // }         
        // public static int DB_HEADSPACE(globaldata g) => SizeOf.DirBlock.Struct(g);
        public static int DB_ENTRYSPACE(globaldata g) => SizeOf.DirBlock.Entries(g);
        
        //public static int GetAnodeBlock(uint a, uint b, globaldata g) => anodes.big_GetAnodeBlock() (g.getanodeblock)(a, b);
        
/*
;-----------------------------------------------------------------------------
;	ULONG divide (ULONG d0, UWORD d1)
;-----------------------------------------------------------------------------
*/
        public static uint divide(uint d0, ushort d1)
        {
            uint q = d0 / d1;
            /* NOTE: I doubt anything depends on this, but lets simulate 68k divu overflow anyway - Piru */
            if (q > 65535UL) return d0;
            return ((d0 % d1) << 16) | q;
        }
        
        
/* max length of filename, diskname and comment
 * FNSIZE is 108 for compatibilty. Used for searching
 * files.
 */
        public static int FNSIZE = 108;
        public static int PATHSIZE = 256;
        public static int FILENAMESIZE(globaldata g) => g.fnsize;
        public static int DNSIZE = 32;
        public static int CMSIZE = 80;
        public static int MAX_ENTRYSIZE = SizeOf.DirEntry.Struct + FNSIZE + CMSIZE + 34;
        
        /*
         * IPTR
There is another important typedef, IPTR. It is really important in AROS, as it the only way to declare a field that can contain both an integer and a pointer.

Note

AmigaOS does not know this typedef. If you are porting a program from AmigaOS to AROS, you have to search your source for occurrences of ULONG that can also contain pointers, and change them into IPTR. If you don't do this, your program will not work on systems which have pointers with more than 32 bits (for example DEC Alphas that have 64-bit pointers).

BPTR
         */

        // (IPTR)(oi).file.direntry > 2 && 
        public static bool IsSoftLink(objectinfo oi) =>oi.file.direntry.type == Constants.ST_SOFTLINK;
        public static bool IsRealDir(objectinfo oi) => oi.file.direntry.type == Constants.ST_USERDIR;
        public static bool IsDir(objectinfo oi) => oi.file.direntry.type > 0;
        public static bool IsFile(objectinfo oi) => oi.file.direntry.type <= 0;
        public static bool IsVolume(objectinfo oi) => oi.volume.root == 0;
        
/* macros on cachedblocks */
        public static bool IsDirBlock(IBlock blk) => blk.id == Constants.DBLKID;
        public static bool IsAnodeBlock(IBlock blk) => blk.id == Constants.ABLKID;
        public static bool IsIndexBlock(IBlock blk) => blk.id == Constants.IBLKID;
        public static bool IsBitmapBlock(IBlock blk) => blk.id == Constants.BMBLKID;
        public static bool IsBitmapIndexBlock(IBlock blk) => blk.id == Constants.BMIBLKID;
        public static bool IsDeldir(IBlock blk) => blk.id == Constants.DELDIRID;
        public static bool IsSuperBlock(IBlock blk) => blk.id == Constants.SBLKID;

        public static bool IsDelDir(objectinfo oi) => oi.deldir.special == Constants.SPECIAL_DELDIR;
        public static bool IsDelFile(objectinfo oi) => oi.deldir.special == Constants.SPECIAL_DELFILE;
        
        /* predefined anodes */
        public static int ANODE_EOF = 0;
        public static int ANODE_RESERVED_1 = 1;	// not used by MODE_BIG
        public static int ANODE_RESERVED_2 = 2;	// not used by MODE_BIG
        public static int ANODE_RESERVED_3 = 3;	// not used by MODE_BIG
        public static int ANODE_BADBLOCKS = 4;	// not used yet
        public static int ANODE_ROOTDIR = 5;
        public static int ANODE_USERFIRST = 6;        
        
        // #define alloc_data (g->glob_allocdata)
        // #define andata (g->glob_anodedata)
        // #define lru_data (g->glob_lrudata)
        // #define FILENAMESIZE (g->fnsize)
        
        public static bool ReservedAreaIsLocked(globaldata g)
        {
            return g.glob_allocdata.res_alert;
        }
        
        /* macros on cachedblocks */
        public static bool IsDirBlock(CachedBlock blk) => blk.blk.id == Constants.DBLKID;
        public static bool IsAnodeBlock(CachedBlock blk) => blk.blk.id == Constants.ABLKID;
        public static bool IsIndexBlock(CachedBlock blk) => blk.blk.id == Constants.IBLKID;
        public static bool IsBitmapBlock(CachedBlock blk) => blk.blk.id == Constants.BMBLKID;
        public static bool IsBitmapIndexBlock(CachedBlock blk) => blk.blk.id == Constants.BMIBLKID;
        public static bool IsDeldir(CachedBlock blk) => blk.blk.id == Constants.DELDIRID;
        public static bool IsSuperBlock(CachedBlock blk) => blk.blk.id == Constants.SBLKID;

        public static void MinRemove(IEntry node, globaldata g)
        {
            var volume = g.currentvolume;
            volume.fileentries.Remove(node);
        }
        
        public static void MinRemove(anodechain anodechain, globaldata g)
        {
            g.currentvolume.anodechainlist.Remove(anodechain);
        }

        public static void MinRemove(CachedBlock node, globaldata g)
        {
#if DEBUG
            Pfs3Logger.Instance.Debug($"Macro: MinRemove CachedBlock block nr {node.blocknr} ({node.GetHashCode()})");
#endif
            // #define MinRemove(node) Remove((struct Node *)node)
            // remove() removes the node from any list it' added to (Amiga MinList exec)

            var volume = g.currentvolume;
            for (var i = 0; i < volume.anblks.Length; i++)
            {
                volume.anblks[i].Remove(node);
            }

            for (var i = 0; i < volume.dirblks.Length; i++)
            {
                volume.dirblks[i].Remove(node);
            }

            volume.indexblks.Remove(node);
            volume.bmblks.Remove(node);
            volume.superblks.Remove(node);
            volume.deldirblks.Remove(node);
            volume.bmindexblks.Remove(node);
        }

        /// <summary>
        /// Remove cached block from lru lists containing it
        /// </summary>
        /// <param name="node"></param>
        /// <param name="g"></param>
        public static void MinRemoveLru(CachedBlock node, globaldata g)
        {
#if DEBUG
            Pfs3Logger.Instance.Debug($"Macro: MinRemoveLru CachedBlock block nr {node.blocknr} ({node.GetHashCode()})");
#endif
            foreach (var lruCachedBlock in g.glob_lrudata.LRUpool.Where(x => x.cblk == node).ToList())
            {
                g.glob_lrudata.LRUpool.Remove(lruCachedBlock);
            }
            
            foreach (var lruCachedBlock in g.glob_lrudata.LRUqueue.Where(x => x.cblk == node).ToList())
            {
                g.glob_lrudata.LRUqueue.Remove(lruCachedBlock);
            }
        }
        
        /// <summary>
        /// add node to head of list. Amiga MinList exec
        /// </summary>
        /// <param name="list"></param>
        /// <param name="node"></param>
        /// <typeparam name="T"></typeparam>
        public static void MinAddHead<T>(LinkedList<T> list, T node)
        {
            // #define MinAddHead(list, node)  AddHead((struct List *)(list), (struct Node *)(node))
            list.AddFirst(node);
        }

        public static LinkedListNode<T> HeadOf<T>(LinkedList<T> list)
        {
            // #define HeadOf(list) ((void *)((list)->mlh_Head))
            return list.First;
        }

        public static bool IsMinListEmpty<T>(LinkedList<T> list) => list.Count == 0;        
        
        public static async Task<CachedBlock> GetAnodeBlock(ushort seqnr, globaldata g)
        {
            // #define GetAnodeBlock(a, b) (g->getanodeblock)(a,b)
            // g->getanodeblock = big_GetAnodeBlock;
            return await Init.big_GetAnodeBlock(seqnr, g);
        }
        
        /// <summary>
        /// convert anodenr to subclass with seqnr and offset
        /// </summary>
        /// <param name="anodenr"></param>
        /// <returns></returns>
        public static anodenr SplitAnodenr(uint anodenr)
        {
            // typedef struct
            // {
            //     UWORD seqnr;
            //     UWORD offset;
            // } anodenr_t;
            return new anodenr
            {
                seqnr = (ushort)(anodenr >> 16),
                offset = (ushort)(anodenr & 0xFFFF)
            };
        }

        public static bool InPartition(uint blk, globaldata g)
        {
            return blk >= g.firstblock && blk <= g.lastblock;
        }
        
        public static void Hash(CachedBlock blk, LinkedList<CachedBlock>[] list, int mask)
        {
            // #define Hash(blk, list, mask)                           \
            //             MinAddHead(&list[(blk->blocknr/2)&mask], blk)
            MinAddHead(list[(blk.blocknr / 2) & mask], blk);
        }
        
        /*
         * Hashing macros
         */
        public static void ReHash(CachedBlock blk, LinkedList<CachedBlock>[] list, int mask, globaldata g)
        {
            // #define ReHash(blk, list, mask)                         \
            // {                                                       \
            //     MinRemove(blk);                                     \
            //     MinAddHead(&list[(blk->blocknr/2)&mask], blk);      \
            // }
            
            MinRemove(blk, g);
            MinAddHead(list[(blk.blocknr / 2) & mask], blk);
        }        
        
        public static bool IsEmptyDBlk(CachedBlock blk, globaldata g)
        {
            // #define FIRSTENTRY(blok) ((struct direntry*)((blok)->blk.entries))
            // #define IsEmptyDBlk(blk) (FIRSTENTRY(blk)->next == 0)
            //return FIRSTENTRY(blk.dirblock).next == 0;
            return blk.dirblock.DirEntries.Count == 0;
        }
        
        public static bool IsUpdateNeeded(int rtbf_threshold, globaldata g)
        {
/* checks if update is needed now */
// #define IsUpdateNeeded(rtbf_threshold)                              \
//         ((alloc_data.rtbf_index > rtbf_threshold) ||                    \
//         (g->rootblock->reserved_free < RESFREE_THRESHOLD + 5 + alloc_data.tbf_resneed))         \

            var alloc_data = g.glob_allocdata;
            return ((alloc_data.rtbf_index > rtbf_threshold) ||
                    (g.RootBlock.ReservedFree < Constants.RESFREE_THRESHOLD + 5 + alloc_data.tbf_resneed));
        }
    }
}