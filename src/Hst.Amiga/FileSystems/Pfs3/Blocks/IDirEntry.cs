namespace Hst.Amiga.FileSystems.Pfs3.Blocks
{
    using System;

    internal interface IDirEntry
    {
        string Name { get; }
        DateTime CreationDate { get; }
        uint Size { get; }
    }
}