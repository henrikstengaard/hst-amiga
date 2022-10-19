﻿namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;
    using Core.Converters;

    public static class Directory
    {
        public static async Task<CachedBlock> MakeDirBlock(uint blocknr, uint anodenr, uint rootanodenr, uint parentnr,
            globaldata g)
        {
            // struct canode anode;
            // struct cdirblock *blk;
            var volume = g.currentvolume;

            //DB(Trace(10,"MakeDirBlock","blocknr = %lx\n", blocknr));

            /* fill in anode (allocated by MakeDirEntry) */
            var anode = new canode
            {
                clustersize = 1,
                blocknr = blocknr,
                next = 0
            };
            await anodes.SaveAnode(anode, anodenr, g);

            var blk = await Lru.AllocLRU(g);
            var dirblock = new dirblock(g);
            dirblock.anodenr = rootanodenr;
            dirblock.parent = parentnr;
            blk.blk = dirblock;
            blk.volume = volume;
            blk.blocknr = blocknr;
            blk.oldblocknr = 0;
            blk.changeflag = true;

            Macro.Hash(blk, volume.dirblks, Constants.HASHM_DIR);
            Cache.LOCK(blk, g);
            return blk;
        }

        /* Set number of deldir blocks (Has to be single threaded)
 * If 0 then deldir is disabled (but MODE_DELDIR stays;
 * InitModules() detect that the number of deldirblocks is 0)
 * There must be a currentvolume
 * Returns error (0 = success)
 */
        public static async Task SetDeldir(int nbr, globaldata g)
        {
            var rext = g.currentvolume.rblkextension;
            //struct cdeldirblock *ddblk, *next;
            CachedBlock ddblk;
            lockentry list;
            int i;
            //ULONG error = 0;

            /* check range */
            if (nbr < 0 || nbr > Constants.MAXDELDIR + 1)
            {
                // return ERROR_BAD_NUMBER;
                throw new Exception("ERROR_BAD_NUMBER");
            }

            /* check if there are locks on any deldir, delfile */
            for (var node = Macro.HeadOf(g.currentvolume.fileentries); node != null; node = node.Next)
            {
                list = node.Value as lockentry;

                if (list == null)
                {
                    continue;
                }

                if (Macro.IsDelDir(list.le.info) || Macro.IsDelFile(list.le.info))
                {
                    // return ERROR_OBJECT_IN_USE;
                    throw new Exception("ERROR_OBJECT_IN_USE");
                }
            }

            await Update.UpdateDisk(g);

            /* flush cache */
            for (var node = Macro.HeadOf(g.currentvolume.deldirblks); node != null; node = node.Next)
            {
                ddblk = node.Value;
                Lru.FlushBlock(ddblk, g);
                // MinRemove(LRU_CHAIN(ddblk));
                // MinAddHead(&g->glob_lrudata.LRUpool, LRU_CHAIN(ddblk));
                Macro.MinRemove(ddblk, g);
                Macro.MinRemove(new LruCachedBlock(ddblk), g);
                Macro.MinAddHead(g.glob_lrudata.LRUpool, new LruCachedBlock(ddblk));
                // i.p.v. FreeLRU((struct cachedblock *)ddblk, g);
            }

            /* free unwanted deldir blocks */
            var rext_blk = rext.rblkextension;
            for (i = nbr; i < rext_blk.deldirsize; i++)
            {
                Allocation.FreeReservedBlock(rext_blk.deldir[i], g);
                rext_blk.deldir[i] = 0;
            }

            /* allocate wanted ones */
            for (i = rext_blk.deldirsize; i < nbr; i++)
            {
                if (await NewDeldirBlock((ushort)i, g) == null)
                {
                    nbr = i + 1;
                    // error = ERROR_DISK_FULL;
                    // break;
                    throw new Exception("ERROR_DISK_FULL");
                }
            }

            /* if deldir size increases, start roving in a the new area 
             * if deldir size decreases, start roving from the start
             */
            if (nbr > rext_blk.deldirsize)
                rext_blk.deldirroving = (ushort)(rext_blk.deldirsize * Constants.DELENTRIES_PER_BLOCK);
            else
                rext_blk.deldirroving = 0;

            /* enable/disable */
            rext_blk.deldirsize = (ushort)nbr;
            g.deldirenabled = nbr > 0;

            await Update.MakeBlockDirty(rext, g);
            await Update.UpdateDisk(g);
        }

        public static async Task<CachedBlock> NewDeldirBlock(ushort seqnr, globaldata g)
        {
            // cdeldirblock
            var volume = g.currentvolume;
            // struct crootblockextension *rext;
            CachedBlock ddblk;
            uint blocknr;

            var rext = volume.rblkextension;

            if (seqnr > Constants.MAXDELDIR)
            {
                // DB(Trace(5, "NewDelDirBlock", "seqnr out of range = %lx\n", seqnr));
                return null;
            }

            /* alloc block and LRU slot */
            if ((ddblk = await Lru.AllocLRU(g)) == null || (blocknr = Allocation.AllocReservedBlock(g)) == 0)
            {
                if (ddblk != null)
                    Lru.FreeLRU(ddblk, g);
                return null;
            }

            /* make reference */
            var rext_blk = rext.rblkextension;
            rext_blk.deldir[seqnr] = blocknr;

            /* fill block */
            ddblk.volume = volume;
            ddblk.blocknr = blocknr;
            ddblk.used = 0;
            var ddblk_blk = new deldirblock(g)
            {
                id = Constants.DELDIRID,
                seqnr = seqnr
            };
            ddblk.blk = ddblk_blk;
            ddblk.changeflag = true;
            ddblk_blk.protection = Constants.DELENTRY_PROT; /* re..re..re.. */
            ddblk_blk.CreationDate = volume.rootblk.CreationDate;
            // ddblk->blk.creationminute	= volume->rootblk->creationminute;
            // ddblk->blk.creationtick		= volume->rootblk->creationtick;

            /* add to cache and return */
            Macro.MinAddHead(volume.deldirblks, ddblk);
            return ddblk;
        }

/*
 * Frees anodes without freeing blocks
 */
        public static async Task FreeAnodesInChain(uint anodenr, globaldata g)
        {
            canode anode = new canode();
            var rext = g.currentvolume.rblkextension;

            // DB(Trace(1, "FreeAnodeInChain", "anodenr: %ld \n", anodenr));
            await anodes.GetAnode(anode, anodenr, g);
            while (anode.nr != 0) /* stops autom.: anode.nr of anode 0 == 0 */
            {
                if (Macro.IsUpdateNeeded(Constants.RTBF_THRESHOLD, g))
                {
                    if (rext != null)
                    {
                        var rext_blk = rext.rblkextension;
                        rext_blk.tobedone.operation_id = Constants.PP_FREEANODECHAIN;
                        rext_blk.tobedone.argument1 = anode.nr;
                        rext_blk.tobedone.argument2 = 0;
                        rext_blk.tobedone.argument3 = 0;
                    }

                    await Update.UpdateDisk(g);
                }

                await anodes.FreeAnode(anode.nr, g);
                await anodes.GetAnode(anode, anode.next, g);
            }

            if (rext != null)
            {
                var rext_blk = rext.rblkextension;
                rext_blk.tobedone.operation_id = 0;
                rext_blk.tobedone.argument1 = 0;
                rext_blk.tobedone.argument2 = 0;
                rext_blk.tobedone.argument3 = 0;
            }
        }

/* <NewFile>
 *
 * NewFile creates a new file in a [directory] on currentvolume
 *
 * input : - [directory]: directory of file;
 *         - [filename]: name (without path) of file
 *         - found: flag, file already present?. If so newfile == old
 *
 * output: - [newfile]: fileinfo of new file (struct is managed by caller)
 *		   - [directory]: fileinfo of parent (can have changed if hardlink) 
 *
 * result: errornr; 0 = success
 *
 * maxneeds: 1 nd + 2 na = 3 res
 *
 * Note: 'directory' and 'newfile' may point to the same.
 */
        public static async Task NewFile(bool found, objectinfo directory, string filename, objectinfo newfile,
            globaldata g)
        {
            objectinfo info = new objectinfo();
            uint anodenr;
            int entryindex = 0;
            byte[] entrybuffer = new byte[Macro.MAX_ENTRYSIZE];
            extrafields extrafields = new extrafields();
            direntry destentry;
            canode anode = new canode();
            int l;
// #if VERSION23
            anodechain achain;
// #endif

            //DB(Trace(10, "NewFile", "%s\n", filename));
            /* check disk-writeprotection etc */
            /* check disk-writeprotection etc */
            Volume.CheckVolume(g.currentvolume, true, g);

// #if DELDIR
            if (Macro.IsDelDir(directory))
            {
                throw new IOException("ERROR_WRITE_PROTECTED");
            }
// #endif

            /* check reserved area lock */
            if (Macro.ReservedAreaIsLocked(g))
            {
                throw new IOException("ERROR_DISK_FULL");
            }

            /* truncate filename to 31 characters */
            if ((l = filename.Length) == 0)
            {
                throw new IOException("ERROR_INVALID_COMPONENT_NAME");
            }

            var fileNameSize = Macro.FILENAMESIZE(g);
            if (l > fileNameSize - 1)
            {
                filename = filename.Substring(fileNameSize - 1);
            }

            if (found)
            {
                /*
                 * new version: take over direntry
                 * (used to simply delete old and make new)
                 */
                info.file = newfile.file;
                info.volume = newfile.volume;
                anodenr = Macro.FIANODENR(info.file);

                /* Check deleteprotection */
                if (Macro.IsVolume(info) ||
                    (info.file.direntry.protection & Constants.FIBF_DELETE) == Constants.FIBF_DELETE)
                {
                    throw new IOException("ERROR_DELETE_PROTECTED");
                }

                /* If link, get real object. After this it has become a
                 * ST_FILE
                 */
                if ((int)info.file.direntry.type == Constants.ST_LINKFILE ||
                    ((int)info.file.direntry.type == Constants.ST_LINKDIR))
                {
                    canode linknode = new canode();

                    GetExtraFields(info.file.direntry, extrafields);
                    anodenr = extrafields.link;
                    await anodes.GetAnode(linknode, info.file.direntry.anode, g);
                    if (!await Lock.FetchObject(linknode.clustersize, anodenr, info, g))
                    {
                        throw new IOException("ERROR_OBJECT_NOT_FOUND");
                    }

                    /* have to check protection again */
                    if ((info.file.direntry.protection & Constants.FIBF_DELETE) == Constants.FIBF_DELETE)
                    {
                        throw new IOException("ERROR_DELETE_PROTECTED");
                    }

                    /* get parent */
                    if (!await GetParent(info, directory, g))
                    {
                        throw new IOException("ERROR_OBJECT_NOT_FOUND");
                    }
                }

                /* Check if there are outstanding locks on object */
                var node = Macro.HeadOf(g.currentvolume.fileentries);
                if (node != null && node.Value is listentry le && Lock.ScanLockList(le, anodenr))
                {
                    //DB(Trace(1, "NewFile", "object in use"));
                    throw new IOException("ERROR_OBJECT_IN_USE");
                }

                if ((achain = await anodes.GetAnodeChain(anodenr, g)) == null)
                    throw new IOException("ERROR_NO_FREE_STORE");

                /* Free used space */
                if (g.deldirenabled && (int)info.file.direntry.type == Constants.ST_FILE)
                {
                    int ddslot;

                    /* free a slot to put old version in, inter. update possible */
                    ddslot = await AllocDeldirSlot(g);

                    /* make replacement anode, because we want to reuse the old one */
                    info.file.direntry.anode = achain.head.an.nr = await anodes.AllocAnode(0, g);
                    await anodes.SaveAnode(achain.head.an, achain.head.an.nr, g);
                    await AddToDeldir(info, ddslot, g);
                    info.file.direntry.anode = anodenr;
                }

                /* Rollover files are essentially just 'reset' by overwriting:
                 * only the virtualsize and offset are set to zero (extrafields)
                 * Other files are deleted and recreated as a new file.
                 */
                if (info.file.direntry.type != Constants.ST_ROLLOVERFILE)
                {
                    /* Change directory entry */
                    SetDEFileSize(info.file.dirblock.dirblock, info.file.direntry, 0, g);
                    info.file.direntry.type = Constants.ST_FILE;
                    await Update.MakeBlockDirty(info.file.dirblock, g);

                    /* Reclaim anode */
                    anode.clustersize = 0;
                    anode.blocknr = 0xffffffff;
                    anode.next = 0;
                    await anodes.SaveAnode(anode, anodenr, g);

                    /* Delete old file (update possible) */
                    if (g.deldirenabled && (int)info.file.direntry.type == Constants.ST_FILE)
                        await Allocation.FreeBlocksAC(achain, Constants.ULONG_MAX, freeblocktype.keepanodes, g);
                    else
                        await Allocation.FreeBlocksAC(achain, Constants.ULONG_MAX, freeblocktype.freeanodes, g);
                    anodes.DetachAnodeChain(achain, g);
                }

                /* Clear direntry extrafields */
                //destentry = (struct direntry *)entrybuffer;
                destentry = DirEntryReader.Read(entrybuffer, entryindex);
                //memcpy(destentry, info.file.direntry, info.file.direntry.next);
                destentry.ExtraFields = new extrafields();
                GetExtraFields(info.file.direntry, extrafields);
                extrafields.virtualsize = extrafields.rollpointer = 0;
                AddExtraFields(destentry, extrafields);
                await ChangeDirEntry(info, destentry, directory, info.file, g);
                newfile.file = info.file;
                return;
            }

            /* direntry alloceren en invullen */
            if (await MakeDirEntry(Constants.ST_FILE, filename, entrybuffer, 0, g))
            {
                var entry = DirEntryReader.Read(entrybuffer, entryindex);
                if (await AddDirectoryEntry(directory, entry, newfile.file, g))
                {
                    return;
                }
                else
                {
                    await anodes.FreeAnode(entry.anode, g);
                }
            }

            throw new IOException("ERROR_DISK_FULL");
        }

/* NewDir
 *
 * Specification:
 * 
 * - make new dir 
 * - returns fileentry (!) with exclusive lock
 *
 * Implementation:
 *
 * - check if file/dir exists
 * - make direntry
 * - make first dirblock
 *
 * Similar to NewFile()
 *
 * maxneeds: 2 nd, 3 na = 2 nablk : 4 res
 */
        public static async Task<IEntry> NewDir(objectinfo parent, string dirname, globaldata g)
        {
            objectinfo info = new objectinfo
            {
                file = new fileinfo()
            };
            IEntry fileentry;
            ListType type = new ListType();
            CachedBlock blk;
            uint parentnr, blocknr;
            byte[] entrybuffer = new byte[Macro.MAX_ENTRYSIZE];
            int l;

            /* check disk-writeprotection etc */
            Volume.CheckVolume(g.currentvolume, true, g);

// #if DELDIR
            if (Macro.IsDelDir(parent))
            {
                //*error = ERROR_WRITE_PROTECTED;
                return null;
            }
// #endif

            /* check reserved area lock */
            if (Macro.ReservedAreaIsLocked(g))
            {
                throw new IOException("ERROR_DISK_FULL");
            }

            /* checkvolume */
            if (Macro.IsVolume(parent))
                parentnr = (uint)Macro.ANODE_ROOTDIR;
            else
                parentnr = parent.file.direntry.anode;

            /* truncate dirname to 31 characters */
            if ((l = dirname.Length) == 0)
            {
                throw new IOException("ERROR_INVALID_COMPONENT_NAME");
            }

            if (l > g.fnsize - 1)
            {
                dirname = dirname.Substring(g.fnsize - 1);
            }

            /* check if object exists */
            if (await SearchInDir(parentnr, dirname, info, g))
            {
                throw new IOException("ERROR_OBJECT_EXISTS");
            }

            /* allocate directory entry, fill it. Make fileentry */
            var entryindex = 0;
            if (!await MakeDirEntry(Constants.ST_USERDIR, dirname, entrybuffer, entryindex, g))
            {
                // goto error1;
                throw new IOException("ERROR_DISK_FULL");
            }

            var de = DirEntryReader.Read(entrybuffer, entryindex);
            if (!await AddDirectoryEntry(parent, de, info.file, g))
            {
                //FreeAnode(((struct direntry *)entrybuffer)->anode, g);
                await anodes.FreeAnode(de.anode, g);
                // error1:
                throw new IOException("ERROR_DISK_FULL");
            }

            type.value = Constants.ET_LOCK | Constants.ET_EXCLREAD;
            fileentry = await Lock.MakeListEntry(info, type, g);
            if (fileentry == null)
            {
                // goto error2;
                return await DiskFullError(info, null, g);
            }

            if (!Lock.AddListEntry(fileentry.ListEntry, g)) /* Should never fail, accessconflict impossible */
            {
                //ErrorMsg(AFS_ERROR_NEWDIR_ADDLISTENTRY, NULL, g);
                // goto error2;
                return await DiskFullError(info, fileentry, g);
            }

            /* Make first directoryblock (needed for parentfinding) */
            if ((blocknr = Allocation.AllocReservedBlock(g)) == 0)
            {
                //*error = ERROR_DISK_FULL;
                // error2:
                return await DiskFullError(info, fileentry, g);
                // await anodes.FreeAnode(info.file.direntry.anode, g);
                // await RemoveDirEntry(info, g);
                // if (fileentry != null)
                //     Lock.FreeListEntry(fileentry, g);
                // //DB(Trace(1, "Newdir", "disk full"));
                // throw new IOException("disk full");
            }

            blk = await MakeDirBlock(blocknr, info.file.direntry.anode, info.file.direntry.anode, parentnr, g);


            //return fileentry as lockentry;
            //throw new NotImplementedException("convert fileentry to lockentry?");
            return fileentry;
        }

        private static async Task<IEntry> DiskFullError(objectinfo info, IEntry fileentry, globaldata g)
        {
            await anodes.FreeAnode(info.file.direntry.anode, g);
            await RemoveDirEntry(info, g);
            if (fileentry != null)
                Lock.FreeListEntry(fileentry, g);
            //DB(Trace(1, "Newdir", "disk full"));
            throw new IOException("disk full");
        }

/*
 * Get deldirentry deldirentrynr (NO CHECK ON VALIDITY
 * deldir is assumed present and enabled
 */
        public static async Task<deldirentry> GetDeldirEntryQuick(uint ddnr, globaldata g)
        {
            CachedBlock ddblk;

            /* get deldirentry */
            if ((ddblk = await GetDeldirBlock((ushort)(ddnr / Constants.DELENTRIES_PER_BLOCK), g)) == null)
                return null;

            var blk = ddblk.deldirblock;
            return blk.entries[ddnr % Constants.DELENTRIES_PER_BLOCK];
        }

        public static async Task<CachedBlock> GetDeldirBlock(ushort seqnr, globaldata g)
        {
            var volume = g.currentvolume;
            CachedBlock rext;
            CachedBlock ddblk;
            uint blocknr;

            rext = volume.rblkextension;

            if (seqnr > Constants.MAXDELDIR)
            {
                //DB(Trace(5,"GetDeldirBlock","seqnr out of range = %lx\n", seqnr));
                throw new IOException("AFS_ERROR_DELDIR_INVALID");
            }

            /* get blocknr */
            var rext_blk = rext.rblkextension;
            if ((blocknr = rext_blk.deldir[seqnr]) == 0)
            {
                //DB(Trace(5,"GetDeldirBlock","ERR: index zero\n"));
                throw new IOException("AFS_ERROR_DELDIR_INVALID");
            }

            /* check cache */
            for (var node = Macro.HeadOf(volume.deldirblks); node != null; node = node.Next)
            {
                ddblk = node.Value;
                var ddblk_blk = ddblk.deldirblock;
                if (ddblk_blk.seqnr == seqnr)
                {
                    Lru.MakeLRU(ddblk, g);
                    return ddblk;
                }
            }

            /* alloc cache */
            if ((ddblk = await Lru.AllocLRU(g)) == null)
            {
                //DB(Trace(5,"GetDeldirBlock","ERR: alloclru failed\n"));
                return null;
            }

            /* read block */
            if ((ddblk.blk = await Disk.RawRead<deldirblock>(g.currentvolume.rescluster, blocknr, g)) == null)
            {
                Lru.FreeLRU(ddblk, g);
                return null;
            }

            /* check it */
            if (ddblk.deldirblock.id != Constants.DELDIRID)
            {
                // ErrorMsg (AFS_ERROR_DELDIR_INVALID, NULL, g);
                Lru.FreeLRU(ddblk, g);
                //volume.rootblk.Options ^= Constants.MODE_DELDIR;
                volume.rootblk.Options ^= RootBlock.DiskOptionsEnum.MODE_DELDIR;
                g.deldirenabled = false;
            }

            /* initialize it */
            ddblk.volume = volume;
            ddblk.blocknr = blocknr;
            ddblk.used = 1;
            ddblk.changeflag = false;

            /* add to cache and return */
            Macro.MinAddHead(volume.deldirblks, ddblk);
            return ddblk;
        }

/* RemoveDirEntry
 *
 * Simply shift the directryentry out with memmove(dest, src, len)
 * References are not corrected (see changedirentry)
 * 
 * makes all fileinfo's in same block invalid !!
 */
        public static async Task RemoveDirEntry(objectinfo info, globaldata g)
        {
            int endofblok, startofblok, destofblok, startofclear;
            ushort clearlen;
            //SIPTR error;
            objectinfo parent = new objectinfo();

            Macro.Lock(info.file.dirblock, g);

            /* change date parent %6.5 */
            if (await GetParent(info, parent, g))
            {
                await Touch(parent, g);
            }

            /* remove direntry */
            // destofblok = (UBYTE *)info.direntry;
            // startofblok = destofblok + info.direntry->next;
            // endofblok = (UBYTE *)&(info.dirblock->blk) + g->rootblock->reserved_blksize;
            // startofclear = endofblok - info.direntry->next;
            // clearlen = info.direntry->next;
            // memmove(destofblok, startofblok, endofblok - startofblok);

            destofblok = info.file.direntry.Offset;
            startofblok = destofblok + info.file.direntry.next;
            endofblok = SizeOf.DirBlock.Entries(g);
            startofclear = endofblok - info.file.direntry.next;
            clearlen = info.file.direntry.next;

            // moves bytes from startofblok to destofblok
            var blk = info.file.dirblock.dirblock;
            for (var i = 0; i < endofblok - startofblok; i++)
            {
                blk.entries[destofblok + i] = blk.entries[startofblok + i];
            }

            /* makes info invalid!! */
            if (info.file.direntry.next != 0)
            {
                //memset(startofclear, 0, clearlen);
                for (var i = 0; i < clearlen; i++)
                {
                    blk.entries[startofclear + i] = 0;
                }
            }

            await Update.MakeBlockDirty(info.file.dirblock, g); // %6.2
        }

// /* <FindObject> 
//  *
//  * FindObject searches the object 'fname' in directory 'directory'.
//  * FindObject zoekt het object 'fname' in directory 'directory'. 
//  * Interpret empty filename as parent and ":" as root.
//  * Does not use multiple-assign-list
//  *
//  * input : - [directory]: the 'root' directory of the search
//  *         - [objectname]: file to be found, including path
//  * 
//  * output: - [object]: If file found : fileinfo of object
//  *                     If path found : fileinfo of directory
//  *         - [error]: Errornumber as result = DOSFALSE; otherwise 0
//  *
//  * result: DOSTRUE  (-1) = file found (->in fileinfo)
//  *          DOSFALSE (0)  = error
//  *
//  * If only a partial path is found, a pointer to the unparsed part
//  * will be stored in g->unparsed.
//  */
//         public static async Task FindObject(objectinfo directory, string objectname,
//             objectinfo object_, globaldata g)
//         {
//             string filename;
//             bool ok;
//
//             *error = 0;
//             filename = GetFullPath(directory, objectname, object_, error, g);
//
//             if (!filename)
//             {
//                 //DB(Trace(2, "FindObject !filename %s\n", objectname));
//                 return false;
//             }
//
//             /* path only (dir or volume) */
//             if (!*filename)
//             {
//                 return true;
//             }
//
//             /* there is a filepart (file or dir)  */
//             ok = await GetObject(filename, object_, g);
//             if (!ok && (*error == ERROR_OBJECT_NOT_FOUND))
//                 g->unparsed = filename;
//
//             return ok;
//         }

/* GetParent
 *
 * childanodenr = anodenr of start directory (the child)
 * parentanodenr = anodenr of directory containing childanodenr (the parent)
 * childfi == parentfi can be dangerous
 * in:childfi; out:parentfi, error
 */
        public static async Task<bool> GetParent(objectinfo childfi, objectinfo parentfi, globaldata g)
        {
            canode anode = new canode();
            CachedBlock dirblock = null;
            direntry de = null;
            uint anodeoffset = 0;
            uint childanodenr, parentanodenr;
            bool eod = false, eob = false, found = false;

            // -I- Find anode of parent
            if (childfi == null || Macro.IsVolume(childfi)) // child is rootdir
            {
                //*error = 0x0;           /* No error; just return NULL */
                return false;
            }

#if DELDIR
	if (g->deldirenabled)
	{
		if (IsDelDir(*childfi))
			return GetRoot(parentfi, g);

		if (IsDelFile(*childfi))
		{
			parentfi->deldir.special = SPECIAL_DELDIR;
			parentfi->deldir.volume = g->currentvolume;
			return TRUE;
		}
	}
#endif

            var blk = childfi.file.dirblock.dirblock;
            childanodenr = blk.anodenr; /* the directory 'child' is in */
            parentanodenr = blk.parent; /* the directory 'childanodenr' is in */

            // -II- check if in root
            if (parentanodenr == 0) /* child is in rootdir */
            {
                parentfi.volume.root = 0;
                parentfi.volume.volume = childfi.file.dirblock.volume;
                return true;
            }

            // -III- get parentdirectory and find direntry
            await anodes.GetAnode(anode, parentanodenr, g);
            while (!found && !eod)
            {
                dirblock = await LoadDirBlock(anode.blocknr + anodeoffset, g);
                if (dirblock != null)
                {
                    blk = dirblock.dirblock;
                    de = Macro.FIRSTENTRY(blk);
                    eob = false;

                    while (!found && !eob)
                    {
                        found = de.anode == childanodenr;
                        eob = de.next == 0;
                        if (!found && !eob)
                        {
                            de = Macro.NEXTENTRY(blk, de);
                        }
                    }

                    if (!found)
                    {
                        var result = await anodes.NextBlock(anode, anodeoffset, g);
                        anodeoffset = result.Item2;
                        eod = !result.Item1;
                    }
                }
                else
                {
                    break;
                }
            }

            if (!found)
            {
                //DB(Trace(1, "GetParent", "DiskNotValidated %ld\n", childanodenr));
                //*error = ERROR_DISK_NOT_VALIDATED;
                return false;
            }

            parentfi.file.direntry = de;
            parentfi.file.dirblock = dirblock;
            Macro.Lock(dirblock, g);
            return true;
        }

/* SearchInDir
 *
 * Search an object in a directory and return the fileinfo
 *
 * input : - dirnodenr: anodenr of directory to search in
 *         - objectname: found object (without path)
 * 
 * output: - info: objectinfo of found object
 *
 * result: success 
 */
        public static async Task<bool> SearchInDir(uint dirnodenr, string objectname, objectinfo info, globaldata g)
        {
            canode anode = new canode();
            CachedBlock dirblock;
            //direntry entry = null;
            var found = false;
            var eod = false;
            uint anodeoffset;
            //byte[] intl_name = new byte[Macro.PATHSIZE];

            //ENTER("SearchInDir");
            //ctodstr(objectname, intl_name);
            //var t = AmigaTextHelper.GetBytes(objectname);
            var intl_name = AmigaTextHelper.ToUpper(objectname, true);

            /* truncate */
            if (intl_name.Length > g.fnsize)
            {
                intl_name = intl_name.Substring(g.fnsize);
            }

            //intltoupper(intl_name);     /* international uppercase objectname */
            await anodes.GetAnode(anode, dirnodenr, g);
            anodeoffset = 0;
            dirblock = await LoadDirBlock(anode.blocknr, g);
            var blk = dirblock.dirblock;
            var entry = DirEntryReader.Read(blk.entries, 0);
            while (blk != null && !found && !eod) /* eod stands for end-of-dir */
            {
                // entry = (struct direntry *)(&dirblock->blk.entries);

                /* scan block */
                var entryIndex = 0;
                while (entry.next != 0 && entryIndex + entry.next < blk.entries.Length)
                {
                    found = intl_name == AmigaTextHelper.ToUpper(entry.Name, true);
                    if (found)
                    {
                        break;
                    }

                    entry = DirEntryReader.Read(blk.entries, entryIndex);
                    entryIndex += entry.next;
                }

                /* load next block */
                if (!found)
                {
                    var result = await anodes.NextBlock(anode, anodeoffset, g);
                    anodeoffset = result.Item2;
                    if (result.Item1)
                    {
                        dirblock = await LoadDirBlock(anode.blocknr + anodeoffset, g);
                        blk = dirblock.dirblock;
                    }
                    else
                    {
                        eod = true;
                    }
                }
            }

            /* make fileinfo */
            if (dirblock == null)
            {
                return false;
            }
            else if (found)
            {
                info.file.direntry = entry;
                info.file.dirblock = dirblock;
                info.volume.root = 1;
                info.volume.volume = dirblock.volume;
                Macro.Lock(dirblock, g);
                return true;
            }
            else
                return false;
        }

/* AddDirectoryEntry
 *
 * Add a directoryentry to a directory.
 * Tries to add the directoryentry at the end of an existing directoryblock. If
 * that fails, create a new one.
 *
 * Operates on currentvolume
 *
 * input : - dir: directory to add directoryentry too
 *          - newentry: the new directoryentry
 *
 * output: - newinfo: pointer to direntry and directoryblock the entry
 *          was added to
 *
 * NB: A) there should ALWAYS be at least one dirblock
 *     B) assumes CURRENTVOLUME 
 */
        public static async Task<bool> AddDirectoryEntry(objectinfo dir, direntry newentry, fileinfo newinfo,
            globaldata g)
        {
            canode anode = new canode();
            uint anodeoffset = 0, diranodenr;
            CachedBlock blok = null;
            direntry entry = null;
            var done = false;
            var eof = false;
            int i;

            if (dir == null || Macro.IsVolume(dir))
                diranodenr = (uint)Macro.ANODE_ROOTDIR;
            else
                diranodenr = dir.file.direntry.anode;

            /* check if space in existing dirblocks */
            await anodes.GetAnode(anode, diranodenr, g);
            for (; !done && !eof;)
            {
                if ((blok = await LoadDirBlock(anode.blocknr + anodeoffset, g)) == null)
                    break;

                var blk = blok.dirblock;
                entry = DirEntryReader.Read(blk.entries, 0);

                /* goto end of dirblock; i = aantal gebruikte bytes */
                for (i = 0; entry.next > 0; entry = DirEntryReader.Read(blk.entries, i))
                    i += entry.next;

                /* does it fit in this block? (keep space for trailing 0) */
                if (i + newentry.next + 1 < Macro.DB_ENTRYSPACE(g))
                {
                    //memcpy(entry, newentry, newentry->next);
                    DirEntryWriter.Write(blk.entries, i, newentry);
                    entry = newentry;
                    //entry.next = 0;
                    //*(UBYTE *)NEXTENTRY(entry) = 0;     // dirblock afsluiten
                    blk.entries[i + newentry.next] = 0;

                    done = true;
                    break;
                }

                var result = await anodes.NextBlock(anode, anodeoffset, g);
                anodeoffset = result.Item2;
                eof = !result.Item1;
            }

            /* no->new dirblock (eof <=> anode is end of chain)
             * We will make the new dirblock at the >start< of
             * the chain.
             * We always allocate new anode
             */
            var newanode = new canode
            {
                clustersize = 1
            };
            if (!done && eof)
            {
                var parent = blok.dirblock.parent;
                if ((newanode.blocknr = Allocation.AllocReservedBlock(g)) == 0)
                {
                    return false;
                }

                await anodes.GetAnode(anode, diranodenr, g);
                newanode.nr = diranodenr;
                newanode.next = anode.nr = await anodes.AllocAnode(anode.next > 0 ? anode.next : anode.nr, g);
                await anodes.SaveAnode(anode, anode.nr, g);
                blok = await MakeDirBlock(newanode.blocknr, newanode.nr, diranodenr, parent, g);
                await anodes.SaveAnode(newanode, newanode.nr, g);
                var blk = blok.dirblock;
                //entry = DirEntryReader.Read(blk.entries, 0);
                //memcpy(entry, newentry, newentry->next);
                //*(UBYTE *)NEXTENTRY(entry) = 0;     // mark end of dirblock
                DirEntryWriter.Write(blk.entries, 0, newentry);
                entry = newentry;
                // entry.next = 0;
            }

            /* fill newinfo */
            newinfo.direntry = entry;
            newinfo.dirblock = blok;

            /* update notify */
            //PFSUpdateNotify(blok->blk.anodenr, &entry->nlength, entry->anode, g);
            if (blok != null)
            {
                Macro.Lock(blok, g);
                await Update.MakeBlockDirty(blok, g);
            }

            await Touch(dir, g);
            return true;
        }

        public static async Task Touch(objectinfo info, globaldata g) // ook archiveflag..
        {
            var time = DateTime.Now;

            if (Macro.IsVolume(info) && g.currentvolume.rblkextension != null)
            {
                var blk = g.currentvolume.rblkextension.rblkextension;
                blk.RootDate = time;
                await Update.MakeBlockDirty(g.currentvolume.rblkextension, g);
            }
            else if (!Macro.IsVolume(info))
            {
                info.file.direntry.CreationDate = time;
                // info.direntry.creationday = (UWORD)time.ds_Days;
                // info.direntry.creationminute = (UWORD)time.ds_Minute;
                // info.direntry->creationtick = (UWORD)time.ds_Tick;
                info.file.direntry.protection =
                    (byte)(info.file.direntry.protection & ~Constants.FIBF_ARCHIVE); // clear archivebit (eor)

                await Update.MakeBlockDirty(info.file.dirblock, g);
            }
        }

/*
 * Update references
 * diff is direntry size difference (new - original)
 */
        public static async Task UpdateChangedRef(fileinfo from, fileinfo to, int diff, globaldata g)
        {
            var volume = from.dirblock.volume;

            for (var node = Macro.HeadOf(volume.fileentries); node != null; node = node.Next)
            {
                var fe = node.Value.ListEntry;
                /* only dirs and files can be in a directory, but the volume *
                 * of volumeinfos can never point to a cached block, so a 
                 * type != ETF_VOLUME check is not necessary. Just check the
                 * dirblock pointer
                 */
                if (fe.info.file.dirblock == from.dirblock)
                {
                    /* is het de targetentry ? */
                    if (fe.info.file.direntry.Offset == from.direntry.Offset)
                    {
                        if (to != null)
                        {
                            fe.info.file = to;
                        }
                    }
                    else
                    {
                        /* take only entries after target */
                        if (fe.info.file.direntry.Offset > from.direntry.Offset)
                        {
                            // fe.info.file.direntry = (struct direntry *)((UBYTE *)fe->info.file.direntry + diff);v
                            var d = fe.info.file.dirblock.dirblock;
                            fe.info.file.direntry = DirEntryReader.Read(d.entries, fe.info.file.direntry.Offset + diff);
                        }
                    }
                }

                /* check for exnext references */
                if (fe.type.flags.dir != 0)
                {
                    var dle = fe.LockEntry;

                    if (dle.nextentry.dirblock == from.dirblock)
                    {
                        if (dle.nextentry.direntry.Offset == from.direntry.Offset && dle.nextentry.direntry.next == 0)
                        {
                            await GetNextEntry(dle, g);
                        }
                        else
                        {
                            /* take only entries after target */
                            if (dle.nextentry.direntry.Offset > from.direntry.Offset)
                            {
                                //dle.nextentry.direntry = (struct direntry *)((UBYTE *)dle->nextentry.direntry + diff);
                                var d = fe.info.file.dirblock.dirblock;
                                dle.nextentry.direntry =
                                    DirEntryReader.Read(d.entries, dle.nextentry.direntry.Offset + diff);
                            }
                        }
                    }
                }
            }
        }

        public static async Task GetNextEntry(lockentry file, globaldata g)
        {
            canode anode = new canode();

            /* get nextentry */
            var d = file.nextentry.dirblock.dirblock;
            file.nextentry.direntry = Macro.NEXTENTRY(d, file.nextentry.direntry);

            /* no next entry? -> next block */
            if (file.nextentry.direntry.next == 0)
            {
                /* NB: 'nextanode' is een verwarrende naam */
                await anodes.GetAnode(anode, file.nextanode, g);
                file.nextanode = await GetFirstNonEmptyDE(anode.next, file.nextentry, g);
            }
        }

/* Get first non empty direntry starting from anode [anodenr] 
 * Returns {NULL, NULL} if end of dir
 */
        public static async Task<uint> GetFirstNonEmptyDE(uint anodenr, fileinfo info, globaldata g)
        {
            canode anode = new canode();
            uint nextsave;
            var found = false;

            anode.next = anodenr;
            while (!found)
            {
                nextsave = anode.next;
                if (nextsave != 0)
                {
                    await anodes.GetAnode(anode, anode.next, g);
                    info.dirblock = await LoadDirBlock(anode.blocknr, g);
                    if (info.dirblock != null)
                    {
                        var d = info.dirblock.dirblock;
                        info.direntry = Macro.FIRSTENTRY(d);
                    }
                }

                if (nextsave == 0 || info.dirblock == null)
                {
                    info.direntry = null;
                    info.dirblock = null;
                    found = true;
                }
                else if (info.direntry.next != 0)
                {
                    Macro.Lock(info.dirblock, g);
                    found = true;
                }
            }

            return anode.nr;
        }

        /* MakeDirEntry
 *
 * Used by L2.NewFile, L2.NewDir
 *
 * Make a new directoryentry. The filename is not checked. Allocates anode
 * for file/dir
 *
 * input : 
 *        - type: ST_FILE, ST_DIR etc ..
 *        - name: objectname
 *        - entrybuffer: place to put direntry (char buffer of size MAX_ENTRYSIZE)
 *
 * output: - info: objectinfo of new directoryentry
 */
        public static async Task<bool> MakeDirEntry(int type, string name, byte[] entrybuffer, int entryIndex,
            globaldata g)
        {
            //ushort entrysize;
            //direntry *direntry;
            //DateTime time;
            //MUFS(struct extrafields extrafields);

            var entrysize = (SizeOf.DirEntry.Struct + name.Length) & 0xfffe;
            // entrysize = ((sizeof(struct direntry) + strlen(name)) & 0xfffe);
            if (g.dirextension)
                entrysize += 2;
            // var direntry = (struct direntry *)entrybuffer;
            // memset(direntry, 0, entrysize);
            var direntry = new direntry();

#if MULTIUSER
	if (g->muFS_ready)
	{
		extrafields.link = 0;
		extrafields.uid = g->user->uid;
		extrafields.gid = g->user->gid;
		extrafields.prot = muGetDefProtection(g->action->dp_Port->mp_SigTask);
		direntry->protection = extrafields.prot;
		extrafields.prot &= 0xffffff00;
	}
#endif

            if ((direntry.anode = await anodes.AllocAnode(0, g)) == 0)
            {
                return false;
            }

            direntry.next = (byte)entrysize;
            direntry.type = (sbyte)type;
            // direntry->fsize        = 0;
            direntry.CreationDate = DateTime.Now;
            // direntry.creationday = (UWORD)time.ds_Days;
            // direntry.creationminute = (UWORD)time.ds_Minute;
            // direntry.creationtick = (UWORD)time.ds_Tick;
            // direntry->protection  = 0x00;    // RWED
            direntry.nlength = (byte)name.Length;

            // the trailing 0 of strcpy() creates the empty comment!
            // the flags field following this is 0 by the memset call
            //strcpy((UBYTE *)&direntry->startofname, name);
            direntry.Name = name;

#if MULTIUSER
	if (g->dirextension && g->muFS_ready)
		AddExtraFields(direntry, &extrafields);
#endif

            DirEntryWriter.Write(entrybuffer, entryIndex, direntry);

            return true;
        }


        /* NULL => failure 
 * The loaded dirblock is locked immediately (prevents flushing)
 * 
 */
        public static async Task<CachedBlock> LoadDirBlock(uint blocknr, globaldata g)
        {
            //struct cdirblock *dirblk;
            CachedBlock dirblk;
            var volume = g.currentvolume;

            //DB(Trace(1, "LoadDirBlock", "loading block %lx\n", blocknr));
            // -I- check if already in cache
            if ((dirblk = Lru.CheckCache(volume.dirblks, Constants.HASHM_DIR, blocknr, g)) == null)
            {
                // -II- not in cache -> put it in
                dirblk = await Lru.AllocLRU(g);

                //DB(Trace(10, "LoadDirBlock", "loading block %lx from disk\n", blocknr));
                //var blk = 
                if ((dirblk.blk = await Disk.RawRead<dirblock>(g.currentvolume.rescluster, blocknr, g)) != null)
                {
                    if (dirblk.blk.id == Constants.DBLKID)
                    {
                        dirblk.volume = g.currentvolume;
                        dirblk.blocknr = blocknr;
                        dirblk.used = 0;
                        dirblk.changeflag = false;
                        Macro.Hash(dirblk, volume.dirblks, Constants.HASHM_DIR);
                        Lru.UpdateReference(blocknr, dirblk, g); // %10
                    }
                    else
                    {
                        // ULONG args[2];
                        // args[0] = dirblk->blk.id;
                        // args[1] = blocknr;
                        Lru.FreeLRU(dirblk, g);
                        // ErrorMsg(AFS_ERROR_DNV_WRONG_DIRID, args, g);
                        // return NULL;
                    }
                }
                else
                {
                    Lru.FreeLRU(dirblk, g);
                    //DB(Trace(5, "LoadDirBlock", "loading block %lx failed\n", blocknr));
                    // ErrorMsg(AFS_ERROR_DNV_LOAD_DIRBLOCK, NULL, g);    // #$%^&??
                    // DebugOn;DebugMsgNum("blocknr", blocknr);
                    return null;
                }
            }

            // EXIT("LoadDirBlock");
            return dirblk;
        }

/*
 * GetExtraFields (normal file only)
 */
        public static void GetExtraFields(direntry direntry, extrafields extrafields)
        {
            // UWORD *extra = (UWORD *)extrafields;
            // UWORD *fields = (UWORD *)(((UBYTE *)direntry) + direntry->next);
            // ushort flags, i;
            //
            // flags = *(--fields);
            // for (i = 0; i < sizeof(struct extrafields) / 2; i++, flags >>= 1)
            // *(extra++) = (flags & 1) ? *(--fields) : 0;

            /* patch protection lower 8 bits */
            extrafields.prot |= direntry.protection;
        }

        public static uint GetDDFileSize(deldirentry dde, globaldata g)
        {
            if (!Constants.LARGE_FILE_SIZE || !g.largefile || dde.filename[0] > Constants.DELENTRYFNSIZE)
                return dde.fsize;
// #if LARGE_FILE_SIZE
// 	else
// 		return dde->fsize | ((FSIZE)dde->fsizex << 32);
// #endif
            return 0;
        }

        public static uint GetDEFileSize(direntry direntry, globaldata g)
        {
            if (!Constants.LARGE_FILE_SIZE || !g.largefile)
                return direntry.fsize;
// #if LARGE_FILE_SIZE
// 	else {
// 		struct extrafields extrafields;
// 		GetExtraFields(direntry, &extrafields);
// 		return direntry->fsize | ((FSIZE)extrafields.fsizex << 32);
// 	}
// #endif
            return 0;
        }

        /* GetFullPath converts a relative path to an absolute path.
 * The fileinfo of the new path is returned in [result].
 * The return value is the filename without path.
 * Error: return 0
 *
 * Parsing Syntax:
 * : after '/' or at the beginning ==> root
 * : after [name] ==> volume [name]
 * / after / or ':' or at the beginning ==> parent
 * / after dir ==> get dir
 * / after file ==> error (ALWAYS) (AMIGADOS ok if LAST file)
 *
 * IN basispath, filename, g
 * OUT fullpath, error
 *
 * If only a partial path is found, a pointer to the unparsed part
 * will be stored in g->unparsed.
 */
// public static async Task<byte> GetFullPath(objectinfo basispath, string filename, objectinfo fullpath, globaldata g)
// {
//     byte pathpart;
//     char parttype;
// 	COUNT index;
// 	bool eop = false, success = true;
// 	volumedata volume;
//
// 	// VVV Init:getrootvolume
// 	//ENTER("GetFullPath");
// 	//g.unparsed = NULL;
//
// 	/* Set base path */
//     if (basispath != null)
//     {
//         fullpath = basispath;
//     }
//     else
//     {
//         fullpath = await GetRoot(g);
//     }
//
// 	/* The basispath should not be a file
// 	 * BTW: softlink is illegal too, but not possible
// 	 */
// 	if (Macro.IsFile(fullpath) || Macro.IsDelFile(fullpath))
// 	{
// 		throw new IOException("ERROR_OBJECT_WRONG_TYPE");
// 	}
//
// 	/* check if device present */
//     if (Macro.IsVolume(fullpath) || Macro.IsDelDir(fullpath))
//     {
//         volume = fullpath.volume.volume;
//     }
//     else
//     {
//         volume = fullpath.file.dirblock.volume;
//     }
//
//     if (!Volume.CheckVolume(volume, false, g))
//     {
//         return 0; // false
//     }
//
//
//     if (filename.IndexOf("/", StringComparison.OrdinalIgnoreCase))
//     {
//         
//     }
//     
//     var parts = filename.Split('/');
//     
// 	/* extend base-path using filename and
// 	 * continue until path complete (eop = end of path)
// 	 */
//     
// 	while (!eop)
// 	{
// 		pathpart = filename;
// 		index = filename.IndexOf(new char []{ '/', ':'}, StringComparison.OrdinalIgnoreCase);
//         index = filename.IndexOf("/", StringComparison.OrdinalIgnoreCase);
// 		parttype = filename[index];
// 		filename[index] = 0x0;
//
// 		switch (parttype)
// 		{
// 			case ':':
// 				success = false;
// 				break;
//
// 			case '/':
// 				if (pathpart == 0x0)
// 				{
// 					// if already at root, fail with an error
// 					if (Macro.IsVolume(fullpath))
// 					{
// 						throw new IOException("ERROR_OBJECT_NOT_FOUND");
// 					}
// 					success = await GetParentOf(fullpath, g);
// 				}
// 				else
// 					success = await GetDir(pathpart, fullpath, g);
// 				break;
//
// 			default:
// 				eop = true;
//                 break;
// 		}
//
// 		filename[index] = parttype;
//
// 		if (!success)
// 		{
// 			/* return pathrest for readlink() */
// 			if (*error == ERROR_IS_SOFT_LINK)
// 				g->unparsed = filename + index;
// 			else if (*error == ERROR_OBJECT_NOT_FOUND)
// 				g->unparsed = filename;
// 			return NULL;
// 		}
//
// 		if (!eop)
// 			filename += index + 1;
// 	}
//
// 	return filename;
// }

        /// <summary>
        /// Find object info for path
        /// </summary>
        /// <param name="current">Current directory</param>
        /// <param name="path">Relative or absolute path to object</param>
        /// <param name="g"></param>
        /// <exception cref="IOException"></exception>
        public static async Task<bool> Find(objectinfo current, string path, globaldata g)
        {
            var parts = (path.StartsWith("/") ? path.Substring(1) : path).Split('/');

            foreach (var part in parts)
            {
                if (!await GetObject(part, current, g))
                {
                    return false;
                }

                current.volume.root = 1;
            }

            return true;
        }

        public static async Task<objectinfo> GetRoot(globaldata g)
        {
            await Volume.UpdateCurrentDisk(g);
            return new objectinfo
            {
                deldir = new deldirinfo
                {
                },
                delfile = new delfileinfo
                {
                },
                volume = new volumeinfo
                {
                    root = 0,
                    volume = g.currentvolume
                },
                // file = new fileinfo
                // {
                // }
            };
        }

/* pre: - path <> 0 and volume or directory
 * result back in path
 */
        public static async Task<bool> GetParentOf(objectinfo path, globaldata g)
        {
            var info = path;
            return await GetParent(info, path, g);
        }

/* pre: - path <> 0 and volume of directory
 *      - dirname without path; strlen(dirname) > 0
 * result back in path
 */
        public static async Task<bool> GetDir(string dirname, objectinfo path, globaldata g)
        {
            bool found;

            found = await GetObject(dirname, path, g);

// #if DELDIR
            if (g.deldirenabled && Macro.IsDelDir(path))
                return true;
// #endif

            /* check if found directory */
// #if DELDIR
            if (!found || Macro.IsFile(path) || Macro.IsDelFile(path))
// #else
//             if (!found || IsFile(*path))
// #endif
            {
                throw new IOException("ERROR_OBJECT_NOT_FOUND"); // DOPUS doesn't like DIR_NOT_FOUND
            }

            /* check if softlink */
            if (Macro.IsSoftLink(path))
            {
                throw new IOException("ERROR_IS_SOFT_LINK");
            }

            /* resolve links */
            if (path.file.direntry.type == Constants.ST_LINKDIR)
            {
                extrafields extrafields = new extrafields();
                canode linknode = new canode();

                GetExtraFields(path.file.direntry, extrafields);
                await anodes.GetAnode(linknode, path.file.direntry.anode, g);
                if (!await Lock.FetchObject(linknode.clustersize, extrafields.link, path, g))
                    return false;
            }

            return true;
        }

/* pre: - path<>0 and volume of directory
 *      - objectname without path; strlen(objectname) > 0
 * result back in path
 */
        public static async Task<bool> GetObject(string objectname, objectinfo path, globaldata g)
        {
            uint anodenr;
            bool found;

            // #if DELDIR
            if (Macro.IsDelDir(path))
            {
                found = (await SearchInDeldir(objectname, path, g) != null);
                goto go_error;
            }
// #endif

            if (Macro.IsVolume(path))
                anodenr = Constants.ANODE_ROOTDIR;
            else
                anodenr = path.file.direntry.anode;

            //DB(Trace(1, "GetObject", "parent anodenr %lx\n", anodenr));
            found = await SearchInDir(anodenr, objectname, path, g);

            go_error:
            if (!found)
            {
// #if DELDIR
                if (g.deldirenabled && Macro.IsVolume(path))
                {
                    if (Constants.deldirname.Equals(objectname, StringComparison.OrdinalIgnoreCase))
                    {
                        path.deldir.special = Constants.SPECIAL_DELDIR;
                        path.deldir.volume = g.currentvolume;
                        return true;
                    }
                }

// #endif
                //throw new IOException("ERROR_OBJECT_NOT_FOUND");
                return false;
            }

            return true;
        }

/* SearchInDeldir
 *
 * Search an object in the del-directory and return the objectinfo if found
 *
 * input : - delname: name of object to be searched for
 * output: - result: the searched for object
 * result: deldirentry * or NULL
 */
        public static async Task<deldirentry> SearchInDeldir(string delname, objectinfo result, globaldata g)
        {
            deldirentry dde;
            CachedBlock dblk;
            int delnumptr;
            //UBYTE intl_name[PATHSIZE];
            uint slotnr, offset;

            //ENTER("SearchInDeldir");
            if ((delnumptr = delname.LastIndexOf(Constants.DELENTRY_SEP)) <= -1)
                return null; /* no delentry seperator */
            //stcd_i(delnumptr + 1, (int *) &slotnr);  /* retrieve the slotnr  */
            slotnr = (uint)(delnumptr + 1);

            delnumptr = 0; /* patch string to get filename part  */
            //ctodstr(delname, intl_name);
            var intl_name = delname;

            /* truncate to maximum length */
            if (intl_name.Length > g.fnsize)
            {
                intl_name = intl_name.Substring(g.fnsize);
            }

            // intltoupper(intl_name);     /* international uppercase objectname */
            intl_name = AmigaTextHelper.ToUpper(intl_name, true);
            delnumptr = Constants.DELENTRY_SEP;

            /* 4.3: get deldir block */
            if ((dblk = await GetDeldirBlock((ushort)(slotnr / Constants.DELENTRIES_PER_BLOCK), g)) == null)
            {
                return null;
            }

            offset = slotnr % Constants.DELENTRIES_PER_BLOCK;

            var blk = dblk.deldirblock;
            dde = blk.entries[offset];
            if (intl_name.Equals(dde.filename, StringComparison.OrdinalIgnoreCase))
            {
                if (!await IsDelfileValid(dde, dblk, g))
                    return null;

                result.delfile.special = Constants.SPECIAL_DELFILE;
                result.delfile.slotnr = slotnr;
                Macro.Lock(dblk, g);
                return dde;
            }

            return null;
        }

        /*
 * Test if delfile is valid by scanning it's blocks
 */
        public static async Task<bool> IsDelfileValid(deldirentry dde, CachedBlock ddblk, globaldata g)
        {
            canode anode = new canode();

            /* check if deldirentry actually used */
            if (dde.anodenr == 0)
            {
                return false;
            }

            /* scan all blocks in the anodelist for validness */
            for (anode.nr = dde.anodenr; anode.nr > 0; anode.nr = anode.next)
            {
                await anodes.GetAnode(anode, anode.nr, g);
                if (await BlockTaken(anode, g))
                {
                    /* free attached anodechain */
                    await FreeAnodesInChain(dde.anodenr, g); /* only FREE anodes, not blocks!! */
                    dde.anodenr = 0;
                    await Update.MakeBlockDirty(ddblk, g);
                    return false;
                }
            }

            return true;
        }

        /*
 * Check if the blocks referenced by an anode are taken
 */
        public static async Task<bool> BlockTaken(canode anode, globaldata g)
        {
            uint size, bmoffset, bmseqnr, field, i, j, blocknr;
            CachedBlock bitmap;
            var allocData = g.glob_allocdata;

            i = (anode.blocknr - allocData.bitmapstart) / 32; // longwordnr
            size = (anode.clustersize + 31) / 32;
            bmseqnr = i / allocData.longsperbmb;
            bmoffset = i % allocData.longsperbmb;

            while (size > 0)
            {
                /* get first bitmapblock */
                bitmap = await Allocation.GetBitmapBlock(bmseqnr, g);

                /* check all blocks */
                while (bmoffset < allocData.longsperbmb)
                {
                    var blk = bitmap.BitmapBlock;
                    /* check all bits in field */
                    field = blk.bitmap[bmoffset];
                    for (i = 0, j = (uint)1 << 31; i < 32; j >>= 1, i++)
                    {
                        if ((field & j) != 0)
                        {
                            /* block is taken, check it out */
                            blocknr = (bmseqnr * allocData.longsperbmb + bmoffset) * 32 + i +
                                      allocData.bitmapstart;
                            if (blocknr >= anode.blocknr && blocknr < anode.blocknr + anode.clustersize)
                                return true;
                        }
                    }

                    bmoffset++;
                    if ((--size) == 0)
                        break;
                }

                /* get ready for next block */
                bmseqnr = (bmseqnr + 1) % (allocData.no_bmb);
                bmoffset = 0;
            }

            return false;
        }

        /*
 * Get a >valid< deldirentry starting from deldirentrynr ddnr
 * deldir is assumed present and enabled
 */
        public static async Task<deldirentry> GetDeldirEntry(int ddnr, globaldata g)
        {
            var rext = g.currentvolume.rblkextension;
            CachedBlock ddblk;
            deldirentry dde;
            var blk = rext.rblkextension;
            var maxdelentrynr = blk.deldirsize * Constants.DELENTRIES_PER_BLOCK - 1;
            ushort oldlock;

            while (ddnr <= maxdelentrynr)
            {
                /* get deldirentry */
                if ((ddblk = await GetDeldirBlock((ushort)(ddnr / Constants.DELENTRIES_PER_BLOCK), g)) == null)
                    break;

                oldlock = ddblk.used;
                Macro.Lock(ddblk, g);
                var ddblk_blk = ddblk.dirblock;
                dde = DelDirEntryReader.Read(ddblk_blk.entries, ddnr % Constants.DELENTRIES_PER_BLOCK);

                /* check if dde valid */
                if (await IsDelfileValid(dde, ddblk, g))
                {
                    /* later --> check if blocks retaken !! */
                    /* can be done by scanning bitmap!!     */
                    return dde;
                }

                ddnr++;
                ddblk.used = oldlock;
            }

            /* nothing found */
            return null;
        }

        public static async Task<IEnumerable<direntry>> GetDirEntries(uint dirnodenr, globaldata g)
        {
            canode anode = new canode();
            var eod = false;
            uint anodeoffset;
            var dirEntries = new List<direntry>();

            await anodes.GetAnode(anode, dirnodenr, g);
            anodeoffset = 0;
            var dirblock = await LoadDirBlock(anode.blocknr, g);
            var blk = dirblock.dirblock;

            while (!eod) /* eod stands for end-of-dir */
            {
                /* scan block */
                var entryIndex = 0;
                direntry entry;
                do
                {
                    entry = DirEntryReader.Read(blk.entries, entryIndex);

                    if (entry.next != 0)
                    {
                        dirEntries.Add(entry);
                    }

                    entryIndex += entry.next;
                } while (entry.next != 0 && entryIndex + entry.next < blk.entries.Length);

                /* load next block */
                var result = await anodes.NextBlock(anode, anodeoffset, g);
                anodeoffset = result.Item2;
                if (result.Item1)
                {
                    dirblock = await LoadDirBlock(anode.blocknr + anodeoffset, g);
                    blk = dirblock.dirblock;
                }
                else
                {
                    eod = true;
                }
            }

            return dirEntries;
        }

/* Allocate deldirslot. Free anodechain attached to slot and clear it.
 * An intermediate update is possible, due to FreeAnodesInChain()
 */
        public static async Task<int> AllocDeldirSlot(globaldata g)
        {
            CachedBlock rext = g.currentvolume.rblkextension; // crootblockextension
            CachedBlock ddblk; // cdeldirblock
            deldirentry dde;
            int ddnr = 0;
            uint anodenr;

            /* get deldirentry and update roving ptr */
            var rextBlk = rext.rblkextension;
            ddnr = rextBlk.deldirroving;
            if ((ddblk = await GetDeldirBlock((ushort)(ddnr / Constants.DELENTRIES_PER_BLOCK), g)) == null)
            {
                rextBlk.deldirroving = 0;
                return 0;
            }

            var ddblkBlk = ddblk.deldirblock;
            dde = ddblkBlk.entries[ddnr % Constants.DELENTRIES_PER_BLOCK];
            rextBlk.deldirroving =
                (ushort)((rextBlk.deldirroving + 1) % (rextBlk.deldirsize * Constants.DELENTRIES_PER_BLOCK));
            await Update.MakeBlockDirty(ddblk, g);

            anodenr = dde.anodenr;
            if (anodenr != 0)
            {
                /* clear it for reuse */
                dde.anodenr = 0;

                /* free attached anodechain */
                await FreeAnodesInChain(anodenr, g); /* only FREE anodes, not blocks!! */
            }

            // DB(Trace(1, "AllocDelDirSlot", "Allocate slot %ld\n", ddnr));
            return ddnr;
        }

/* Add a file to the deldir. 
 * Deldir assumed enabled here, and info assumed a file (ST_FILE)
 * ddnr is deldir slot to use. Slot is assumed to be allocated by
 * AllocDeldirSlot()
 */
        public static async Task AddToDeldir(objectinfo info, int ddnr, globaldata g)
        {
            CachedBlock ddblk; // cdeldirblock
            deldirentry dde;
            direntry de = info.file.direntry;
            CachedBlock rext; // crootblockextension
            //struct DateStamp time;

            //DB(Trace(1, "AddToDeldir", "slotnr %ld\n", ddnr));
            /* get deldirentry to put it in */
            ddblk = await GetDeldirBlock((ushort)(ddnr / Constants.DELENTRIES_PER_BLOCK), g);
            var ddblkBlk = ddblk.deldirblock;
            dde = ddblkBlk.entries[ddnr % Constants.DELENTRIES_PER_BLOCK];

            /* put new one in */
            dde.anodenr = de.anode;
            SetDDFileSize(dde, GetDEFileSize(de, g), g);
            // dde->creationday = de->creationday;
            // dde->creationminute = de->creationminute;
            // dde->creationtick = de->creationtick;
            dde.CreationDate = de.CreationDate;
            dde.filename = de.Name.Substring(0, Math.Min(Constants.DELENTRYFNSIZE - 1, (int)de.nlength));
            //strncpy(&dde->filename[1], &de->startofname, dde->filename[0]);

            /* Touch deldir block. Inserted here, simply because this the only
             * place touching the deldir will be needed.
             * Note: Only this copy is touched ...
             */
            // DateStamp(&time);
            rext = g.currentvolume.rblkextension;

            // ddblk->blk.creationday = rext->blk.dd_creationday = (UWORD)time.ds_Days;
            // ddblk->blk.creationminute = rext->blk.dd_creationminute = (UWORD)time.ds_Minute;
            // ddblk->blk.creationtick = rext->blk.dd_creationtick = (UWORD)time.ds_Tick;
            var rextBlk = rext.rblkextension;
            ddblkBlk.CreationDate = rextBlk.dd_creationdate;

            /* dirtify block */
            await Update.MakeBlockDirty(ddblk, g);
        }

        public static void SetDDFileSize(deldirentry dde, uint size, globaldata g)
        {
            dde.fsize = size;
#if LARGE_FILE_SIZE
	if (!LARGE_FILE_SIZE || !g->largefile)
		return;
	dde->fsizex = (UWORD)(size >> 32);
#endif
        }

        public static void SetDEFileSize(dirblock dirBlock, direntry direntry, uint size, globaldata g)
        {
            var de = DirEntryReader.Read(dirBlock.entries, direntry.Offset);
            if (!g.largefile)
                de.fsize = size;
#if LARGE_FILE_SIZE
	else {
		struct extrafields extrafields;
		UWORD high = (UWORD)(size >> 32);
		GetExtraFields(direntry, &extrafields);
		if (extrafields.fsizex != high) {
			extrafields.fsizex = high;
			AddExtraFields(direntry, &extrafields);
		}
		direntry->fsize = (ULONG)size;
	}
#endif
            DirEntryWriter.Write(dirBlock.entries, de.Offset, de);
        }

/* Change a directoryentry. Covers all reference changing too

 * If direntry==NULL no new direntry is to be added, only removed.
 * result may be NULL then as well
 *
 * in: from, to, destdir
 * out: result
 *
 * from can become INVALID..
 */

        public static async Task ChangeDirEntry(objectinfo from, direntry to, objectinfo destdir, fileinfo result,
            globaldata g)
        {
            uint destanodenr = Macro.IsRoot(destdir) ? Constants.ANODE_ROOTDIR : Macro.FIANODENR(destdir.file);

            /* check whether a 'within dir' rename */
            if (to != null && destanodenr == from.file.dirblock.dirblock.anodenr)
                await RenameWithinDir(from, to, result, g);
            else
                await RenameAcrossDirs(from, to, destdir, result, g);
        }

        public static void AddExtraFields(direntry direntry, extrafields extra)
        {
            direntry.ExtraFields = extra;

            // ushort offset, dirext;
            // ushort[] array = new ushort[16];
            // ushort i = 0, j = 0;
            // ushort flags = 0, orvalue;
            // UWORD *fields = (UWORD *)extra;
            //
            // /* patch protection lower 8 bits */
            extra.prot &= 0xffffff00;
            // offset = (ushort)(SizeOf.DirEntry.Struct + (direntry.nlength) + (Macro.COMMENT(direntry) & 0xfffe));
            // dirext = (UWORD *)((UBYTE *)(direntry) + offset);
            //
            // orvalue = 1;
            // /* fill packed field array */
            // for (i = 0; i < SizeOf.ExtraFields.Struct / 2; i++)
            // {
            //     if (*fields)
            //     {
            //         array[j++] = *fields++;
            //         flags |= orvalue;
            //     }
            //     else
            //     {
            //         fields++;
            //     }
            //
            //     orvalue <<= 1;
            // }
            //
            // /* add fields to direntry */
            // i = j;
            // while (i)
            //     *dirext++ = array[--i];
            // *dirext++ = flags;
            //
            // direntry.next = offset + 2 * j + 2;
        }


/*
 * Rename file within dir
 * NULL destination not allowed
 */
        public static async Task RenameWithinDir(objectinfo from, direntry to, fileinfo result, globaldata g)
        {
            int spaceneeded;
            objectinfo mover = new objectinfo
            {
                file = new fileinfo()
            };

            Macro.Lock(from.file.dirblock, g);
            var fromDirBlock = from.file.dirblock.dirblock;
            mover.file.direntry = Macro.FIRSTENTRY(fromDirBlock);
            mover.file.dirblock = from.file.dirblock;
            spaceneeded = to.next - from.file.direntry.next;
            if (spaceneeded <= 0)
            {
                await RenameInPlace(from, to, result, g);
            }
            else
            {
                /* make space in block
                 */
                while (CheckFit(from.file.dirblock, spaceneeded, g) != null &&
                       from.file.direntry.Offset != mover.file.direntry.Offset)
                {
                    // from.direntry = (struct direntry *)((UBYTE*)from.direntry - mover.direntry->next);
                    from.file.direntry =
                        DirEntryReader.Read(fromDirBlock.entries, from.file.direntry.Offset - mover.file.direntry.next);
                    await MoveToPrevious(mover, mover.file.direntry, result, g);
                }

                if (CheckFit(from.file.dirblock, spaceneeded, g) != null)
                    await RenameInPlace(from, to, result, g);
                else
                    await MoveToPrevious(from, to, result, g);
            }

            Macro.Lock(result.dirblock, g);
        }

/*
 * Check if direntry will fit in,
 * returns position to place it if ok
 */
        public static direntry CheckFit(CachedBlock blok, int needed, globaldata g)
        {
            // struct cdirblock *blok
            direntry entry;
            int i;

            /* goto end of dirblock */
            var blk = blok.dirblock;
            entry = Macro.FIRSTENTRY(blk);
            for (i = 0; entry.next != 0; entry = Macro.NEXTENTRY(blk, entry))
                i += entry.next;

            if (needed + i + 1 <= Macro.DB_ENTRYSPACE(g))
                return entry;
            else
                return null;
        }

/*
 * Moves firstentry to previous block, changing it to to. To can point to de.direntry
 * if wanted.
 * Return new fileinfo in 'result'
 * NB: no need to touch parent: MTP is always followed by another function
 * on the block.
 */
        public static async Task<bool> MoveToPrevious(objectinfo de, direntry to, fileinfo result, globaldata g)
        {
            direntry dest;
            CachedBlock prevblock = new CachedBlock(g); // cdirblock
            canode anode = new canode();
            int removedlen;
            uint prev;

            Macro.Lock(de.file.dirblock, g);

            /* get previous block */
            var dirblockBlk = de.file.dirblock.dirblock;
            await anodes.GetAnode(anode, dirblockBlk.anodenr, g);
            prev = 0;
            while (anode.blocknr != de.file.dirblock.blocknr && anode.next != 0)
            {
                prev = anode.nr;
                await anodes.GetAnode(anode, anode.next, g);
            }

            /* savety check */
            if (anode.blocknr != de.file.dirblock.blocknr)
            {
                throw new IOException("AFS_ERROR_CACHE_INCONSISTENCY");
            }

            /* Get dirblock in question
             * Special case : previous == 0!!->add new head!!
             */
            if (prev != 0)
            {
                await anodes.GetAnode(anode, prev, g);
                if ((prevblock = await LoadDirBlock(anode.blocknr, g)) == null)
                    return false;
            }

            /* Add new entry */
            if (prev != 0 && (dest = CheckFit(prevblock, to.next, g)) != null)
            {
                // memcpy(dest, to, to->next);
                // *(UBYTE*)NEXTENTRY(dest) = 0; /* end of dirblock */
                var dirBlock = prevblock.dirblock;
                DirEntryWriter.Write(dirBlock.entries, dest.Offset, to);
                BigEndianConverter.ConvertUInt16ToBytes(0, dirBlock.entries,
                    dest.Offset + to.next); /* end of dirblock */
                result.direntry = dest;
                result.dirblock = prevblock;
            }
            else
            {
                /* make new dirblock .. */
                uint parent;
                canode newanode = new canode();
                CachedBlock newblock; // struct cdirblock *newblock;

                newanode.clustersize = 1;
                var dirBlockBlk = de.file.dirblock.dirblock;
                parent = dirblockBlk.parent;
                if ((newanode.blocknr = Allocation.AllocReservedBlock(g)) == 0)
                {
                    return false;
                }

                if (prev == 0)
                {
                    await anodes.GetAnode(anode, dirBlockBlk.anodenr, g);
                    newanode.nr = anode.nr;
                    newanode.next = anode.nr = await anodes.AllocAnode(anode.next != 0 ? anode.next : anode.nr, g);
                }
                else
                {
                    newanode.nr = await anodes.AllocAnode(anode.nr, g);
                    newanode.next = anode.next;
                    anode.next = newanode.nr;
                }

                await anodes.SaveAnode(anode, anode.nr, g);
                newblock = await MakeDirBlock(newanode.blocknr, newanode.nr, dirBlockBlk.anodenr, parent, g);
                await anodes.SaveAnode(newanode, newanode.nr, g); /* MUST be done AFTER MakeDirBlock */

                /* add entry */
                var newBlockBlk = newblock.dirblock;
                dest = Macro.FIRSTENTRY(newBlockBlk);
                // memcpy(dest, to, to->next);
                // *(UBYTE*)NEXTENTRY(dest) = 0; /* end of dirblock */
                DirEntryWriter.Write(newBlockBlk.entries, dest.Offset, to);
                BigEndianConverter.ConvertUInt16ToBytes(0, newBlockBlk.entries,
                    dest.Offset + to.next); /* end of dirblock */
                result.direntry = dest;
                result.dirblock = newblock;
            }

            Macro.Lock(result.dirblock, g);
            await Update.MakeBlockDirty(result.dirblock, g);

            /* remove old entry & make blocks dirty */
            removedlen = de.file.direntry.next;
            await RemoveDirEntry(de, g);

            /* update references */
            await UpdateChangedRef(de.file, result, -removedlen, g);
            return true;
        }

/*
 * There HAS to be sufficient space!!
 */
        public static async Task RenameInPlace(objectinfo from, direntry to, fileinfo result, globaldata g)
        {
            int dest, start, end;
            int movelen;
            int diff;
            objectinfo parent = new objectinfo();

            Macro.Lock(from.file.dirblock, g);

            /* change date parent */
            if (await GetParent(from, parent, g))
            {
                await Touch(parent, g);
            }

            /* make place for new entry */
            diff = to.next - from.file.direntry.next;
            dest = from.file.direntry.Offset + to.next;
            start = from.file.direntry.Offset + from.file.direntry.next;
            //end = (UBYTE *)&(from.dirblock->blk) + g.RootBlock.ReservedBlksize;
            end = SizeOf.DirBlock.Entries(g);
            movelen = (diff > 0) ? (end - dest) : (end - start);
            // memmove(dest, start, movelen);
            var dirBlock = from.file.dirblock.dirblock;
            for (var i = 0; i < movelen; i++)
            {
                dirBlock.entries[dest + i] = dirBlock.entries[start + i];
            }

            /* fill in new entry */
            // memcpy((UBYTE *)from.direntry, to, to.next);
            DirEntryWriter.Write(dirBlock.entries, from.file.direntry.Offset, to);

            /* fill in result and make block dirty */
            result.direntry = from.file.direntry;
            result.dirblock = from.file.dirblock;
            await Update.MakeBlockDirty(from.file.dirblock, g);

            /* update references */
            await UpdateChangedRef(from.file, result, diff, g);
        }

/*
 * Move a file from one dir to another
 * NULL = delete allowed
 */
        public static async Task RenameAcrossDirs(objectinfo from, direntry to, objectinfo destdir, fileinfo result,
            globaldata g)
        {
            ushort removedlen;

            /* remove old entry (invalidates 'destdir') */
            removedlen = from.file.direntry.next;
            await RemoveDirEntry(from, g);
            if (to != null)
            {
                /* test on volume is not necessary, because file.dirblock = volume.volume !=
                 * from.dirblock
                 * restore 'destdir' (can be invalidated by RemoveDirEntry)
                 */
                if (destdir.file.dirblock == from.file.dirblock &&
                    destdir.file.direntry.Offset > from.file.direntry.Offset)
                {
                    // destdir->file.direntry = (struct direntry *)((UBYTE *)destdir->file.direntry - removedlen);
                    var dirBlock = destdir.file.dirblock.dirblock;
                    destdir.file.direntry =
                        DirEntryReader.Read(dirBlock.entries, destdir.file.direntry.Offset - removedlen);
                }

                /* add new entry */
                await AddDirectoryEntry(destdir, to, result, g);
            }

            await UpdateChangedRef(from.file, result, -removedlen, g);
            if (result != null)
            {
                Macro.Lock(result.dirblock, g);
            }
        }

        public static async Task<uint> ReadFromObject(fileentry file, byte[] buffer, uint size, globaldata g)
        {
            CheckAccess.CheckReadAccess(file, g);

            /* check anodechain, make if not there */
            if (file.anodechain == null)
            {
                //DB(Trace(2,"ReadFromObject","getting anodechain"));
                if ((file.anodechain = await anodes.GetAnodeChain(file.le.anodenr, g)) == null)
                {
                    throw new IOException("ERROR_NO_FREE_STORE");
                }
            }

            // #if ROLLOVER
            if (Macro.IsRollover(file.le.info))
            {
                return await Disk.ReadFromRollover(file, buffer, size, g);
            }
            else
                // #endif
            {
                return await Disk.ReadFromFile(file, buffer, size, g);
            }
        }

        public static async Task<uint> WriteToObject(fileentry file, byte[] buffer, uint size, globaldata g)
        {
            /* check write access */
            CheckAccess.CheckWriteAccess(file, g);

            /* check anodechain, make if not there */
            if (file.anodechain == null)
            {
                if ((file.anodechain = await anodes.GetAnodeChain(file.le.anodenr, g)) == null)
                {
                    throw new IOException("ERROR_NO_FREE_STORE");
                }
            }

            /* changing file -> set notify flag */
            file.checknotify = true;
            g.dirty = true;

            // #if ROLLOVER
            if (Macro.IsRollover(file.le.info))
                return await Disk.WriteToRollover(file, buffer, size, g);
            else
                return await Disk.WriteToFile(file, buffer, size, g);
        }

/*
 * Updates size field of links
 */
        public static async Task UpdateLinks(direntry object_, globaldata g)
        {
            canode linklist = new canode();
            objectinfo loi = new objectinfo();
            extrafields extrafields = new extrafields();
            uint linknr;

            //ENTER("UpdateLinks");
            GetExtraFields(object_, extrafields);
            linknr = extrafields.link;
            while (linknr != 0)
            {
                /* Update link: get link object info and update size */
                await anodes.GetAnode(linklist, linknr, g);
                await Lock.FetchObject(linklist.blocknr, linklist.nr, loi, g);
                loi.file.direntry.fsize = object_.fsize;
                await Update.MakeBlockDirty(loi.file.dirblock, g);
                linknr = linklist.next;
            }
        }

        /* DeleteObject
 *
 * Specification:
 *
 * - The object referenced by the info structure is removed
 * - The object must be on currentvolume
 *
 * Implementation:
 *
 * - check deleteprotection
 * - if dir, check if directory is empty
 * - check if there are outstanding locks on object 
 * - remove object from directory and free anode
 * - rearrange & store directory
 *
 * Don't check dirtycount!
 * info becomes INVALID!
 */
        public static async Task DeleteObject(objectinfo info, globaldata g)
        {
            uint anodenr;

            //ENTER("DeleteObject");
            /* Check deleteprotection */
// #if DELDIR
            if (info == null || info.deldir.special <= Constants.SPECIAL_DELFILE ||
                (info.file.direntry.protection & Constants.FIBF_DELETE) == Constants.FIBF_DELETE)
// #else
// 	if (!info || IsVolume(*info) || info->file.direntry->protection & FIBF_DELETE)
// #endif
            {
                throw new IOException("ERROR_DELETE_PROTECTED");
            }

            /* Check if link, links can always be removed */
            if ((info.file.direntry.type == Constants.ST_LINKFILE) ||
                (info.file.direntry.type == Constants.ST_LINKDIR))
            {
                await DeleteLink(info, g);
            }

            anodenr = Macro.FIANODENR(info.file);

            /* Check if there are outstanding locks on object */
            // if (ScanLockList(HeadOf(&g->currentvolume->fileentries), anodenr))
            // {
            // 	DB(Trace(1, "Delete", "object in use"));
            // 	*error = ERROR_OBJECT_IN_USE;
            // 	return DOSFALSE;
            // }

            /* Check if object has links,
             * if it does the object should not be deleted,
             * just the direntry
             */
            if (!await RemapLinks(info, g))
            {
                /* Remove object from directory and free anode */
                if (info.file.direntry.type == Constants.ST_USERDIR)
                {
                    await DeleteDir(info, g);
                }
                else
                {
                    /* ST_FILE or ST_SOFTLINK */
                    anodechain achain;
                    var do_deldir = g.deldirenabled && info.file.direntry.type == Constants.ST_FILE;

                    if ((achain = await anodes.GetAnodeChain(anodenr, g)) == null)
                    {
                        throw new IOException("ERROR_NO_FREE_STORE");
                    }

                    if (do_deldir)
                    {
                        var ddslot = await AllocDeldirSlot(g);
                        await AddToDeldir(info, ddslot, g);
                    }

                    await ChangeDirEntry(info, null, null, null, g); /* remove direntry */
                    if (do_deldir)
                    {
                        await Allocation.FreeBlocksAC(achain, Constants.ULONG_MAX, freeblocktype.keepanodes, g);
                    }
                    else
                    {
                        await Allocation.FreeBlocksAC(achain, Constants.ULONG_MAX, freeblocktype.freeanodes, g);
                        await anodes.FreeAnode(achain.head.an.nr, g);
                    }

                    anodes.DetachAnodeChain(achain, g);
                }
            }

            //return true;
        }

/* 
 * Delete directory
 */
        public static async Task<bool> DeleteDir(objectinfo info, globaldata g)
        {
            canode anode = new canode();
            canode chnode = new canode();
            var volume = g.currentvolume;
            CachedBlock dirblk; // cdirblock
            ushort t;
            var alloc_data = g.glob_allocdata;

            anode.nr = Macro.FIANODENR(info.file);
            if (!await DirIsEmpty(anode.nr, g))
            {
                throw new IOException("ERROR_DIRECTORY_NOT_EMPTY");
            }
            else
            {
                /* check if tobefreedcache is sufficiently large,
                 * otherwise update disk
                 */
                chnode.next = anode.nr;
                for (t = 1; chnode.next != 0; t++)
                {
                    await anodes.GetAnode(chnode, chnode.next, g);
                }

                if (2 * t + alloc_data.rtbf_index > Constants.RTBF_THRESHOLD)
                {
                    await Update.UpdateDisk(g);
                }

                /* do it (btw: fails if dirblock contains more than 128 empty
                 * blocks)
                 */
                anode.next = anode.nr;
                while (anode.next != 0)
                {
                    await anodes.GetAnode(anode, anode.next, g);

                    /* remove dirblock from list if there */
                    dirblk = Lru.CheckCache(volume.dirblks, Constants.HASHM_DIR, anode.blocknr, g);
                    if (dirblk != null)
                    {
                        Macro.MinRemove(dirblk, g);
                        if (dirblk.changeflag)
                            Lru.ResToBeFreed(dirblk.oldblocknr, g);

                        Lru.FreeLRU(dirblk, g);
                    }

                    await anodes.FreeAnode(anode.nr, g);
                    Lru.ResToBeFreed(anode.blocknr, g);
                }

                await ChangeDirEntry(info, null, null, null, g); // delete entry from parentdir
                return true;
            }
        }


/* Check if directory with anodenr [anodenr] is empty 
 * There can be multiple empty directory blocks
 */
        public static async Task<bool> DirIsEmpty(uint anodenr, globaldata g)
        {
            canode anode = new canode();
            CachedBlock dirblok; // cdirblock

            await anodes.GetAnode(anode, anodenr, g);
            dirblok = await LoadDirBlock(anode.blocknr, g);

            var blk = dirblok?.dirblock;
            while (dirblok != null && (blk != null && blk.entries[0] == 0) && anode.next != 0)
            {
                await anodes.GetAnode(anode, anode.next, g);
                dirblok = await LoadDirBlock(anode.blocknr, g);
                blk = dirblok?.dirblock;
            }

            if (dirblok != null && (blk != null && blk.entries[0] == 0)) /* not empty->entries present */
                return true;
            else
                return false;
        }

/*
 * Removes link from linklist and kills direntry
 */
        public static async Task DeleteLink(objectinfo link, globaldata g)
        {
            canode linknode = new canode();
            canode linklist = new canode();
            extrafields extrafields = new extrafields();
            objectinfo object_ = new objectinfo();
            objectinfo directory = new objectinfo();
            var entrybuffer = new byte[Macro.MAX_ENTRYSIZE];

            /* get node to remove */
            await anodes.GetAnode(linknode, link.file.direntry.anode, g);
            GetExtraFields(link.file.direntry, extrafields);

            /* delete old entry */
            await ChangeDirEntry(link, null, null, null, g);

            /* get object */
            await Lock.FetchObject(linknode.clustersize, extrafields.link, object_, g);
            GetExtraFields(object_.file.direntry, extrafields);

            /* if the object lists our link as the first link, redirect it to the next one */
            if (extrafields.link == linknode.nr)
            {
                extrafields.link = linknode.next;
                //memcpy(entrybuffer, object_.file.direntry, object_.file.direntry->next);
                DirEntryWriter.Write(entrybuffer, 0, object_.file.direntry);
                //AddExtraFields(entrybuffer, extrafields);
                AddExtraFields(object_.file.direntry, extrafields);
                if (!await GetParent(object_, directory, g))
                {
                    throw new IOException("ERROR_DISK_NOT_VALIDATED");
                    //return false;	// should never happen
                }
                else
                {
                    await ChangeDirEntry(object_, object_.file.direntry, directory, object_.file, g);
                }
            }
            /* otherwise simply remove the link from the list of links */
            else
            {
                await anodes.GetAnode(linklist, extrafields.link, g);
                while (linklist.next != linknode.nr)
                {
                    await anodes.GetAnode(linklist, linklist.next, g);
                }

                linklist.next = linknode.next;
                await anodes.SaveAnode(linklist, linklist.nr, g);
            }

            await anodes.FreeAnode(linknode.nr, g);
        }

/*
 * Removes head of linklist and promotes first link as
 * master (NB: object is the main object, NOT a link).
 * Returns linkstate: TRUE: a link was promoted
 *                    FALSE: there was no link to promote
 */
        public static async Task<bool> RemapLinks(objectinfo object_, globaldata g)
        {
            extrafields extrafields = new extrafields();
            canode linknode = new canode();
            objectinfo link = new objectinfo();
            objectinfo directory = new objectinfo();
            direntry destentry;
            var entrybuffer = new byte[Macro.MAX_ENTRYSIZE];

            //ENTER("RemapLinks");
            /* get head of linklist */
            GetExtraFields(object_.file.direntry, extrafields);
            if (extrafields.link == 0)
            {
                return false;
            }

            /* the file has links; get head of list
             * we are going to promote this link to
             * an object 
             */
            await anodes.GetAnode(linknode, extrafields.link, g);

            /* get direntry belonging to this linknode */
            await Lock.FetchObject(linknode.blocknr, linknode.nr, link, g);

            /* Promote it from link to object */
            //destentry = (struct direntry *)entrybuffer;
            //memcpy(destentry, link.file.direntry, link.file.direntry.next);
            destentry = link.file.direntry;
            GetExtraFields(link.file.direntry, extrafields);
            destentry.type = object_.file.direntry.type; // is this necessary?
            destentry.fsize = object_.file.direntry.fsize; // is this necessary?
            destentry.anode = object_.file.direntry.anode; // is this necessary?

            extrafields.link = linknode.next;
            AddExtraFields(destentry, extrafields);

            DirEntryWriter.Write(entrybuffer, 0, destentry);

            /* Free old linklist node */
            await anodes.FreeAnode(linknode.nr, g);

            /* Remove source direntry */
            await ChangeDirEntry(object_, null, null, null, g);

            /* Refetch new head (can have become invalid) */
            await Lock.FetchObject(linknode.blocknr, linknode.nr, link, g);
            if (await GetParent(link, directory, g))
            {
                await ChangeDirEntry(link, destentry, directory, link.file, g);
            }

            /* object directory has changed; update link chain
             * new directory is the old chain head was in: linknode.linkdir (== linknode.blocknr)
             */
            await UpdateLinkDir(link.file.direntry, linknode.blocknr, g);
            return true;
        }

/*
 * Update linklist to reflect new directory of linked to object
 */
        public static async Task UpdateLinkDir(direntry object_, uint newdiran, globaldata g)
        {
            canode linklist = new canode();
            extrafields extrafields = new extrafields();
            uint linknr;

            //ENTER("UpdateLinkDir");
            GetExtraFields(object_, extrafields);
            linknr = extrafields.link;
            while (linknr != 0)
            {
                /* update linklist: change clustersize (== object dir) */
                await anodes.GetAnode(linklist, linknr, g);
                linklist.clustersize = newdiran;
                await anodes.SaveAnode(linklist, linklist.nr, g);
                linknr = linklist.next;
            }
        }

        /* RenameAndMove
 *
 * Specification:
 *
 * - rename object
 * - renaming directories into a child not allowed!
 *
 * Rename across devices tested in dd_Rename (DosToHandlerInterface)
 *
 * Implementation:
 * 
 * - source ophalen
 * - check if new name allowed
 * - destination maken
 * - remove source direntry
 * - add destination direntry
 *
 * maxneeds: 2 dblk changed, 1 new an : 3 res
 *
 * sourcedi = objectinfo of source directory
 * destdi = objectinfo of destination directory
 * srcinfo = objectinfo of source
 * destinfo = objectinfo of destination
 * src- destanodenr = anodenr of source- destination directory
 */
        public static async Task<bool> RenameAndMove(objectinfo sourcedi, objectinfo srcinfo, objectinfo destdi,
            string destname, globaldata g)
        {
            direntry srcdirentry, destentry;
            var entrybuffer = new byte[Macro.MAX_ENTRYSIZE];
            uint srcanodenr, destanodenr;
            short srcfieldoffset, destfieldoffset, fieldsize;
            objectinfo destinfo = new objectinfo();
            //objectinfo destdi = new objectinfo();
            //string srccomment, destcomment;
            //string destname;

            // COMMENTED: Already set
            // /* fetch source info & path and check if exists */
            // if (!(destname = GetFullPath (destdir, destpath, destdi, error, g)))
            // {
            //     throw new IOException("ERROR_OBJECT_NOT_FOUND");
            // }

            /* source nor destination may be a volume */
            if (Macro.IsVolume(srcinfo) || string.IsNullOrEmpty(destname))
            {
                throw new IOException("ERROR_OBJECT_WRONG_TYPE");
            }

            // #if DELDIR
            if (Macro.IsDelDir(sourcedi) || Macro.IsDelDir(destdi) || Macro.IsDelDir(srcinfo))
            {
                throw new IOException("ERROR_WRITE_PROTECTED");
            }
            // #endif

            /* check reserved area lock */
            if (Macro.ReservedAreaIsLocked(g))
            {
                throw new IOException("ERROR_DISK_FULL");
            }

            srcdirentry = srcinfo.file.direntry;
            //srccomment = Macro.COMMENT(srcdirentry);

            /* check if new name allowed
             * destpath should exist and file should not
             * %9.1 the same name IS allowed (rename 'hello' to 'Hello')
             */
            destinfo = destdi.Clone();
            if (await Find(destinfo, destname, g))
            {
                if (destinfo.file.direntry.Offset != srcinfo.file.direntry.Offset)
                {
                    throw new IOException("ERROR_OBJECT_EXISTS");
                }
            }

            /* Test if a directory is being renamed to a child of itself. This is so
             * if:
             * 1) source (srcinfo) is a directory and
             * 2) sourcepath (sourcedi) <> destinationpath (destdi) and
             * 3) source (srcinfo) is part of destpath (destdi)
             * Example: rename a/b to a/b/c/d:
             * 1) a/b is dir [ok]; 2) a <> a/b/c [ok]; 3) a/b is part of a/b/c [ok]
             * Links need special attention! 
             */
            srcanodenr = Macro.IsRootA(sourcedi) ? Constants.ANODE_ROOTDIR : Macro.FIANODENR(sourcedi.file);
            destanodenr = Macro.IsRootA(destdi) ? Constants.ANODE_ROOTDIR : Macro.FIANODENR(destdi.file);
            if (Macro.IsRealDir(srcinfo) && (srcanodenr != destanodenr) && await IsChildOf(destdi, srcinfo, g))
            {
                throw new IOException("ERROR_OBJECT_IN_USE");
            }

            /* Make destination  */
            //destentry = (struct direntry *)&entrybuffer;
            //destentry = srcdirentry;
            destentry = new direntry
            {
                Offset = srcdirentry.Offset,
                next = srcdirentry.next,
                type = srcdirentry.type,
                anode = srcdirentry.anode,
                fsize = srcdirentry.fsize,
                protection = srcdirentry.protection,
                nlength = (byte)destname.Length,
                Name = destname,
                comment = srcdirentry.comment
            };

            /* copy header */
            //memcpy(destentry, srcdirentry, offsetof(struct direntry, nlength));

            /* copy name */
            // destentry.nlength = (byte)destname.Length;
            //    if (destentry.nlength > Macro.FILENAMESIZE(g) - 1)
            //    {
            //        destentry.nlength = Macro.FILENAMESIZE(g) - 1;
            //    }
            // memcpy((UBYTE *)&destentry->startofname, destname, destentry->nlength);
            //
            // /* copy comment */
            // destcomment = (UBYTE *)&destentry->startofname + destentry->nlength;
            // memcpy(destcomment, srccomment, *srccomment + 1);

            /* copy fields */
            srcfieldoffset =
                (short)((SizeOf.DirEntry.Struct + srcdirentry.nlength + srcdirentry.comment.Length) & 0xfffe);
            destfieldoffset = (short)((SizeOf.DirEntry.Struct + destname.Length + srcdirentry.comment.Length) & 0xfffe);
            fieldsize = (byte)(srcdirentry.next - srcfieldoffset);
            //    if (g.dirextension)
            //    {
            //        //memcpy((UBYTE *)destentry + destfieldoffset, (UBYTE *)srcdirentry + srcfieldoffset, fieldsize);
            //    }

            /* set size */
            if (g.dirextension)
                destentry.next = (byte)(destfieldoffset + fieldsize);
            else
                destentry.next = (byte)destfieldoffset;

            /* remove source and add new direntry 
             * Makes srcinfo INVALID
             */
            //PFSDoNotify(&srcinfo->file, TRUE, g);

            await ChangeDirEntry(srcinfo, destentry, destdi, destinfo.file, g); // output:destinfo

            /* Update linklist and notify source if object moved across dirs
             */
            if (destanodenr != srcanodenr)
            {
                await MoveLink(destentry, destanodenr, g);
                //PFSDoNotify (&destinfo.file, TRUE, g);
            }
            // else
            // {
            // 	//PFSDoNotify (&destinfo.file, FALSE, g);
            // }

            /* If object is a directory and parent changed, update dirblocks */
            if (Macro.IsDir(destinfo) && (srcanodenr != destanodenr))
            {
                canode anode = new canode();
                uint anodeoffset;
                var gadoor = true;

                anode.nr = destinfo.file.direntry.anode;
                anodeoffset = 0;
                await anodes.GetAnode(anode, anode.nr, g);
                for (anodeoffset = 0; gadoor;)
                {
                    CachedBlock blk; // cdirblock

                    blk = await LoadDirBlock(anode.blocknr + anodeoffset, g);
                    if (blk != null)
                    {
                        var dirBlockBlk = blk.dirblock;
                        dirBlockBlk.parent = destanodenr; // destination dir
                        await Update.MakeBlockDirty(blk, g);
                    }

                    var nextBlockResult = await anodes.NextBlock(anode, anodeoffset, g);
                    gadoor = nextBlockResult.Item1;
                    anodeoffset = nextBlockResult.Item2;
                }
            }

            return true;
        }

        public static async Task<bool> IsChildOf(objectinfo child, objectinfo parent, globaldata g)
        {
            objectinfo up = new objectinfo();
            bool goon = true;

            while (goon && !Macro.IsSameOI(child, parent))
            {
                goon = await GetParent(child, up, g);
                child = up;
            }

            return Macro.IsSameOI(child, parent);
        }

/* 
 * Update linklist to reflect moved node
 * (is supercopy of UpdateLinkDir)
 */
        public static async Task MoveLink(direntry object_, uint newdiran, globaldata g)
        {
            canode linklist = new canode();
            extrafields extrafields = new extrafields();
            uint linknr;

            //ENTER("MoveLink");
            GetExtraFields(object_, extrafields);

            /* check if is link or linked to */
            if ((linknr = extrafields.link) == 0)
            {
                return;
            }

            /* check filetype */
            if (object_.type == Constants.ST_LINKDIR || object_.type == Constants.ST_LINKFILE)
            {
                /* it is a link -> just change the linkdir */
                await anodes.GetAnode(linklist, object_.anode, g);
                linklist.blocknr = newdiran;
                await anodes.SaveAnode(linklist, linklist.nr, g);
            }
            else
            {
                /* it is the head (linked to) */
                while (linknr != 0)
                {
                    /* update linklist: change clustersize (== object dir) */
                    await anodes.GetAnode(linklist, linknr, g);
                    linklist.clustersize = newdiran; /* the object's directory */
                    await anodes.SaveAnode(linklist, linklist.nr, g);
                    linknr = linklist.next;
                }
            }
        }
    }
}