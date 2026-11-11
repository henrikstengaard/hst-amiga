namespace Hst.Amiga.FileSystems.SmartFileSystem
{
    using Blocks;

    public static class EntryConverter
    {
        /// <summary>
        /// Convert smart file system object to entry.
        /// </summary>
        /// <param name="sfsObject">Object to convert.</param>
        /// <param name="linkPath">Path of soft link, if object is a soft link.</param>
        public static Entry ToEntry(SfsObject sfsObject, string linkPath = null)
        {
            return new Entry
            {
                Name = sfsObject.Name,
                Type = GetEntryType(sfsObject),
                Size = sfsObject.IsFile ? sfsObject.Size : 0,
                Date = sfsObject.DateModified,
                ProtectionBits = ProtectionHelper.ToProtectionBits(sfsObject.Protection),
                Comment = sfsObject.Comment ?? string.Empty,
                LinkPath = sfsObject.IsSoftLink ? linkPath : null
            };
        }

        public static EntryType GetEntryType(SfsObject sfsObject)
        {
            if (sfsObject.IsDir)
            {
                return EntryType.Dir;
            }

            return sfsObject.IsSoftLink ? EntryType.SoftLink : EntryType.File;
        }
    }
}
