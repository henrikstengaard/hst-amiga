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
        public byte startofname;      /* filename, followed by filenote length & filenote */
        public byte pad;              /* make size even                   */
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

        public static int EntrySize(direntry direntry, globaldata g)
        {
            // entrysize = ((sizeof(struct direntry) + strlen(name)) & 0xfffe);
            var entrysize = (SizeOf.DirEntry.Struct + direntry.Name.Length + direntry.comment.Length) & 0xfffe;
            if (g.dirextension)
            {
                entrysize += extrafields.ExtraFieldsSize(direntry.ExtraFields);
            }
            return entrysize;
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