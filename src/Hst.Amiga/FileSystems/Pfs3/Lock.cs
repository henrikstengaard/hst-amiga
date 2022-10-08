namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;

    public static class Lock
    {
/* MakeListEntry
**
** Allocated filentry structure and fill it with data from objectinfo and
** listtype. The result should be freed with FreeListEntry.
**
** input : - info: objectinfo of object 
**		 - type: desired type (readlock, writelock, readfe, writefe)
**
** result: the fileentry, or NULL if failure
*/
        public static async Task<listentry> MakeListEntry(objectinfo info, ListType type, globaldata g)
        {
            var newinfo = new objectinfo();
            //uint size;
            var extrafields = new extrafields();
//#if DELDIR
            deldirentry dde = new deldirentry();
//#endif

            //ENTER("MakeListEntry");

            // alloceren fileentry
            // switch (type.flags.type)
            // {
            //     case Constants.ETF_FILEENTRY:
            //         size = SizeOf.FileEntry;
            //         break;
            //     case Constants.ETF_VOLUME:
            //     case Constants.ETF_LOCK:
            //         size = (uint)SizeOf.LockEntry.Struct;
            //         break;
            //     default:
            //         return null;
            // }

            // DB(Trace(1,"MakeListEntry","size = %lx\n",size));

//#if DELDIR
            if (Macro.IsDelDir(info) || Macro.IsVolume(info) || Macro.IsDir(info))
//#else
//	if (Macro.IsVolume(info) || Macro.IsDir(info))
//#endif
            {
                type.flags.dir = 1;
            }

            /* softlinks cannot directly be opened */
//#if DELDIR
	        if (info.deldir.special > Constants.SPECIAL_DELFILE && info.file.direntry.type == Constants.ST_SOFTLINK)
// #else
//             if (!Macro.IsVolume(info) && info.file.direntry.type == Constants.ST_SOFTLINK)
// #endif
            {
                throw new IOException("ERROR_IS_SOFT_LINK");
                // return NULL;
            }

            var listentry = new listentry();
            var fileentry = new fileentry();
            // if (!(listentry = AllocMemP (size, g)))
            // {
            // 	*error = ERROR_NO_FREE_STORE;
            // 	return NULL;
            // }

            /* go after link and fetch the fileinfo of the real object
             * (stored in 'newinfo'
             */
//#if DELDIR
            if (info.deldir.special > Constants.SPECIAL_DELFILE && (
//#else
//	if (!Macro.IsVolume(info) && (
//#endif
                    (info.file.direntry.type == Constants.ST_LINKFILE) ||
                    (info.file.direntry.type == Constants.ST_LINKDIR)))
            {
                var linknode = new canode();

                /* The clustersize of the linknode (direntry.anode) 
                 * actually is the anodenr of the directory the linked to
                 * object is in. The object can be found by searching for
                 * 'anode == objectid'. This objectid can be found in
                 * the extrafields
                 */
                Directory.GetExtraFields(info.file.direntry, extrafields);
                await anodes.GetAnode(linknode, info.file.direntry.anode, g);
                if (!await FetchObject(linknode.clustersize, extrafields.link, newinfo, g))
                {
                    throw new IOException("ERROR_OBJECT_NOT_FOUND");
                }
            }
            else
            {
                newinfo = info;
            }

            // general
            listentry.type = type;
//#if DELDIR
            switch (newinfo.delfile.special)
            {
                case 0:
                    listentry.anodenr = Constants.ANODE_ROOTDIR;
                    break;

                case Constants.SPECIAL_DELDIR:
                    listentry.anodenr = 0;
                    break;

                case Constants.SPECIAL_DELFILE:
                    dde = await Directory.GetDeldirEntryQuick(newinfo.delfile.slotnr, g);
                    listentry.anodenr = dde.anodenr;
                    break;

                default:
                    listentry.anodenr = newinfo.file.direntry.anode;
                    break;
            }
// #else
// 	listentry->anodenr = (newinfo.file.direntry) ? (newinfo.file.direntry->anode) : ANODE_ROOTDIR;
// #endif

            listentry.info = newinfo;
            
            // TODO: DOS communication, not sure this is needed 
            //listentry.filelock.fl_Access = (type.flags.access & 2) != 0 ? Constants.EXCLUSIVE_LOCK : Constants.SHARED_LOCK;
            //listentry.filelock.fl_Task = g.msgport;
            //listentry.filelock.fl_Volume = Macro.MKBADDR(g.currentvolume.devlist);
            
            listentry.volume = g.currentvolume;

            // type specific
            switch (type.flags.type)
            {
                case Constants.ETF_VOLUME:
                    listentry.filelock.fl_Key = 0;
                    // listentry->lock.fl_Volume = MKBADDR(newinfo.volume.volume->devlist);
                    listentry.volume		  = newinfo.volume.volume;
                    break;

                case Constants.ETF_LOCK:
                    /* every dirlock MUST have a different fl_Key (DOPUS!) */
                    listentry.filelock.fl_Key = (int)listentry.anodenr;
                    // listentry->lock.fl_Volume = MKBADDR(newinfo.file.dirblock->volume->devlist);
                    listentry.volume = newinfo.file.dirblock.volume;
                    break;

                case Constants.ETF_FILEENTRY:
//#define fe ((fileentry_t *)listentry)
                    //var fe = listentry as fileentry;
                    listentry.filelock.fl_Key = (int)listentry.anodenr;
                    // listentry->lock.fl_Volume = MKBADDR(MKBADDR(newinfo.file.dirblock->volume->devlist);
                    listentry.volume = newinfo.file.dirblock.volume;
                    fileentry.originalsize = Macro.IsDelFile(newinfo)
                        ? Directory.GetDDFileSize(dde, g)
                        : Directory.GetDEFileSize(newinfo.file.direntry, g);

                    /* Get anodechain. If it fails anodechain will become NULL. This has to be
                     * taken into account by functions that use the chain
                     */
                    fileentry.anodechain = await anodes.GetAnodeChain(listentry.anodenr, g);
                    fileentry.currnode = fileentry.anodechain.head;

//#if ROLLOVER
                    /* Rollover file: set offset to rollfileoffset */
                    /* check for rollover files */
                    if (Macro.IsRollover(newinfo))
                    {
                        Directory.GetExtraFields(newinfo.file.direntry, extrafields);
                        await Disk.SeekInFile(fileentry, (int)extrafields.rollpointer, Constants.OFFSET_BEGINNING, g);
                    }

// #endif /* ROLLOVER */
// #undef fe
                    break;

                default:
                    listentry = null;
                    return null;
            }

            return listentry;
        }
        
/* AddListEntry
**
** Checks if the listentry causes access conflicts
** Adds the entry to the locklist 
*/
        public static bool AddListEntry(listentry entry, globaldata g)
        {
            //DB(Trace(1,"AddListEntry","fe = %lx\n", entry->volume->fileentries.mlh_Head));

            if (entry==null)
                return false;

            if (AccessConflict(entry))
            {
                //DB(Trace(1,"AddListEntry","found accessconflict!"));
                return false;
            }

            var volume = entry.volume;

            /* add to head of list; als link locks using BPTRs */
            // if (!Macro.IsMinListEmpty(volume.fileentries))
            // {
            //     entry.filelock.fl_Link = MKBADDR(&(((listentry_t *)Macro.HeadOf(volume.fileentries))->lock))
            // }
            // else
            // {
            //     entry.filelock.fl_Link = 0;
            // }

            Macro.MinAddHead(volume.fileentries, entry);

            return true; 
        }
        
/*
 * Search object by anodenr in directory 
 * in: diranodenr, target: anodenr of target and the anodenr of the directory to search in
 * out: result: an objectinfo to the object, if found
 * returns: success
 */
        public static async Task<bool> FetchObject(uint diranodenr, uint target, objectinfo result, globaldata g)
        {
            canode anode = new canode();
            CachedBlock dirblock = null;
            direntry de = null;
            uint anodeoffset = 0;
            bool eod = false, found = false;

            /* Get directory and find object */
            await anodes.GetAnode(anode, diranodenr, g);
            while (!found && !eod)
            {
                dirblock = await Directory.LoadDirBlock(anode.blocknr + anodeoffset, g);
                if (dirblock != null)
                {
                    var blk = dirblock.dirblock;
                    de = Macro.FIRSTENTRY(blk);
                    while (de.next > 0)
                    {
                        if (!(found = de.anode == target))
                            de = Macro.NEXTENTRY(blk, de);
                        else
                            break;
                    }

                    if (!found)
                    {
                        var nextBlockResult = await anodes.NextBlock(anode, anodeoffset, g);
                        anodeoffset = nextBlockResult.Item2;
                        eod = !nextBlockResult.Item1;
                    }
                }
                else
                    break;
            }

            if (!found)
                return false;

            result.file.dirblock = dirblock;
            result.file.direntry = de;
            Macro.Lock(dirblock, g);
            return true;
        }
        
        public static void FreeListEntry(IEntry entry, globaldata g)
        {
            //#define fe ((fileentry_t *)entry)
            var fe = entry as fileentry;
            if (Macro.IsFileEntry(entry) && fe.anodechain != null)
            {
                anodes.DetachAnodeChain(fe.anodechain, g);
            }
            //FreeMemP(entry, g);
            //#undef fe
        }
        
/* AccessConflict
**
** input : - [entry]: the object to be granted access
**    This object should contain valid references
**
** result: TRUE = accessconflict; FALSE = no accessconflict
**    All locks on same ANODE are checked. So a lock on a link can
**    be denied if the linked to file is locked exclusively.
**
** Because UpdateReference always updates all references to a dirblock,
** and the match object is valid, a flushed reference CANNOT point to
** the same dirblock. If it is a link, it CAN reference the same
** object
**
** Returns FALSE if there is an exclusive lock or if there is
** write access on a shared lock
**
*/
        public static bool AccessConflict (listentry entry)
        {
            uint anodenr;
            listentry fe;
            volumedata volume;

            //DB(Trace(1,"Accessconflict","entry %lx\n",entry));

            // -I- get anodenr
            anodenr =  entry.anodenr;
            volume  = entry.volume;

            // -II- zoek locks naar zelfde object
            //for (var node = Macro.HeadOf(volume.bmindexblks); node != null; node = node.Next)
                
            for(var node = Macro.HeadOf(volume.fileentries); node != null; node = node.Next)
            {
                fe = node.Value.ListEntry;
                if(fe.type.flags.type == Constants.ETF_VOLUME)
                {
                    if(entry.type.flags.type == Constants.ETF_VOLUME &&
                       (!Macro.SHAREDLOCK(fe) || !Macro.SHAREDLOCK(entry)))
                    {
                        //DB(Trace(1,"Accessconflict","on volume\n"));
                        return true;
                    }
                }	
                else if(fe.anodenr == anodenr)
                {
                    // on of the two wants or has an exclusive lock?
                    if(!Macro.SHAREDLOCK(fe) || !Macro.SHAREDLOCK(entry))
                    {
                        //DB(Trace(1,"Accessconflict","exclusive lock\n"));
                        return true;
                    }

                    // new & old shared lock, both write? 
                    else if(fe.type.flags.access == Constants.ET_SHAREDWRITE &&
                            entry.type.flags.access == Constants.ET_SHAREDWRITE)
                    {
                        //DB(Trace(1,"Accessconflict","two write locks\n"));
                        return true;
                    }
                }
            }
	
            return false;	// no conflicting locks
        }
    }
}