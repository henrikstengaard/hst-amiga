namespace Hst.Amiga.DataTypes.UaeFsDbs
{
    public class UaeFsDbNode
    {
        // from winuae fsdb_win32.cpp:
        /* The on-disk format is as follows:
         * Offset 0, 1 byte, valid
         * Offset 1, 4 bytes, mode
         * Offset 5, 257 bytes, aname
         * Offset 262, 257 bytes, nname
         * Offset 519, 81 bytes, comment
         * Offset 600, 4 bytes, Windows-side mode
         *
         * 1.6.0+ Unicode data
         *
         * Offset  604, 257 * 2 bytes, aname
         * Offset 1118, 257 * 2 bytes, nname
         *        1632
         */

        public enum NodeVersion
        {
            Version1,
            Version2
        }

        public NodeVersion Version { get; set; } = NodeVersion.Version1;
        public byte Valid { get; set; }
        public uint Mode { get; set; }
        public string AmigaName { get; set; }
        public string NormalName { get; set; }
        public string Comment { get; set; }
        public uint WinMode { get; set; }
        public string AmigaNameUnicode { get; set; }
        public string NormalNameUnicode { get; set; }

        public UaeFsDbNode()
        {
            Valid = 1;
        }
    }
}