namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;

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

        public static async Task<IEntry> NewDir(objectinfo parent, string dirname, globaldata g)
        {
            objectinfo info = new objectinfo
            {
                file = new fileinfo()
            };
            listentry fileentry;
            ListType type = new ListType();
            CachedBlock blk;
            uint parentnr, blocknr;
            byte[] entrybuffer = new byte[Macro.MAX_ENTRYSIZE];
            int l;

            /* check disk-writeprotection etc */
            if (!Volume.CheckVolume(g.currentvolume, true, g))
                return null;

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
            if ((fileentry = await Lock.MakeListEntry(info, type, g)) == null)
            {
                // goto error2;
                await DiskFullError(info, fileentry, g);
            }

            if (!Lock.AddListEntry(fileentry, g)) /* Should never fail, accessconflict impossible */
            {
                //ErrorMsg(AFS_ERROR_NEWDIR_ADDLISTENTRY, NULL, g);
                // goto error2;
                await DiskFullError(info, fileentry, g);
            }

            /* Make first directoryblock (needed for parentfinding) */
            if ((blocknr = Allocation.AllocReservedBlock(g)) == 0)
            {
                //*error = ERROR_DISK_FULL;
                // error2:
                await DiskFullError(info, fileentry, g);
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

        private static async Task DiskFullError(objectinfo info, listentry fileentry, globaldata g)
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
            endofblok = g.RootBlock.ReservedBlksize;
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
                while (entry.next != 0)
                {
                    found = intl_name == entry.Name;
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
                for (i = 0; entry.next > 0; entry = DirEntryReader.Read(blk.entries, entry.next))
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
            direntry.type = (byte)type;
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
            throw new NotImplementedException();
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
        public static async Task<bool> GetDir(string dirname, globaldata g)
        {
            objectinfo path = new objectinfo();
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
                throw new IOException("ERROR_OBJECT_NOT_FOUND");
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
            var maxdelentrynr = blk.deldirsize*Constants.DELENTRIES_PER_BLOCK - 1;
            ushort oldlock;

            while (ddnr <= maxdelentrynr)
            {
                /* get deldirentry */
                if ((ddblk = await GetDeldirBlock((ushort)(ddnr/Constants.DELENTRIES_PER_BLOCK), g)) == null)
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
        
        public static async Task<IEnumerable<IDirEntry>> GetDirEntries(uint dirnodenr, globaldata g)
        {
            canode anode = new canode();
            var eod = false;
            uint anodeoffset;
            var dirEntries = new List<IDirEntry>();

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
                } while (entry.next != 0);
                
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
    }
}