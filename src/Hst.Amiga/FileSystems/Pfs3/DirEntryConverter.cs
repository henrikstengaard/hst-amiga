namespace Hst.Amiga.FileSystems.Pfs3
{
    using Blocks;

    public static class DirEntryConverter
    {
        public static Entry ToEntry(direntry dirEntry)
        {
            return new Entry
            {
                Name = dirEntry.Name,
                Type = GetEntryType(dirEntry.type),
                Size = dirEntry.fsize,
                ProtectionBits = GetProtectionBits(dirEntry.protection),
                Date = dirEntry.CreationDate,
                Comment = dirEntry.comment
            };
        }

        public static EntryType GetEntryType(int type)
        {
            switch (type)
            {
                case Constants.ST_USERDIR:
                    return EntryType.Dir;
                case Constants.ST_FILE:
                    return EntryType.File;
                case Constants.ST_SOFTLINK:
                    return EntryType.SoftLink;
                case Constants.ST_LINKDIR:
                    return EntryType.DirLink;
                case Constants.ST_LINKFILE:
                    return EntryType.FileLink;
                default:
                    return EntryType.File;
            }
        }

        public static ProtectionBits GetProtectionBits(int protection)
        {
            var protectionBits = ProtectionBits.Read | ProtectionBits.Write | ProtectionBits.Executable | ProtectionBits.Delete;

            // add held resident flag, if bit is present
            if ((protection & Constants.FIBF_HELDRESIDENT) == Constants.FIBF_HELDRESIDENT)
            {
                protectionBits |= ProtectionBits.HeldResident;
            }

            // add script flag, if bit is present
            if ((protection & Constants.FIBF_SCRIPT) == Constants.FIBF_SCRIPT)
            {
                protectionBits |= ProtectionBits.Script;
            }

            // add pure flag, if bit is present
            if ((protection & Constants.FIBF_PURE) == Constants.FIBF_PURE)
            {
                protectionBits |= ProtectionBits.Pure;
            }

            // add archive flag, if bit is present
            if ((protection & Constants.FIBF_ARCHIVE) == Constants.FIBF_ARCHIVE)
            {
                protectionBits |= ProtectionBits.Archive;
            }
            
            // remove read flag, if bit is present
            if ((protection & Constants.FIBF_READ) == Constants.FIBF_READ)
            {
                protectionBits &= ~ProtectionBits.Read;
            }
            
            // remove write flag, if bit is present
            if ((protection & Constants.FIBF_WRITE) == Constants.FIBF_WRITE)
            {
                protectionBits &= ~ProtectionBits.Write;
            }
            
            // remove executable flag, if bit is present
            if ((protection & Constants.FIBF_EXECUTE) == Constants.FIBF_EXECUTE)
            {
                protectionBits &= ~ProtectionBits.Executable;
            }

            // remove delete flag, if bit is present
            if ((protection & Constants.FIBF_DELETE) == Constants.FIBF_DELETE)
            {
                protectionBits &= ~ProtectionBits.Delete;
            }
            
            return protectionBits;
        }

        public static byte GetProtection(ProtectionBits protectionBits)
        {
            byte protection = 0;

            if (protectionBits.HasFlag(ProtectionBits.HeldResident))
            {
                protection |= Constants.FIBF_HELDRESIDENT;
            }
            
            if (protectionBits.HasFlag(ProtectionBits.Script))
            {
                protection |= Constants.FIBF_SCRIPT;
            }
            
            if (protectionBits.HasFlag(ProtectionBits.Pure))
            {
                protection |= Constants.FIBF_PURE;
            }
            
            if (protectionBits.HasFlag(ProtectionBits.Archive))
            {
                protection |= Constants.FIBF_ARCHIVE;
            }
            
            if (!protectionBits.HasFlag(ProtectionBits.Read))
            {
                protection |= Constants.FIBF_READ;
            }

            if (!protectionBits.HasFlag(ProtectionBits.Write))
            {
                protection |= Constants.FIBF_WRITE;
            }
            
            if (!protectionBits.HasFlag(ProtectionBits.Executable))
            {
                protection |= Constants.FIBF_EXECUTE;
            }
            
            if (!protectionBits.HasFlag(ProtectionBits.Delete))
            {
                protection |= Constants.FIBF_DELETE;
            }

            return protection;
        }
    }
}