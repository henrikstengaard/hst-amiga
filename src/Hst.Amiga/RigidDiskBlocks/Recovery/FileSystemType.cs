namespace Hst.Amiga.RigidDiskBlocks.Recovery
{
    public enum FileSystemType
    {
        /// <summary>
        /// Fast file system or old file system (DOS\0 - DOS\7)
        /// </summary>
        FastFileSystem,

        /// <summary>
        /// Professional file system 3 (PFS\1, PFS\2)
        /// </summary>
        ProfessionalFileSystem3,

        /// <summary>
        /// Smart file system (SFS\0)
        /// </summary>
        SmartFileSystem
    }
}
