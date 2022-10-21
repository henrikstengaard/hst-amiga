namespace Hst.Amiga.FileSystems.Pfs3.Blocks
{
    using System;

    public class deldirentry : IDirEntry
    {
        // struct deldirentry
        // {
        //     ULONG anodenr;			/* anodenr							*/
        //     ULONG fsize;			/* size of file						*/
        //     UWORD creationday;		/* datestamp						*/
        //     UWORD creationminute;
        //     UWORD creationtick;
        //     UBYTE filename[16];		/* filename; filling up to 30 chars	*/
        //     // was previously filename[18]
        //     // now last two bytes used for extended file size
        //     UWORD fsizex;			/* extended bits 32-47 of fsize		*/
        // };
        
        /// <summary>
        /// offset in deldirblock entries
        /// </summary>
        public int Offset { get; set; }
        
        public uint anodenr;			/* anodenr							*/
        public uint fsize;			/* size of file						*/
        /* datestamp						*/

        public string filename;		/* filename; filling up to 30 chars	*/
        // was previously filename[18]
        // now last two bytes used for extended file size
        public ushort fsizex;			/* extended bits 32-47 of fsize		*/

        public string Name
        {
            get => filename;
            set => filename = value;
        }
        public DateTime CreationDate { get; set; }

        public uint Size
        {
            get => fsize;
            set => fsize = value;
        }
        
        public deldirentry()
        {
            CreationDate = DateTime.MinValue;
            filename = string.Empty;
        }
    }
}