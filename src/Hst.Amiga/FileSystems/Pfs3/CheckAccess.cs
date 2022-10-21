namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.IO;

    public static class CheckAccess
    {
/* fileaccess that changes a file but doesn't write to it
 */
        public static void CheckChangeAccess(fileentry file, globaldata g)
        {
            // #if DELDIR
            /* delfiles cannot be altered */
            if (Macro.IsDelFile(file.le.info))
            {
                throw new IOException("ERROR_WRITE_PROTECTED");
            }
            // #endif

            /* test on type */
            if (!Macro.IsFile(file.le.info)) 
            {
                throw new IOException("ERROR_OBJECT_WRONG_TYPE");
            }
	
            /* volume must be or become currentvolume */
            Volume.CheckVolume(file.le.volume, true, g);

            /* check reserved area lock */
            if (Macro.ReservedAreaIsLocked(g))
            {
                throw new IOException("ERROR_DISK_FULL");
            }
        }
        
/* fileaccess that reads from a file
 */
        public static void CheckReadAccess(fileentry file, globaldata g)
        {
            /* Test on read-protection, type and volume */
            // #if DELDIR
	        if (!Macro.IsDelFile(file.le.info))
	        {
                // #endif
                if (!Macro.IsFile(file.le.info)) 
                {
                    throw new IOException("ERROR_OBJECT_WRONG_TYPE");
                }

                if ((file.le.info.file.direntry.protection & Constants.FIBF_READ) == Constants.FIBF_READ)
                {
                    throw new IOException("ERROR_READ_PROTECTED");
                }
                // #if DELDIR
	        }
            // #endif

            Volume.CheckVolume(file.le.volume, false, g);
        }
        
/* fileaccess that writes to a file
 */
        public static void CheckWriteAccess(fileentry file, globaldata g)
        {
            CheckChangeAccess(file, g);

            if ((file.le.info.file.direntry.protection & Constants.FIBF_WRITE) == Constants.FIBF_WRITE)
            {
                throw new IOException("ERROR_WRITE_PROTECTED");
            }
        }
        
/* check on operate access (like Seek)
 */
        public static void CheckOperateFile(fileentry file, globaldata g)
        {
            /* test on type */
            // #if DELDIR
            if (!Macro.IsDelFile(file.le.info) && !Macro.IsFile(file.le.info))
            // #else
            //             if (!IsFile(file->le.info)) 
            // #endif
            {
                throw new IOException("ERROR_OBJECT_WRONG_TYPE");
            }

            /* volume must be or become currentvolume */
            Volume.CheckVolume(file.le.volume, false, g);
        }
    }
}