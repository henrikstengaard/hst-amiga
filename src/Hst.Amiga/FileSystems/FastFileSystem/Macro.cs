namespace Hst.Amiga.FileSystems.FastFileSystem
{
    public static class Macro
    {
        public static bool isFFS(int c) => (c& Constants.FSMASK_FFS) != 0;
        public static bool isOFS(int c) => (c & Constants.FSMASK_FFS) == 0;
        public static bool isINTL(int c) => (c & Constants.FSMASK_INTL) != 0;

        public static bool hasD(uint c) => (c & Constants.ACCMASK_D) != 0;
        public static bool hasE(uint c) => (c & Constants.ACCMASK_E) != 0;
        public static bool hasW(uint c) => (c&Constants.ACCMASK_W) != 0;
        public static bool hasR(uint c) => (c&Constants.ACCMASK_R) != 0;
        public static bool hasA(uint c) => (c&Constants.ACCMASK_A) != 0;
        public static bool hasP(uint c) => (c&Constants.ACCMASK_P) != 0;
        public static bool hasS(uint c) => (c&Constants.ACCMASK_S) != 0;
        public static bool hasH(uint c) => (c&Constants.ACCMASK_H) != 0;
    }
}