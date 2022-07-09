namespace Hst.Amiga.FileSystems.FastFileSystem
{
    public interface IHeaderBlock
    {
        int Type { get; } // 0x000
        int HeaderKey { get; set; } // 0x004
        int HighSeq { get; set; } // 0x008
        int IndexSize { get; set; } // 0x00c: hashtable or datatable size
        int FirstData { get; set; } // 0x010: file header block
        int Checksum { get; set; } // 0x014
        int[] Index { get; set; }
        
        int RealEntry { get; set; } // 0x1d4
        int NextLink { get; set; } // 0x1d8
        int NextSameHash { get; set; } // 0x1f0
        int Parent { get; set; } // 0x1f4
        int Extension { get; set; } // 0x1f8
        int SecType { get; } // 0x1fc        
    }
}