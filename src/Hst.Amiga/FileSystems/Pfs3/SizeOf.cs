namespace Hst.Amiga.FileSystems.Pfs3
{
    public static class SizeOf
    {
        public const int INDEXBLOCK_T = 2 * Amiga.SizeOf.UWord + 2 * Amiga.SizeOf.ULong;
        public const int ANODEBLOCK_T = 2 * Amiga.SizeOf.UWord + 3 * Amiga.SizeOf.ULong;
        public const int ANODE_T = 3 * Amiga.SizeOf.ULong;

        public static class RootBlock
        {
            public static int IdxUnion => Constants.MAXSMALLBITMAPINDEX + 1 + Constants.MAXSMALLINDEXNR + 1;
        }

        public static class DirBlock
        {
            public static int Struct(globaldata g) => Amiga.SizeOf.UWord * 4 + Amiga.SizeOf.ULong * 3 + Entries(g);

            public static int Entries(globaldata g) =>
                g.RootBlock.ReservedBlksize - Amiga.SizeOf.UWord * 4 - Amiga.SizeOf.ULong * 3;
        }

        public static class DelDirBlock
        {
            public const int Entry = Amiga.SizeOf.ULong * 2 + Amiga.SizeOf.UWord * 3 + 16 + Amiga.SizeOf.UWord;

            public static int Entries(globaldata g) =>
                (g.RootBlock.ReservedBlksize - Amiga.SizeOf.UWord * 2 - Amiga.SizeOf.ULong * 2 -
                 Amiga.SizeOf.UWord * 4 -
                 Amiga.SizeOf.ULong - Amiga.SizeOf.UWord * 3) / Entry;
        }

        public static class DirEntry
        {
            public static int Struct => Amiga.SizeOf.UByte + Amiga.SizeOf.Byte + (Amiga.SizeOf.ULong * 2) +
                                        (Amiga.SizeOf.UWord * 3) + (Amiga.SizeOf.UByte * 4);
        }

        public static class FileInfo
        {
            public static int Struct => DirEntry.Struct;
        }

        // public static class LockEntry
        // {
        //     public static int Struct => Amiga.SizeOf.ULong * 3 + ListEntry.Struct + FileInfo.Struct;
        // }
        //
        // public static class FileEntry
        // {
        //     public static int Struct => ListEntry.Struct + Amiga.SizeOf.ULong * 4 + Amiga.SizeOf.Bool;
        // }

        public static class ExtraFields
        {
            public static int Struct => Amiga.SizeOf.ULong + (Amiga.SizeOf.UWord * 2) +
                                        (Amiga.SizeOf.ULong * 3) + Amiga.SizeOf.UWord;
        }
    }
}