namespace Hst.Amiga.FileSystems.Pfs3.Blocks
{
    using System;

    public interface IDirEntry
    {
        string Name { get; set; }
        DateTime CreationDate { get; set; }
        uint Size { get; set; }
    }
}