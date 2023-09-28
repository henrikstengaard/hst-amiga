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
                ProtectionBits = ProtectionBitsConverter.ToProtectionBits(dirEntry.protection),
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
    }
}