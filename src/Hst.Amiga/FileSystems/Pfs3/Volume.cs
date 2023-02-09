namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Blocks;

    public static class Volume
    {
/* make and fill in volume structure
 * uses g->geom!
 * returns 0 is fails
 */
        public static async Task<volumedata> MakeVolumeData(RootBlock rootblock, globaldata g)
        {
            //  struct volumedata *volume;
            //  struct MinList *list;
            //
            // ENTER("MakeVolumeData");

            // volume = AllocMemPR (sizeof(struct volumedata), g);
            var volume = new volumedata
            {
                rootblk = rootblock,
                rootblockchangeflag = false
            };

            /* lijsten initieren */
            // for (list = &volume.fileentries; list <= &volume->notifylist; list++)
            //     NewList((struct List *)list);
            // for (var node = volume.fileentries.First; list <= volume->notifylist; list++)
            // {
            //     list = node.Value;
            //     NewList((struct List *)list);
            // }


            /* andere gegevens invullen */
            volume.numsofterrors = 0;
            volume.diskstate = Constants.ID_VALIDATED;

            /* these could be put in rootblock @@ see also HD version */
            volume.numblocks = g.TotalSectors;
            volume.bytesperblock = (ushort)g.blocksize;
            volume.rescluster = (ushort)(rootblock.ReservedBlksize / volume.bytesperblock);

            /* Calculate minimum fake block size that keeps total block count less than 16M.
             * Workaround for programs (including WB) that calculate free space using
             * "in use * 100 / total" formula that overflows if in use is block count is larger
             * than 16M blocks with 512 block size. Used only in ACTION_INFO.
             */
            g.infoblockshift = 0;
            // if (DOSBase->dl_lib.lib_Version < 50)
            // {
            //     ushort blockshift = 0;
            //     var bpb = volume.bytesperblock;
            //     while (bpb > 512)
            //     {
            //         blockshift++;
            //         bpb >>= 1;
            //     }
            //
            //     // Calculate smallest safe fake block size, up to max 32k. (512=0,1024=1,..32768=6)
            //     while ((volume.numblocks >> blockshift) >= 0x02000000 && g.infoblockshift < 6)
            //     {
            //         g.infoblockshift++;
            //         blockshift++;
            //     }
            // }

            /* load rootblock extension (if it is present) */
            if (rootblock.Extension > 0 && rootblock.Options.HasFlag(RootBlock.DiskOptionsEnum.MODE_EXTENSION))
            {
                var rext = new CachedBlock();

                // rext = AllocBufmemR(sizeof(struct cachedblock) +rootblock->reserved_blksize, g);
                // memset(rext, 0, sizeof(struct cachedblock) +rootblock->reserved_blksize);
                IBlock blk;
                if ((blk = await Disk.RawRead<rootblockextension>(volume.rescluster, rootblock.Extension, g)) == null)
                {
                    throw new IOException("AFS_ERROR_READ_EXTENSION");
                }
                else
                {
                    rext.blk = blk;
                    if (rext.blk.id == Constants.EXTENSIONID)
                    {
                        volume.rblkextension = rext;
                        rext.volume = volume;
                        rext.blocknr = rootblock.Extension;
                    }
                    else
                    {
                        throw new IOException("AFS_ERROR_EXTENSION_INVALID");
                    }
                }
            }
            else
            {
                volume.rblkextension = null;
            }

            return volume;
        }

/* free all resources (memory) taken by volume accept doslist
** it is assumed all this data can be discarded (not checked here!)
** it is also assumed this volume is no part of any volumelist
*/
        public static void FreeVolumeResources(volumedata volume, globaldata g)
        {
            // ENTER("Free volume resources");

            if (volume != null)
            {
                FreeUnusedResources(volume, g);
// #if VERSION23
                // if (volume.rblkextension != null)
                // 	FreeBufmem (volume.rblkextension, g);
// #endif
// #if DELDIR
// 	//	if (g->deldirenabled)
// 	//		FreeBufmem (volume->deldir, g);
// #endif
                // FreeBufmem (volume->rootblk, g);
                // FreeMemP (volume, g);
            }

            // EXIT("FreeVolumeResources");
        }

        public static void FreeUnusedResources(volumedata volume, globaldata g)
        {
            // struct MinList *list;
            // struct MinNode *node, *next;

            // ENTER("FreeUnusedResources");

            /* check if volume passed */
            if (volume == null)
                return;

            // for (list = volume->anblks; list<=&volume->bmindexblks; list++)
            
            /* start with anblks!, fileentries are to be kept! */
            // for (list = volume->anblks; list<=&volume->bmindexblks; list++)
            // {
            //     node = (struct MinNode *)HeadOf(list);
            //     while ((next = node->mln_Succ))
            //     {
            //         FlushBlock((struct cachedblock *)node, g);
            //         FreeLRU((struct cachedblock *)node);
            //         node = next;
            //     }
            // }
            // foreach (var list in volume.anblks)
            // {
            //     FreeMinList(list, g);
            // }
            FreeMinList(volume.anblks, g);

            // foreach (var list in volume.dirblks)
            // {
            //     FreeMinList(list, g);
            // }
            FreeMinList(volume.dirblks, g);

            // FreeMinList(volume.indexblks, g);
            FreeMinList(volume.indexblks, g);
            
            // FreeMinList(volume.bmblks, g);
            FreeMinList(volume.bmblks, g);
            
            // FreeMinList(volume.superblks, g);
            FreeMinList(volume.superblks, g);
            
            // FreeMinList(volume.deldirblks, g);
            FreeMinList(volume.deldirblks, g);
            
            // FreeMinList(volume.bmindexblks, g);
            FreeMinList(volume.bmindexblks, g);

            foreach (var node in g.glob_lrudata.LRUpool.Where(x => x.cblk?.blk == null).ToList())
            {
                g.glob_lrudata.LRUpool.Remove(node);
            }
            
            foreach (var node in g.glob_lrudata.LRUqueue.Where(x => x.cblk?.blk == null).ToList())
            {
                g.glob_lrudata.LRUqueue.Remove(node);
            }
        }

        private static void FreeMinList(LinkedList<CachedBlock> list, globaldata g)
        {
            for (var node = list.First; node != null; node = node.Next)
            {
                Lru.FlushBlock(node.Value, g);
                Lru.FreeLRU(node.Value, g);
            }
        }

        private static void FreeMinList(IDictionary<uint, CachedBlock> list, globaldata g)
        {
            var removeKeys = new List<uint>();
            
            foreach (var node in list)
            {
                if (node.Value != null)
                {
                    Lru.FlushBlock(node.Value, g);
                    Lru.FreeLRU(node.Value, g);
                }
                
                removeKeys.Add(node.Key);
            }

            foreach (var key in removeKeys)
            {
                list.Remove(key);
            }
        }
        
        /* CheckVolume checks if a volume (ve lock) is (still) present.
** If volume==NULL (no disk present) then FALSE is returned (@XLII).
** result: requested volume present/not present TRUE/FALSE
*/
        public static void CheckVolume(volumedata volume, bool write, globaldata g)
        {
            if (volume == null || g.currentvolume == null)
            {
                switch (g.disktype)
                {
                    case Constants.ID_UNREADABLE_DISK:
                    case Constants.ID_NOT_REALLY_DOS:
                        throw new IOException("ERROR_NOT_A_DOS_DISK");

                    case Constants.ID_NO_DISK_PRESENT:
                        if (volume == null && g.currentvolume == null)
                        {
                            throw new IOException("ERROR_NO_DISK");
                        }

                        break;
                    default:
                        throw new IOException("ERROR_DEVICE_NOT_MOUNTED");
                }
            }
            else if (g.currentvolume == volume)
            {
                switch (g.diskstate)
                {
                    case Constants.ID_WRITE_PROTECTED:
                        if (write)
                        {
                            throw new IOException("ERROR_DISK_WRITE_PROTECTED");
                        }

                        break;

                    case Constants.ID_VALIDATING:
                        if (write)
                        {
                            throw new IOException("ERROR_DISK_NOT_VALIDATED");
                        }

                        break;

                    case Constants.ID_VALIDATED:
                        if (write && g.softprotect)
                        {
                            throw new IOException("ERROR_DISK_WRITE_PROTECTED");
                        }

                        break;
                }
            }
            else
            {
                throw new IOException("ERROR_DEVICE_NOT_MOUNTED");
            }
        }

        public static async Task<RootBlock> GetCurrentRoot(globaldata g)
        {
            // read boot block
            var blockBytes = await Disk.RawRead(1, Constants.BOOTBLOCK1, g);
            var rootBlock = RootBlockReader.Parse(blockBytes);

            if (!(rootBlock.DiskType == Constants.ID_PFS_DISK || rootBlock.DiskType == Constants.ID_PFS2_DISK))
            {
                throw new IOException("ID_NOT_REALLY_DOS");
            }

            g.disktype = Constants.ID_PFS_DISK;

            // read root block
            blockBytes = await Disk.RawRead(1, Constants.ROOTBLOCK, g);
            rootBlock = RootBlockReader.Parse(blockBytes);
            
            // read reserved bitmap blocks
            var numReserved = Pfs3Formatter.CalcNumReserved(g, rootBlock.ReservedBlksize);
            //var reservedBitmapBlockCount = Pfs3Formatter.CalculateRootBlockAndReservedBitmapBlockCount(rootBlock, numReserved);
            var bytesPerBlock = (ushort)g.blocksize;
            var resCluster = (ushort)(rootBlock.ReservedBlksize / bytesPerBlock);

            var reservedBitmapLongs = numReserved / 32 + 1;
            var reservedBitmapBlocks = reservedBitmapLongs <= rootBlock.LongsPerBmb
                ? 1
                : 1 + (reservedBitmapLongs - rootBlock.LongsPerBmb) / (rootBlock.ReservedBlksize / Amiga.SizeOf.ULong) + 1;

            blockBytes = await Disk.RawRead((uint)reservedBitmapBlocks * resCluster, Constants.ROOTBLOCK + 1, g);
            rootBlock.ReservedBitmapBlock = BitmapBlockReader.Parse(blockBytes, (int)(numReserved / 32 + 1));
            
            /* check size and read all rootblock blocks */
            // 17.10: with 1024 byte blocks rblsize can be 1!
            var rblsize = rootBlock.RblkCluster;
            if (rblsize < 1 || rblsize > 521)
            {
                throw new IOException("ID_NOT_REALLY_DOS");
            }

            // original PFS_DISK with PFS2_DISK features -> don't mount
            if (rootBlock.DiskType == Constants.ID_PFS_DISK && (rootBlock.Options.HasFlag(RootBlock.DiskOptionsEnum.MODE_LARGEFILE) ||
                                                                rootBlock.ReservedBlksize > 1024))
                throw new IOException("ID_NOT_REALLY_DOS");

            Lru.InitLRU(g, rootBlock.ReservedBlksize);
            
            /* size check */
            // if ((rootBlock.Options.HasFlag(RootBlock.DiskOptionsEnum.MODE_SIZEFIELD) &&
            //     (g->geom->dg_TotalSectors != (*rootblock)->disksize))
            // {
            //     throw new IOException("ID_NOT_REALLY_DOS");
            // }

            return rootBlock;
        }

        public static async Task DiskInsertSequence(RootBlock rootBlock, globaldata g)
        {
            var fe = new fileentry
            {
                le = new listentry
                {
                    info = new objectinfo
                    {
                        
                    }
                }
            };
            
            // fe = LOCKTOFILEENTRY(locklist);
            // if(fe->le.type.flags.type == ETF_VOLUME)
            //     g->currentvolume = fe->le.info.volume.volume;
            // else
            //     g->currentvolume = fe->le.volume;
            
            g.currentvolume = await MakeVolumeData(rootBlock, g);
            
            /* update rootblock */
            g.RootBlock = g.currentvolume.rootblk = rootBlock;
            
            /* Reconfigure modules to new volume */
            await Init.InitModules (g.currentvolume, false, g);

            /* create rootblockextension if its not there yet */
            if (g.currentvolume.rblkextension == null &&
                g.diskstate != Constants.ID_WRITE_PROTECTED)
            {
                Pfs3Formatter.MakeRBlkExtension (g);
            }

            /* upgrade deldir */
            if (rootBlock.DelDir > 0)
            {
                /* kill current deldir */
                var ddblk = await Lru.AllocLRU(g);
                if (ddblk != null)
                {
                    if ((ddblk.blk = await Disk.RawRead<deldirblock>(Constants.RESCLUSTER(g), rootBlock.DelDir, g) ) != null)
                    {
                        var blk = ddblk.deldirblock;
                        if (blk.id == Constants.DELDIRID)
                        {
                            for (var i=0; i<31; i++)
                            {
                                var nr = blk.entries[i].anodenr;
                                if (nr > 0)
                                    await Directory.FreeAnodesInChain(nr, g);
                            }
                        }
                    }
                    Lru.FreeLRU(ddblk, g);
                }
                
                /* create new deldir */
                await Directory.SetDeldir(1, g);
                Lru.ResToBeFreed(rootBlock.DelDir, g);
                rootBlock.DelDir = 0;
                rootBlock.Options |= RootBlock.DiskOptionsEnum.MODE_SUPERDELDIR;
            }
            
            /* update datestamp and enable */
            rootBlock.Options |= RootBlock.DiskOptionsEnum.MODE_DATESTAMP; 
            rootBlock.Datestamp++;
            g.dirty = true;
        }
        
/* checks if disk is changed. If so calls NewVolume()
** NB: new volume might be NOVOLUME or NOTAFDSDISK
*/
        public static async Task UpdateCurrentDisk(globaldata g)
        {
            await NewVolume(false, g);
        }
        
        public static async Task NewVolume (bool force, globaldata g)
        {
            bool oldstate, newstate;//, changed;

            /* check if something changed */
            // changed = UpdateChangeCount (g);
            // if (!FORCE && !changed)
            //     return;
	           //
            // if (!AttemptLockDosList(LDF_VOLUMES | LDF_WRITE))
            //     return;

            // ENTER("NewVolume");
#if DEBUG
            Pfs3Logger.Instance.Debug("Volume: NewVolume Enter");
#endif
            Disk.FlushDataCache(g);

            /* newstate <=> there is a PFS disk present */
            oldstate = g.currentvolume != null;
            var rootBlock = await GetCurrentRoot(g);
            newstate = rootBlock != null;

            /* undo error enforced softprotect */
            if (g.softprotect && g.protectkey == ~0)
            {
                g.protectkey = 0;
                g.softprotect = false;
            }

            if (oldstate && !newstate)
            {
                await DiskRemoveSequence (g);
            }

            if (newstate)
            {
                // if (oldstate && SameDisk (rootBlock, g.currentvolume.rootblk))
                // {
                //     // FreeBufmem (rootblock, g);  /* @XLVII */
                // }
                // else
                // {
                //     if (oldstate)
                //     {
                //         await DiskRemoveSequence (g);
                //     }
                //     await DiskInsertSequence(rootBlock, g);
                // }
            }
            else
            {
                g.currentvolume = null;    /* @XL */
            }

            // UnLockDosList(LDF_VOLUMES | LDF_WRITE);

            // UpdateAndMotorOff(g);
            //EXIT("NewVolume");
#if DEBUG
            Pfs3Logger.Instance.Debug("Volume: NewVolume Exit");
#endif
        }
        
/* pre:
**  globaldata->currentvolume not necessarily present
** post:
**  the old currentvolume is updated en als 'removed' currentvolume == 0
** return waarde = currentdisk back in drive?
** used by NewVolume and ACTION_INHIBIT
*/
        public static async Task DiskRemoveSequence(globaldata g)
        {
            volumedata oldvolume = g.currentvolume;

            // ENTER("DiskRemoveSequence");

            /* -I- update disk 
            ** will ask for old volume if there are unsaved changes
            ** causes recursive NewVolume call. That's why 'currentvolume'
            ** has to be cleared first; UpdateDisk won't be called for the
            ** same disk again
            */
            if(oldvolume != null && g.dirty)
            {
                // RequestCurrentVolumeBack(g);
                await Update.UpdateDisk(g);
                return;
            }

            /* disk removed */
            g.currentvolume = null;
            Disk.FlushDataCache(g);

            /* -II- link locks in doslist 
            ** lockentries: link to doslist...
            ** fileentries: link them too...
            */
            // if(!Macro.IsMinListEmpty(&oldvolume->fileentries))
            // {
            //     DB(Trace(1, "DiskRemoveSequence", "there are locks\n"));
            //     oldvolume->devlist->dl_LockList = MKBADDR(&(((listentry_t *)(HeadOf(&oldvolume->fileentries)))->lock));
            //     oldvolume->devlist->dl_Task = NULL;
            //     FreeUnusedResources(oldvolume, g);
            // }
            // else
            // {
            //     DB(Trace(1, "DiskRemoveSequence", "removing doslist\n"));
            //     RemDosEntry((struct DosList*)oldvolume->devlist);
            //     FreeDosEntry((struct DosList*)oldvolume->devlist);
            //     MinRemove(oldvolume);
            //     FreeVolumeResources(oldvolume, g);
            // }

// #ifdef TRACKDISK
//             if(g->trackdisk)
//             {
//                 g->request->iotd_Req.io_Command = CMD_CLEAR;
//                 DoIO((struct IORequest*)g->request);
//             }
// #endif

            // CreateInputEvent(FALSE, g);

// #if ACCESS_DETECT
// 	g->tdmode = ACCESS_UNDETECTED;
// #endif

            // EXIT("DiskRemoveSequence");
            // return;
        }
    }
}