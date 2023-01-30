namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    public static class File
    {
        public static async Task<IEntry> Open(objectinfo filefi, bool write, globaldata g)
        {
#if DEBUG
            Pfs3Logger.Instance.Debug($"File: Open '{filefi.file.direntry.Name}'");
#endif
            
            if (Macro.IsSoftLink(filefi))
            {
                throw new IOException("ERROR_IS_SOFT_LINK");
            }
            
            /* check if file (only files can be opened) */
// #if DELDIR
            if ((Macro.IsVolume(filefi) || Macro.IsDelDir(filefi) || Macro.IsDir(filefi)))
// #else
//             if ((IsVolume(filefi) || IsDir(filefi)))
// #endif
            {
                throw new IOException("ERROR_OBJECT_WRONG_TYPE");
            }
            
            var type = new ListType
            {
                value = Constants.ET_FILEENTRY
            };

            if (write)
            {
                // if (!Directory.NewFile(found, pathfi, filename, filefi, g))
                // {
                //     // DB(Trace(1, "Newfile", "output failed"));
                //     return null;
                // }
                type.flags.access = Constants.ET_EXCLWRITE;                
            }
            
            /* Add file to list  */
            IEntry fileFe;
            if ((fileFe = await Lock.MakeListEntry(filefi, type, g)) == null)
            {
                throw new IOException("make list entry error");
            }

            if (!Lock.AddListEntry(fileFe.ListEntry, g))
            {
                //DB(Trace(1, "dd_Open", "AddListEntry failed"));
                Lock.FreeListEntry(fileFe, g);
                throw new IOException("ERROR_OBJECT_IN_USE");
            }

            return fileFe;
        }
        
        public static async Task Close(fileentry fe, globaldata g)
        {
#if DEBUG
            Pfs3Logger.Instance.Debug($"File: Close '{fe.ListEntry.info.file.direntry.Name}'");
#endif
            // SIPTR error;
            // fileentry_t *fe = (fileentry_t *)pkt->dp_Arg1;

            if (fe == null)
            {
                throw new ArgumentNullException(nameof(fe));
            }

            
            if (fe.checknotify)
            {
                Cache.ClearSearchInDirCache(fe.le.dirblocknr, g);

                Volume.CheckVolume(fe.le.volume, true, g);
                await Lru.UpdateLE(fe.le, g);
                await Directory.Touch(fe.le.info, g);
                var size = Directory.GetDEFileSize(fe.le.info.file.direntry, g);
                if (fe.originalsize != size)
                {
                    var dirBlock = fe.le.info.file.dirblock.dirblock;
                    await Directory.UpdateLinks(fe.le.info.file.direntry, g);
                }

                //PFSDoNotify(&fe->le.info.file, TRUE, g);
                fe.checknotify = false;
            }

            Lock.RemoveListEntry(fe, g);
        }

        public static void MakeSharedFileEntriesAndClear(globaldata g)
        {
            foreach (var fileEntry in g.currentvolume.fileentries)
            {
                fileEntry.ListEntry.type.flags.access = Constants.ET_SHAREDWRITE;
            }
            
            g.currentvolume.fileentries.Clear();
        }
    }
}