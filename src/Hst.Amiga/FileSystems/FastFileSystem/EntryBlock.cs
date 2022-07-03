namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;

    public class EntryBlock
    {
        /*
        struct bEntryBlock {
        // 000	int32_t	type;		// T_HEADER == 2
        // 004	int32_t	headerKey;	// current block number 
                    int32_t	r1[3];
        // 014	uint32_t	checkSum;
        // 018	int32_t	hashTable[HT_SIZE];
                    int32_t	r2[2];
        // 140	int32_t	access;	// bit0=del, 1=modif, 2=write, 3=read
        // 144	int32_t	byteSize;
        // 148	char	commLen;
        // 149	char	comment[MAXCMMTLEN+1];
                    char	r3[91-(MAXCMMTLEN+1)];
        // 1a4	int32_t	days;
        // 1a8	int32_t	mins;
        // 1ac	int32_t	ticks;
        // 1b0	char	nameLen;
        // 1b1	char	name[MAXNAMELEN+1];
                    int32_t	r4;
        // 1d4	int32_t	realEntry;
        // 1d8	int32_t	nextLink;
                    int32_t	r5[5];
        // 1f0	int32_t	nextSameHash;
        // 1f4	int32_t	parent;
        // 1f8	int32_t	extension;
        // 1fc	int32_t	secType;
        };
         */
        public byte[] BlockBytes { get; set; }
        
        public int Type { get; set; } // 0x000
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
        public int SecType { get; set; } // 0x1fc

        public EntryBlock()
        {
            IndexSize = Constants.INDEX_SIZE; // HT_SIZE, MAX_DATABLK
            Index = new int[Constants.INDEX_SIZE];

            Comment = string.Empty;
            Name = string.Empty;
        }

        public static EntryBlock CreateDirBlock()
        {
            return new EntryBlock
            {
                Type = Constants.T_HEADER,
                SecType = Constants.ST_DIR
            };
        }

        public static EntryBlock CreateFileHeaderBlock()
        {
            return new EntryBlock
            {
                Type = Constants.T_HEADER,
                SecType = Constants.ST_FILE
            };
        }
    }
}