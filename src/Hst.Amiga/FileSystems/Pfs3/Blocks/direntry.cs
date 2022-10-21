namespace Hst.Amiga.FileSystems.Pfs3.Blocks
{
    using System;

    public class direntry : IDirEntry
    {
        // struct direntry
        // {
        //     UBYTE next;             /* sizeof direntry                  */
        //     BYTE  type;             /* dir, file, link etc              */
        //     ULONG anode;            /* anode number                     */
        //     ULONG fsize;            /* sizeof file                      */
        //     UWORD creationday;      /* days since Jan. 1, 1978 (like ADOS; WORD instead of LONG) */
        //     UWORD creationminute;   /* minutes past modnight            */
        //     UWORD creationtick;     /* ticks past minute                */
        //     UBYTE protection;       /* protection bits (like DOS)       */
        //     UBYTE nlength;          /* lenght of filename               */
        //     UBYTE startofname;      /* filename, followed by filenote length & filenote */
        //     UBYTE pad;              /* make size even                   */
        // };

        /// <summary>
        /// offset in dirblock entries
        /// </summary>
        public int Offset { get; set; }
        
        public byte next;             /* sizeof direntry                  */
        public sbyte type;             /* dir, file, link etc              */
        public uint anode;            /* anode number                     */
        public uint fsize { get; set; }           /* sizeof file                      */

        // public ushort creationday;      /* days since Jan. 1, 1978 (like ADOS; WORD instead of LONG) */
        // public ushort creationminute;   /* minutes past modnight            */
        // public ushort creationtick;     /* ticks past minute                */
        public byte protection;       /* protection bits (like DOS)       */
        public byte nlength;          /* lenght of filename               */
        public string Name { get; set; }
        public byte startofname;      /* filename, followed by filenote length & filenote */
        public byte pad;              /* make size even                   */
        public string comment;
        
        public extrafields ExtraFields { get; set; }

        public uint Size
        {
            get => fsize;
            set => fsize = value;
        }
        public DateTime CreationDate { get; set; }
    }
}