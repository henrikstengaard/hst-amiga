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
                ProtectionBits = ProtectionBitsConverter.ToProtectionBits((int)entry.Access),
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
    }
}