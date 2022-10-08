namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;

    public class fileentry : IEntry
    {
/*
 * // de specifieke structuren
        typedef struct
        {
            listentry_t le;
	
            struct anodechain *anodechain;      // the cached anodechain of this file
            struct anodechainnode *currnode;    // anode behorende bij offset in file
            ULONG   anodeoffset;        // blocknr binnen currentanode
            ULONG   blockoffset;        // byteoffset binnen huidig block
            FSIZE   offset;             // offset tov start of file
            FSIZE   originalsize;       // size of file at time of opening
            BOOL    checknotify;        // set if a notify is necessary at ACTION_END time > ALSO touch flag <
        } fileentry_t;
 */
        public listentry le;
        public anodechain anodechain; // the cached anodechain of this file
        public anodechainnode currnode; // anode behorende bij offset in file
        public uint anodeoffset; // blocknr binnen currentanode
        public uint blockoffset; // byteoffset binnen huidig block
        public uint offset; // offset tov start of file
        public uint originalsize; // size of file at time of opening
        public bool checknotify; // set if a notify is necessary at ACTION_END time > ALSO touch flag <
        public listentry ListEntry => le;
        public lockentry LockEntry => throw new NotImplementedException();
    }
}