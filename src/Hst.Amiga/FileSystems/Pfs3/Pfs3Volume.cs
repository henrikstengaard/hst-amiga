namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using RigidDiskBlocks;

    public class Pfs3Volume : IAsyncDisposable
    {
        public readonly globaldata g;
        
        public Pfs3Volume(globaldata g)
        {
            this.g = g;
        }

        public async ValueTask DisposeAsync()
        {
            await Pfs3Helper.Unmount(g);
            
            // Dispose methods should call SuppressFinalize
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        public static async Task<Pfs3Volume> Mount(Stream stream, PartitionBlock partitionBlock)
        {
            return new Pfs3Volume(await Pfs3Helper.Mount(stream, partitionBlock));
        }
    }
}