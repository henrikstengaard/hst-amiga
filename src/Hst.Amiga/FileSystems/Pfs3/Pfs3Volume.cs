﻿namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using RigidDiskBlocks;

    public class Pfs3Volume : IAsyncDisposable, IDisposable
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
            
            if ((await Directory.Find(currentDirectory, path, g)).Any())
            {
                throw new IOException("Not found");
            }
            
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
        /// Open file for reading or writing data
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="write"></param>
        /// <returns></returns>
        public async Task<EntryStream> OpenFile(string fileName, bool write)
        {
            var objectInfo = currentDirectory.Clone();
            if ((await Directory.Find(objectInfo, fileName, g)).Any())
            {
                throw new IOException("Not found");
            }
            var fileEntry = await File.Open(objectInfo, write, g) as fileentry;
            return new EntryStream(fileEntry, g);
        }

        /// <summary>
        /// Delete file or directory from current directory
        /// </summary>
        /// <param name="name"></param>
        public async Task Delete(string name)
        {
            var objectInfo = currentDirectory.Clone();
            if ((await Directory.Find(objectInfo, name, g)).Any())
            {
                throw new IOException("Not found");
            }
            await Directory.DeleteObject(objectInfo, g);
        }

        /// <summary>
        /// Rename or move a file or directory
        /// </summary>
        /// <param name="oldName">Old name</param>
        /// <param name="newName">New name</param>
        /// <exception cref="IOException"></exception>
        public async Task Rename(string oldName, string newName)
        {
            var srcInfo = currentDirectory.Clone();
            if ((await Directory.Find(srcInfo, oldName, g)).Any())
            {
                throw new IOException("Not found");
            }

            var destInfo = currentDirectory.Clone();
            var remainingParts = await Directory.Find(destInfo, newName, g);

            if (remainingParts.Length == 0)
            {
                throw new IOException("Exists");
            }

            if (remainingParts.Length > 1)
            {
                throw new IOException($"Directory '{remainingParts[0]}' not found");
            }
            
            await Directory.RenameAndMove(currentDirectory, srcInfo, destInfo, remainingParts[0], g);
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

        public void Dispose()
        {
            Pfs3Helper.Unmount(g).GetAwaiter().GetResult();

            GC.SuppressFinalize(this);
        }
    }
}