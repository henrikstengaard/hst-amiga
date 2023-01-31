namespace Hst.Amiga.FileSystems.Pfs3.Blocks
{
    using System;

    public class direntry : IDirEntry
    {
        // struct direntry
        // {
        //     UBYTE next;             /* sizeof direntry                  */ 0
        //     BYTE  type;             /* dir, file, link etc              */ 1
        //     ULONG anode;            /* anode number                     */ 2
        //     ULONG fsize;            /* sizeof file                      */ 6
        //     UWORD creationday;      /* days since Jan. 1, 1978 (like ADOS; WORD instead of LONG) */ 10
        //     UWORD creationminute;   /* minutes past modnight            */ 12
        //     UWORD creationtick;     /* ticks past minute                */ 14
        //     UBYTE protection;       /* protection bits (like DOS)       */ 16
        //     UBYTE nlength;          /* lenght of filename               */ 17
        //     UBYTE startofname;      /* filename, followed by filenote length & filenote */ 18
        //     UBYTE pad;              /* make size even                   */ 19
        // };

        /// <summary>
        /// offset in dirblock entries
        /// </summary>
        //public int Offset { get; set; }
        /// <summary>
        /// Position of dir entry in dir block
        /// </summary>
        public int Position { get; set; }
        
        public readonly byte Next;             /* sizeof direntry                  */
        public sbyte type;             /* dir, file, link etc              */
        public uint anode;            /* anode number                     */
        public uint fsize { get; set; }           /* sizeof file                      */

        // public ushort creationday;      /* days since Jan. 1, 1978 (like ADOS; WORD instead of LONG) */
        // public ushort creationminute;   /* minutes past modnight            */
        // public ushort creationtick;     /* ticks past minute                */
        public byte protection;       /* protection bits (like DOS)       */
        //public byte nlength;          /* lenght of filename               */
        public string Name { get; set; }
        public static readonly byte StartOfName = 17;      /* filename, followed by filenote length & filenote */
        //public byte pad;              /* make size even                   */
        public string comment;
        
        /// <summary>
        /// dir entry extra fields, null if dir entry doesn't have extra fields
        /// </summary>
        public extrafields ExtraFields { get; set; }

        public uint Size
        {
            get => fsize;
            set => fsize = value;
        }
        
        public DateTime CreationDate { get; set; }

        public direntry()
        {
            Position = -1;
            Name = string.Empty;
            comment = string.Empty;
            ExtraFields = new extrafields();
        }

        public direntry(byte next) : this()
        {
            this.Next = next;
        }
        
        public static int EntrySize(string name, string comment, extrafields extraFields, globaldata g)
        {
            // entrysize = ((sizeof(struct direntry) + strlen(name)) & 0xfffe);
            var entrysize = (SizeOf.DirEntry.Struct + name.Length + comment.Length) & 0xfffe;
            if (g.dirextension)
            {
                entrysize += extrafields.ExtraFieldsSize(extraFields);
            }
            return entrysize;
        }

        public static int EntrySize(direntry dirEntry, globaldata g)
        {
            return EntrySize(dirEntry.Name, dirEntry.comment, dirEntry.ExtraFields, g);
        }
        
        public static void Copy(direntry src, direntry dest)
        {
            // dest.Offset = src.Offset;
            // dest.next = src.next;
            dest.Position = src.Position;
            dest.type = src.type;
            dest.anode = src.anode;
            dest.fsize = src.fsize;
            dest.protection = src.protection;
            dest.CreationDate = src.CreationDate;
            //dest.nlength = src.nlength;
            dest.Name = src.Name;
            dest.comment = src.comment;
            dest.ExtraFields = src.ExtraFields;
        }
    }
}