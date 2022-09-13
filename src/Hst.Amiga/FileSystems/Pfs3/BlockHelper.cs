namespace Hst.Amiga.FileSystems.Pfs3
{
    public class BlockHelper
    {
        

        // #define AllocBufmem(size,g) ((g->allocbufmem)(size,g))

        private static void AllocBufmemR(int size, globaldata g)
        {
            // ULONG *buffer;
            //
            // while (!(buffer = AllocBufmem (size, g)))
            //     OutOfMemory (g);
            //
            // return buffer;
            g.allocbufmem = new long[size];
        }



    }
}