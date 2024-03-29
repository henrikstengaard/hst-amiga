﻿namespace Hst.Amiga.FileSystems.Pfs3.Blocks
{
    public class anodeblock : IBlock
    {
        // typedef struct anodeblock
        // {
        //     UWORD id;               /* AB                               */
        //     UWORD not_used;
        //     ULONG datestamp;
        //     ULONG seqnr;
        //     ULONG not_used_2;
        //     struct anode nodes[0];
        // } anodeblock_t;
        
        public byte[] BlockBytes { get; set; }
        
        public ushort id { get; set; }
        public ushort not_used_1 { get; set; }
        public uint datestamp { get; set; }
        public uint seqnr;
        public uint not_used_2;
        public anode[] nodes;

        public anodeblock(globaldata g)
        {
            id = Constants.ABLKID; /* AB                               */
            nodes = new anode[(g.RootBlock.ReservedBlksize - Amiga.SizeOf.UWord * 2 - Amiga.SizeOf.ULong * 3) / anode.Size];
            for (var i = 0; i < nodes.Length; i++)
            {
                nodes[i] = new anode();
            }
        }
    }
}