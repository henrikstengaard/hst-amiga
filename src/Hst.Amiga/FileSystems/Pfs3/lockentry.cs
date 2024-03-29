﻿namespace Hst.Amiga.FileSystems.Pfs3
{
    public class lockentry : IEntry
    {
        // typedef struct lockentry
        // {
        //     listentry_t le;
        //
        //     ULONG               nextanode;          // anodenr of next entry (dir/vollock only)
        //     struct fileinfo     nextentry;          // for examine
        //     ULONG               nextdirblocknr;     // for flushed block only.. (dir/vollock only)
        //     ULONG               nextdirblockoffset;
        // } lockentry_t;
        public listentry le;

        public uint nextanode; // anodenr of next entry (dir/vollock only)
        public fileinfo nextentry; // for examine
        public uint nextdirblocknr; // for flushed block only.. (dir/vollock only)
        public uint nextdirblockoffset;

        public ListType type => le.type;
        public listentry ListEntry => le;
        
        public lockentry LockEntry => this;
    }
}