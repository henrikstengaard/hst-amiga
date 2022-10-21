namespace Hst.Amiga.FileSystems
{
    public enum EntryType
    {
        /// <summary>
        /// Directory
        /// </summary>
        Dir,
        /// <summary>
        /// File
        /// </summary>
        File,
        /// <summary>
        /// Hard link to directory
        /// </summary>
        DirLink,
        /// <summary>
        /// Hard link to file
        /// </summary>
        FileLink,
        /// <summary>
        /// Soft link to file or directory
        /// </summary>
        SoftLink
    }
}