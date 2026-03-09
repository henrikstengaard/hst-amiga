using Hst.Amiga.FileSystems.Exceptions;

namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    public static class File
    {
	    /// <summary>
	    /// 
	    /// </summary>
	    /// <param name="parentfi">Parent objectinfo to open file from.</param>
	    /// <param name="fullname">Relative path from parent to open.</param>
	    /// <param name="mode">File mode.</param>
	    /// <param name="overwrite"></param>
	    /// <param name="g">Globaldata.</param>
	    /// <returns></returns>
	    public static async Task<IEntry> Open(objectinfo parentfi, string fullname, Amiga.FileSystems.FileMode mode,
		    bool overwrite, globaldata g)
        {
	        // static SIPTR dd_Open(struct DosPacket *pkt, globaldata * g)
	        // ARG1 = BPTR Filehandle to fill in
	        // ARG2 = BPTR LOCK on directory ARG3 is relative to
	        // ARG3 = BSTR Name of file to be opened
	        // RES1 = Success/Failure (DOSTRUE/DOSFALSE)
	        // RES2 = failure code
	        //
	        // fullname: name inclusief pat
	        // filename: filename zonder pat
	        // pathfi: fileinfo van path van te openen file
	        // filefi: fileinfo van te openen file
	        // mode: nodetype van fileentry van file
	        //
	        // GURUbook: ACTION_FINDINPUT premits write access (598)
	        
	        // struct FileHandle *filehandle;
	        // listentry_t *filefe;
	        // lockentry_t *parentfe;
	        // union objectinfo pathfi, filefi, *parentfi;
	        // GetFileInfoFromLock(pkt->dp_Arg2, 0, parentfe, parentfi);

	        if (mode == FileSystems.FileMode.Read && overwrite)
	        {
		        throw new FileSystemException("Read mode cannot be used with overwrite");
	        }

	        var pathfi = new objectinfo();
            var filefi = new objectinfo();

            /* 15.9: check if path is file. If so it has to be opened
             * if an empty string was specified as filename
             * (see GuruBook:599)
             */
			
			/* Get path to file */
			var filename = await Directory.GetFullPath(parentfi.Clone(), fullname, pathfi, g);
			if (filename == null)
			{
				throw new IOException("Failed to get full path");
				//return DOSFALSE;
			}

			/* try to locate file */
			var found = await Directory.FindObject(pathfi, filename, filefi, g);

			var isLink = false;
			var originalSize = 0U;
			
            if (found)
            {
	            // update is link and original size as new file calls fetch object, which changes link to dir or file and
	            // therefore entry size will also change
	            isLink = filefi.file.direntry.type == Constants.ST_LINKFILE ||
	                     filefi.file.direntry.type == Constants.ST_LINKDIR;
	            originalSize = filefi.file.direntry.Size;
	            
				/* softlinks cannot directly be opened */
				if (Macro.IsSoftLink(filefi))
				{
					throw new IOException("ERROR_IS_SOFT_LINK");
					//return DOSFALSE;
				}

				/* check if file (only files can be opened) */
				// #if DELDIR
				if ((Macro.IsVolume(filefi) || Macro.IsDelDir(filefi) || Macro.IsDir(filefi)))
				// #else
				// 		if ((IsVolume(filefi) || IsDir(filefi)))
				// #endif
				{
					// ERROR_OBJECT_WRONG_TYPE
					//return DOSFALSE;
					throw new NotAFileException($"Open file '{filename}' failed, entry exists and is not a file or link");
				}
			}

			var type = new ListType
			{
				value = Constants.ET_FILEENTRY
			};

			// #if MULTIUSER
			// #if MU_CHECKDIR
			// 	if (IsVolume(pathfi))
			// 		memset(&path_extrafields, 0, sizeof(struct extrafields));
			// 	else
			// #if DELDIR
			// 		GetExtraFieldsOI(&pathfi, &path_extrafields);
			// #else /* DELDIR */
			// 		GetExtraFields(pathfi.file.direntry, &path_extrafields);
			// #endif /* DELDIR */
			// #endif /* MU_CHECKDIR */
			//
			// 	if (found)
			// #if DELDIR
			// 		GetExtraFieldsOI(&filefi, &extrafields, g);
			// #else
			// 		GetExtraFields(filefi.file.direntry, &extrafields);
			// #endif
			//
			// 	if (g->muFS_ready)
			// 	{
			// #if MU_CHECKDIR
			// 		path_flags = muGetRelationshipA(g->user, (path_extrafields.uid << 16) + path_extrafields.gid, NULL);
			// #endif
			// 		if (found)
			// 			flags = muGetRelationshipA(g->user, (extrafields.uid << 16) + extrafields.gid, NULL);
			// 	}
			// 	else
			// 	{
			// #if MU_CHECKDIR
			// 		path_flags = ((path_extrafields.uid == muNOBODY_UID) << muRelB_NO_OWNER) | muRelF_NOBODY;
			// #endif
			// 		if (found)
			// 			flags = ((extrafields.uid == muNOBODY_UID) << muRelB_NO_OWNER) | muRelF_NOBODY;
			// 	}
			// #endif /* MULTIUSER */

			
			
			
			// 	switch (pkt->dp_Type)
			// 	{
			// 		case ACTION_FINDINPUT:
			// 			if (!found)
			// 				return DOSFALSE;
			//
			// #if MULTIUSER
			// 			if ((*error = muFS_CheckReadAccess(extrafields.prot, flags, g)))
			// 				return DOSFALSE;
			// #endif
			// 			type.flags.access = ET_SHAREDREAD;
			// 			break;
			//
			// 		case ACTION_FINDUPDATE:
			//
			// #if MULTIUSER
			// 			if (found)
			// 			{
			// 				if ((*error = muFS_CheckWriteAccess(extrafields.prot, flags, g)))
			// 					return DOSFALSE;
			// 			}
			// #if MU_CHECKDIR
			// 			else
			// 			{
			// 				if ((*error = muFS_CheckWriteAccess(path_extrafields.prot, path_flags, g)))
			// 					return DOSFALSE;
			// 			}
			// #endif /* MU_CHECKDIR */
			// #endif /* MULTIUSER */
			//
			// 			if (!found)
			// 			{
			// 				if ((*error = NewFile (found, &pathfi, filename, &filefi, g)))
			// 				{
			// 					DB(Trace(1, "NewFile", "update failed"));
			// 					return DOSFALSE;
			// 				}
			// 			}
			//
			// 			type.flags.access = ET_SHAREDWRITE;
			// 			break;
			//
			// 		case ACTION_FINDOUTPUT:
			// 			if (found)
			// 			{
			// #if MULTIUSER
			// 				if ((*error = muFS_CheckDeleteAccess(extrafields.prot, flags, g)))
			// 					return DOSFALSE;
			//
			// 				if ((*error = muFS_CheckWriteAccess(extrafields.prot, flags, g)))
			// 					return DOSFALSE;
			// 			}
			//
			// #if MU_CHECKDIR
			// 			if ((*error = muFS_CheckWriteAccess(path_extrafields.prot, path_flags, g)))
			// 				return DOSFALSE;
			// #endif /* MU_CHECKDIR */
			// #else /* MULTIUSER */
			// 			}
			// #endif /* MULTIUSER */
			//
			// 			if ((*error = NewFile (found, &pathfi, filename, &filefi, g)))
			// 			{
			// 				DB(Trace(1, "Newfile", "output failed"));
			// 				return DOSFALSE;
			// 			}
			//
			// #if 0
			// 			/* NOTE: commented out, this breaks some programs */
			// 			/* version 18.5 fix issue 3286818 */
			// 			type.flags.access = ET_SHAREDWRITE;
			// #endif
			// 			type.flags.access = ET_EXCLWRITE;
			// 			break;
			//
			// 		default:
			// 			*error = ERROR_ACTION_NOT_KNOWN;
			// 			return DOSFALSE;
			// 	}

			switch(mode)
			{
				// case ACTION_FINDINPUT:
				case Amiga.FileSystems.FileMode.Read:
					if (!found)
					{
						// ERROR_OBJECT_NOT_FOUND
						throw new PathNotFoundException($"Path '{filename}' not found");
					}

					var hasReadProtectionBit =
						(filefi.file.direntry.protection & Constants.FIBF_READ) == 0;

					if (!g.IgnoreProtectionBits && !hasReadProtectionBit)
					{
						throw new FileSystemException($"File '{filename}' does not have read protection bits set");
					}

					type.flags.access = Constants.ET_SHAREDREAD;
					break;

				// case ACTION_FINDUPDATE:
				case Amiga.FileSystems.FileMode.Append:
				case Amiga.FileSystems.FileMode.Write:
					if (overwrite || !found)
					{
						await Directory.NewFile(found, parentfi, filename, filefi, overwrite, g);
					}
					type.flags.access = Constants.ET_EXCLWRITE;
					break;
				
				default:
					throw new IOException("ERROR_ACTION_NOT_KNOWN");
			}
			
			/* Add file to list  */
			// if (!(filefe = MakeListEntry(&filefi, type, error, g)))
			// 	return DOSFALSE;
			IEntry fileFe;
			if ((fileFe = await Lock.MakeListEntry(filefi, type, g)) == null)
			{
				throw new IOException("make list entry error");
			}

			// 	if (!AddListEntry(filefe))
			// 	{
			// 		DB(Trace(1, "dd_Open", "AddListEntry failed"));
			// 		FreeListEntry(filefe, g);
			// 		*error = ERROR_OBJECT_IN_USE;
			// 		return DOSFALSE;
			// 	}
			if (!Lock.AddListEntry(fileFe.ListEntry, g))
			{
				//DB(Trace(1, "dd_Open", "AddListEntry failed"));
				Lock.FreeListEntry(fileFe, g);
				throw new IOException("ERROR_OBJECT_IN_USE");
			}

			/* if the file was created, the user has to be notified */
			// 	((fileentry_t *) filefe)->checknotify = !found;
			// 	filehandle->fh_Arg1 = (SIPTR)filefe;     // We get this with Read(), Write() etc
			if (fileFe is fileentry fileentry)
			{
				fileentry.checknotify = !found || isLink;
				
				if (isLink)
				{
					fileentry.originalsize = originalSize;
				}
			}
			
			return fileFe;
			
			// 	return DOSTRUE;
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