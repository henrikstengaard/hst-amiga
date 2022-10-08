namespace Hst.Amiga.FileSystems.Pfs3
{
    public class listentry : IEntry
    {
        // /* de algemene structure */
        //         typedef struct listentry
        //         {
        //             struct listentry    *next;          /* for linkage                                      */
        //             struct listentry    *prev;
        //             struct FileLock     lock;           /* <4A> contains accesstype, dirblocknr (redundant) */
        //             listtype            type;
        //             ULONG               anodenr;        /* object anodenr. Always valid. Used by ACTION_SLEEP */
        //             ULONG               diranodenr;     /* anodenr of parent. Only valid during SLEEP_MODE. */
        //             union objectinfo    info;           /* refers to dir                                    */
        //             ULONG               dirblocknr;     /* set when block flushed and info is set to NULL   */
        //             ULONG               dirblockoffset;
        //             struct volumedata   *volume;        /* pointer to volume                                */
        //         } listentry_t;        
        public listentry next; /* for linkage                                      */
        public listentry prev;
        public FileLock filelock; /* <4A> contains accesstype, dirblocknr (redundant) */
        public ListType type { get; set; }
        public uint anodenr; /* object anodenr. Always valid. Used by ACTION_SLEEP */
        public uint diranodenr; /* anodenr of parent. Only valid during SLEEP_MODE. */
        public objectinfo info; /* refers to dir                                    */
        public uint dirblocknr; /* set when block flushed and info is set to NULL   */
        public uint dirblockoffset;
        public volumedata volume; /* pointer to volume                                */
        
        public listentry ListEntry => this;

        public lockentry LockEntry =>
            new lockentry
            {
                le = this,
                nextentry = new fileinfo
                {
                    
                }
            };

        public listentry()
        {
            filelock = new FileLock();
        }
    }
}