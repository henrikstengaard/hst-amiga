namespace Hst.Amiga.FileSystems.Pfs3
{
    public class ListType
    {
        // union listtype
        // {
        // 	struct
        // 	{
        // 		unsigned pad:11;
        // 		unsigned dir:1;     // 0 = file; 1 = dir or volume
        // 		unsigned type:2;    // 0 = unknown; 3 = lock; 1 = volume; 2 = fileentry
        // 		unsigned access:2;  // 0 = read shared; 2 = read excl; 1,3 = write shared, excl
        // 	} flags;
        //
        // 	UWORD value;
        // };
        
        // acess   10
        public class ListTypeFlags
        {
            public int pad { get; set; }
            public int dir { get; set; }
            public int type { get; set; }
            public int access { get; set; }
        }

        public ListTypeFlags flags;

        public ushort value
        {
            get => (ushort)(flags.dir << 4 | flags.type << 2 | flags.access);
            set
            {
                flags.dir = (value & 16) >> 4;
                flags.type = (value & 12) >> 2;
                flags.access = value & 3;
            }
        }

        public ListType()
        {
            flags = new ListTypeFlags();
        }

        // public enum ListTypeDir
        // {
        //     File = 0,
        //     Dir = 1
        // }

        // public enum ListTypeAccess
        // {
        //     // unsigned access:2;  // 0 = read shared; 2 = read excl; 1,3 = write shared, excl
        //     ReadShared,
        //     WriteShared,
        //     ReadExcl,
        //     WriteExcl
        // }

        // public enum ListTypeType
        // {
        //     // unsigned type:2;    // 0 = unknown; 3 = lock; 1 = volume; 2 = fileentry
        //     Unknown,
        //     Volume,
        //     FileEntry,
        //     Lock
        // }
    }
}