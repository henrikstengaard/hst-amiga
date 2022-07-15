namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System;

    public abstract class EntryBlock : IBlock, IHeaderBlock, IEntryBlock
    {
        public uint Offset { get; set; }
        public byte[] BlockBytes { get; set; }
        
        public abstract int Type { get; } // 0x000
        public int HeaderKey { get; set; } // 0x004
        public int HighSeq { get; set; } // 0x008

        public int DataSize { get => IndexSize; set => IndexSize = value; } // 0x00c: file header block
        public int HashTableSize { get => IndexSize; set => IndexSize = value; } // 00c: root block, dir block
        
        public int FirstData { get; set; } // 0x010: file header block
        
        public int Checksum { get; set; } // 0x014

        public int IndexSize { get; set; }
        public int[] Index { get; set; }
        
        /// <summary>
        /// hash table used root block and dir block. offset 0x018
        /// </summary>
        public int[] HashTable { get => Index; set => Index = value; }
        
        /// <summary>
        /// data blocks used by file header blocks, offset 0x018
        /// </summary>
        public int[] DataBlocks { get => Index; set => Index = value; }
        
        public int Access { get; set; } // 0x140: file header block
        public int ByteSize { get; set; } // 0x144: file header block
        public string Comment { get; set; } // 0x148: length, 0x149: comment
        public DateTime Date { get; set; } // 0x1a4: days, 0x1a8: mins, 0x1ac: ticks
        public string Name { get; set; } // 0x1b0: length, 0x1b1: name
        public int RealEntry { get; set; } // 0x1d4
        public int NextLink { get; set; } // 0x1d8
        public int NextSameHash { get; set; } // 0x1f0
        public int Parent { get; set; } // 0x1f4
        public int Extension { get; set; } // 0x1f8
        public abstract int SecType { get;} // 0x1fc

        protected EntryBlock()
        {
            IndexSize = Constants.INDEX_SIZE; // HT_SIZE, MAX_DATABLK
            Index = new int[Constants.INDEX_SIZE];

            Comment = string.Empty;
            Name = string.Empty;
        }
    }
}