namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    public interface IHeaderBlock
    {
        int Type { get; } // 0x000
        uint HeaderKey { get; set; } // 0x004
        uint HighSeq { get; set; } // 0x008
        uint IndexSize { get; set; } // 0x00c: hashtable or datatable size
        uint FirstData { get; set; } // 0x010: file header block
        int Checksum { get; set; } // 0x014
        uint[] Index { get; set; }
        
        uint RealEntry { get; set; } // 0x1d4
        uint NextLink { get; set; } // 0x1d8
        uint NextSameHash { get; set; } // 0x1f0
        uint Parent { get; set; } // 0x1f4
        uint Extension { get; set; } // 0x1f8
        int SecType { get; } // 0x1fc        
    }
}