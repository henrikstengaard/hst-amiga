namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using RigidDiskBlocks;

    public class Pfs3Volume : IAsyncDisposable
    {
        public readonly globaldata g;
        private objectinfo currentDirectory;
        private uint dirNodeNr;

        public Pfs3Volume(globaldata g, objectinfo currentDirectory, uint dirNodeNr)
        {
            this.g = g;
            this.currentDirectory = currentDirectory;
            this.dirNodeNr = dirNodeNr;
        }

        public async ValueTask DisposeAsync()
        {
            await Pfs3Helper.Unmount(g);

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Name of volume
        /// </summary>
        public string Name => g.RootBlock.DiskName;
        
        /// <summary>
        /// Size of volume in bytes
        /// </summary>
        public long Size => (long)g.RootBlock.DiskSize * g.blocksize;
        
        /// <summary>
        /// Free volume disk space in bytes
        /// </summary>
        public long Free => (long)g.RootBlock.BlocksFree * g.blocksize;

        /// <summary>
        /// List entries in current directory
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<Entry>> ListEntries()
        {
            return (await Directory.GetDirEntries(dirNodeNr, g)).Select(DirEntryConverter.ToEntry).ToList();
        }

        /// <summary>
        /// Change current directory
        /// </summary>
        /// <param name="path">Relative or absolute path.</param>
        public async Task ChangeDirectory(string path)
        {
            if (path.StartsWith("/") && !Macro.IsRoot(currentDirectory))
            {
                currentDirectory = await Directory.GetRoot(g);
            }
            
            await Directory.Find(currentDirectory, path, g);
            dirNodeNr = Macro.IsRoot(currentDirectory) ? (uint)Macro.ANODE_ROOTDIR : currentDirectory.file.direntry.anode;
        }
        
        /// <summary>
        /// Create directory in current directory
        /// </summary>
        /// <param name="dirName"></param>
        public async Task CreateDirectory(string dirName)
        {
            await Directory.NewDir(currentDirectory, dirName, g);
        }

        /// <summary>
        /// Create file in current directory
        /// </summary>
        /// <param name="fileName"></param>
        public async Task CreateFile(string fileName)
        {
            var notUsed = new objectinfo();
            await Directory.NewFile(false, currentDirectory, fileName, notUsed, g);
        }
        
        /// <summary>
        /// Mount pfs3 volume in stream using partition block information
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="partitionBlock"></param>
        /// <returns></returns>
        public static async Task<Pfs3Volume> Mount(Stream stream, PartitionBlock partitionBlock)
        {
            var g = await Pfs3Helper.Mount(stream, partitionBlock);
            
            var root = await Directory.GetRoot(g);
            var dirNodeNr = (uint)Macro.ANODE_ROOTDIR;
            
            return new Pfs3Volume(g, root, dirNodeNr);
        }
    }
}