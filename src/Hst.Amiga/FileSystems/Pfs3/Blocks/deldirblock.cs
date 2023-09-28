namespace Hst.Amiga.FileSystems.Pfs3.Blocks
{
    using System;

    public class deldirblock : ISeqBlock
    {
        // struct deldirblock
        // {
        //     UWORD id;				/* 'DD'								*/
        //     UWORD not_used;
        //     ULONG datestamp;
        //     ULONG seqnr;
        //     UWORD not_used_2[2];
        //     UWORD not_used_3;		/* roving in older versions	(<17.9)	*/	
        //     UWORD uid;				/* user id							*/
        //     UWORD gid;				/* group id							*/
        //     ULONG protection;
        //     UWORD creationday;
        //     UWORD creationminute;
        //     UWORD creationtick;
        //     struct deldirentry entries[0];	/* 31 entries				*/
        // };

        public byte[] BlockBytes { get; set; }

        public ushort id { get; set; } // 0x0
        public ushort not_used_1 { get; set; } // 0x2
        public uint datestamp { get; set; } // 0x4
        public uint seqnr { get; set; } // 0x8
        public ushort uid { get; set; } // 0xc
        public ushort gid { get; set; } // 0xe
        public uint protection { get; set; } // 0x10
        public DateTime CreationDate { get; set; } // 0x14
        public deldirentry[] entries { get; set; } // 0x20

        public deldirblock(globaldata g)
        {
            id = Constants.DELDIRID;
            entries = new deldirentry[SizeOf.DelDirBlock.Entries(g)];
            for (var i = 0; i < entries.Length; i++)
            {
                entries[i] = new deldirentry();
            }
        }
    }
}