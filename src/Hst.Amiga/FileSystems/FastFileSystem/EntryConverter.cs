namespace Hst.Amiga.FileSystems.FastFileSystem
{
    public static class EntryConverter
    {
        public static Hst.Amiga.FileSystems.Entry ToEntry(Entry entry)
        {
            return new Hst.Amiga.FileSystems.Entry
            {
                Name = entry.Name,
                Type = GetEntryType(entry.Type),
                Size = entry.Size,
                ProtectionBits = GetProtectionBits(entry.Access),
                Date = entry.Date,
                Comment = entry.Comment
            };
        }

        public static EntryType GetEntryType(int type)
        {
            switch (type)
            {
                case Constants.ST_DIR:
                    return EntryType.Dir;
                case Constants.ST_FILE:
                    return EntryType.File;
                case Constants.ST_LSOFT:
                    return EntryType.SoftLink;
                case Constants.ST_LDIR:
                    return EntryType.DirLink;
                case Constants.ST_LFILE:
                    return EntryType.FileLink;
                default:
                    return EntryType.File;
            }
        }

        public static ProtectionBits GetProtectionBits(uint access)
        {
            var protectionBits = ProtectionBits.Read | ProtectionBits.Write | ProtectionBits.Executable | ProtectionBits.Delete;

            // add held resident flag, if bit is present
            if ((access & Constants.ACCMASK_H) == Constants.ACCMASK_H)
            {
                protectionBits |= ProtectionBits.HeldResident;
            }
            
            // add script flag, if bit is present
            if ((access & Constants.ACCMASK_S) == Constants.ACCMASK_S)
            {
                protectionBits |= ProtectionBits.Script;
            }
            
            // add pure flag, if bit is present
            if ((access & Constants.ACCMASK_P) == Constants.ACCMASK_P)
            {
                protectionBits |= ProtectionBits.Pure;
            }

            // add archive flag, if bit is present
            if ((access & Constants.ACCMASK_A) == Constants.ACCMASK_A)
            {
                protectionBits |= ProtectionBits.Archive;
            }

            // remove read flag, if bit is present
            if ((access & Constants.ACCMASK_R) == Constants.ACCMASK_R)
            {
                protectionBits &= ~ProtectionBits.Read;
            }

            // remove write flag, if bit is present
            if ((access & Constants.ACCMASK_W) == Constants.ACCMASK_W)
            {
                protectionBits &= ~ProtectionBits.Write;
            }

            // remove executable flag, if bit is present
            if ((access & Constants.ACCMASK_E) == Constants.ACCMASK_E)
            {
                protectionBits &= ~ProtectionBits.Executable;
            }
            
            // remove delete flag, if bit is present
            if ((access & Constants.ACCMASK_D) == Constants.ACCMASK_D)
            {
                protectionBits &= ~ProtectionBits.Delete;
            }
            
            return protectionBits;
        }

        public static uint GetAccess(ProtectionBits protectionBits)
        {
            uint access = 0;

            if (protectionBits.HasFlag(ProtectionBits.HeldResident))
            {
                access |= Constants.ACCMASK_H;
            }
            
            if (protectionBits.HasFlag(ProtectionBits.Script))
            {
                access |= Constants.ACCMASK_S;
            }
            
            if (protectionBits.HasFlag(ProtectionBits.Pure))
            {
                access |= Constants.ACCMASK_P;
            }
            
            if (protectionBits.HasFlag(ProtectionBits.Archive))
            {
                access |= Constants.ACCMASK_A;
            }
            
            if (!protectionBits.HasFlag(ProtectionBits.Read))
            {
                access |= Constants.ACCMASK_R;
            }

            if (!protectionBits.HasFlag(ProtectionBits.Write))
            {
                access |= Constants.ACCMASK_W;
            }
            
            if (!protectionBits.HasFlag(ProtectionBits.Executable))
            {
                access |= Constants.ACCMASK_E;
            }
            
            if (!protectionBits.HasFlag(ProtectionBits.Delete))
            {
                access |= Constants.ACCMASK_D;
            }

            return access;
        }
    }
}