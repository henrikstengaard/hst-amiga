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
                CreationDate = dirEntry.CreationDate
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
            if ((protection & 128) == 128)
            {
                protectionBits |= ProtectionBits.HeldResident;
            }

            // add script flag, if bit is present
            if ((protection & 64) == 64)
            {
                protectionBits |= ProtectionBits.Script;
            }

            // add pure flag, if bit is present
            if ((protection & 32) == 32)
            {
                protectionBits |= ProtectionBits.Pure;
            }

            // add archive flag, if bit is present
            if ((protection & 16) == 16)
            {
                protectionBits |= ProtectionBits.Archive;
            }
            
            // remove read flag, if bit is present
            if ((protection & 8) == 8)
            {
                protectionBits &= ~ProtectionBits.Read;
            }
            
            // remove write flag, if bit is present
            if ((protection & 4) == 4)
            {
                protectionBits &= ~ProtectionBits.Write;
            }
            
            // remove executable flag, if bit is present
            if ((protection & 2) == 2)
            {
                protectionBits &= ~ProtectionBits.Executable;
            }

            // remove delete flag, if bit is present
            if ((protection & 1) == 1)
            {
                protectionBits &= ~ProtectionBits.Delete;
            }
            
            return protectionBits;
        }
    }
}