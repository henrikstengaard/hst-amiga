namespace Hst.Amiga.FileSystems.Pfs3
{
    public interface IEntry
    {
        listentry ListEntry { get; }
        //lockentry LockEntry { get; }
        
        // //#define fe ((fileentry_t *)listentry)
        
        // IEntry - how to convert between these?
        // - listentry
        // - lockentry
        // - fileentry
        
        /*
typedef struct listentry
{
	struct listentry    *next;          // for linkage                                      
        struct listentry    *prev;
        struct FileLock     lock;           // <4A> contains accesstype, dirblocknr (redundant) 
        listtype            type;
        ULONG               anodenr;        // object anodenr. Always valid. Used by ACTION_SLEEP 
        ULONG               diranodenr;     // anodenr of parent. Only valid during SLEEP_MODE. 
        union objectinfo    info;           // refers to dir                                    
        ULONG               dirblocknr;     // set when block flushed and info is set to NULL   
        ULONG               dirblockoffset;
        struct volumedata   *volume;        // pointer to volume                                
    } listentry_t;
    */
        
        // typedef struct lockentry
        // {
        //     listentry_t le;
        //
        //     ULONG               nextanode;          // anodenr of next entry (dir/vollock only)
        //     struct fileinfo     nextentry;          // for examine
        //     ULONG               nextdirblocknr;     // for flushed block only.. (dir/vollock only)
        //     ULONG               nextdirblockoffset;
        // } lockentry_t;
        
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
        
    }
}