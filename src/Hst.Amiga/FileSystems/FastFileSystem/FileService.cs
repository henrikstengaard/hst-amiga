namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;
    using Exceptions;
    using FileMode = FileMode;

    /// <summary>
    /// Opens files and gets and frees file blocks of a volume.
    /// </summary>
    internal class FileService
    {
        private readonly Volume volume;

        public FileService(Volume volume)
        {
            this.volume = volume;
        }

        public async Task<Stream> Open(Entry entry, FileMode mode = FileMode.Read)
        {
            return await Open(entry.Parent, entry.Name, mode);
        }

        public async Task<Stream> Open(uint parentSector, string name, FileMode mode,
            bool overwrite = false, bool ignoreProtectionBits = false)
        {
            var parent = await volume.BlockStore.ReadEntryBlock(parentSector);
            
            var write = mode == FileMode.Write || mode == FileMode.Append;

            if (!volume.Stream.CanWrite && write)
            {
                throw new FileSystemException("Device is mounted read only");
            }

            var result = await volume.Directories.GetEntryBlock(parentSector, parent.HashTable, name);
            var nSect = result.NSect;
            if (!write && nSect == uint.MaxValue)
            {
                if (!volume.IgnoreErrors)
                {
                    throw new PathNotFoundException($"Path \"{name}\" not found");
                }
                volume.Logs.Add($"ERROR: File \"{name}\" not found.");
            }

            var entry = result.EntryBlock;
            
            if (entry != null && entry.SecType == Constants.ST_LFILE)
            {
                nSect = entry.RealEntry;
                entry = await volume.BlockStore.ReadEntryBlock(entry.RealEntry);
            }
            
            // free entry file blocks, if write and overwrite existing entry
            if (write && overwrite && nSect != uint.MaxValue)
            {
                await FreeFileBlocks(entry);
            }
            
            if (!write && entry == null)
            {
                if (!volume.IgnoreErrors)
                {
                    throw new PathNotFoundException($"Path \"{name}\" not found");
                }

                volume.Logs.Add($"ERROR: File \"{name}\" has no entry block.");
                return null;
            }

            if (overwrite && entry != null && entry.SecType != Constants.ST_FILE && entry.SecType != Constants.ST_LFILE)
            {
                throw new NotAFileException($"Entry '{name}' has secondary type {entry.SecType} and is not a file or link file");
            }
            
            switch (mode)
            {
                case FileMode.Read when Macro.hasR(entry.Access):
                    if (!ignoreProtectionBits)
                    {
                        throw new FileSystemException($"File '{name}' does not have read protection bits set");
                    }
                    break;
                case FileMode.Write when !overwrite && nSect != uint.MaxValue:
                    throw new PathAlreadyExistsException($"Path '{name}' already exists");
            }

            var fileHdr = mode == FileMode.Write
                ? new EntryBlock(volume.FileSystemBlockSize) {  SecType = Constants.ST_FILE }
                : entry;

            var eof = mode == FileMode.Write || mode == FileMode.Append;

            if (mode == FileMode.Write)
            {
                fileHdr = overwrite && nSect != uint.MaxValue ? entry : await volume.Directories.CreateFile(parent, name);
                if (overwrite)
                {
                    // reset existing file header block for overwriting
                    fileHdr.HighSeq = 0;
                    fileHdr.ByteSize = 0;
                    fileHdr.Date = DateTime.Now;
                    fileHdr.Access = 0;
                    fileHdr.Comment = string.Empty;
                    fileHdr.FirstData = 0;
                    fileHdr.Extension = 0;
                    for (var i = 0; i < fileHdr.Index.Length; i++)
                    {
                        fileHdr.Index[i] = 0;
                    }
                }
                eof = true;
            }

            var entryStream = new EntryStream(volume, write, eof, fileHdr);

            if (mode == FileMode.Append)
            {
                entryStream.Seek(entry.ByteSize, SeekOrigin.Begin);
            }

            return entryStream;
        }
        
/*
 * adfFreeFileBlocks
 *
 */
        public async Task FreeFileBlocks(EntryBlock entry)
        {
            // int i;
            // struct FileBlocks fileBlocks;
            // RETCODE rc = RC_OK;

            var fileBlocks = await GetFileBlocks(entry);

            for(var i=0; i < fileBlocks.nbData; i++)
            {
                volume.Allocator.SetBlockFree(fileBlocks.data[i]);
            }
            for(var i=0; i < fileBlocks.nbExtens; i++)
            {
                volume.Allocator.SetBlockFree(fileBlocks.extens[i]);
            }
            
            if (entry.Extension != 0)
            {
                volume.Allocator.SetBlockFree(entry.Extension);
            }

            // free(fileBlocks.data);
            // free(fileBlocks.extens);
		          //
            // return rc;
        }   
        
/*
 * adfGetFileBlocks
 *
 */
        public async Task<FileBlocks> GetFileBlocks(EntryBlock entry)
        {
            // int32_t n, m;
            // SECTNUM nSect;
            // struct bFileExtBlock extBlock;
            // int32_t i;
            var fileBlocks = new FileBlocks
            {
                header = entry.HeaderKey,
            };
            // adfFileRealSize( entry.ByteSize, volume.DataBlockSize, &(fileBlocks->nbData), &(fileBlocks->nbExtens) );
            CalculateFileBlockCounts(entry.ByteSize, volume.DataBlockSize, fileBlocks);

            fileBlocks.data = new uint[fileBlocks.nbData];
            // fileBlocks->data=(SECTNUM*)malloc(fileBlocks->nbData * sizeof(SECTNUM));
            // if (!fileBlocks->data) {
            //     (*adfEnv.eFct)("adfGetFileBlocks : malloc");
            //     return RC_MALLOC;
            // }

            fileBlocks.extens = new uint[fileBlocks.nbExtens];
            // fileBlocks.extens=(SECTNUM*)malloc(fileBlocks->nbExtens * sizeof(SECTNUM));
            // if (!fileBlocks->extens) {
            //     (*adfEnv.eFct)("adfGetFileBlocks : malloc");
            //     return RC_MALLOC;
            // }

            var n = 0;
            var m = 0;	
            /* in file header block */
            for(var i=0; i < entry.HighSeq; i++)
                fileBlocks.data[n++] = entry.DataBlocks[volume.IndexSize - 1 - i];

            /* in file extension blocks */
            var nSect = entry.Extension;
            while(nSect!=0)
            {
                fileBlocks.extens[m++] = nSect;
                var extBlock = await volume.BlockStore.ReadFileExtBlock(nSect);
                for(var i=0; i<extBlock.HighSeq; i++)
                    fileBlocks.data[n++] = extBlock.Index[volume.IndexSize - 1 - i];
                nSect = extBlock.Extension;
            }
            if (fileBlocks.nbExtens + fileBlocks.nbData != n + m)
                throw new IOException("adfGetFileBlocks : less blocks than expected");

            return fileBlocks;
        }   
        
        /*
 * adfFileRealSize
 *
 * Compute and return real number of block used by one file
 * Compute number of datablocks and file extension blocks
 *
 */
        public uint CalculateFileBlockCounts(uint size, uint blockSize, FileBlocks fileBlocks) // int32_t *dataN, int32_t *extN
        {
            // int32_t data, ext;

            /*--- number of data blocks ---*/
            var data = size / blockSize;
            if ( size % blockSize != 0)
                data++;

            /*--- number of header extension blocks ---*/
            var ext = 0U;
            if (data > volume.IndexSize) {
                ext = (data - volume.IndexSize) / volume.IndexSize;
                if ((data - volume.IndexSize) % volume.IndexSize != 0)
                {
                    ext++;
                }
            }

            if (fileBlocks != null)
            {
                fileBlocks.nbData = data;
                fileBlocks.nbExtens = ext;
            }
            
            return ext + data + 1;
        }
    }
}