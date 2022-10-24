namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;

    public static class File
    {
        public static async Task<Stream> Open(Volume volume, Entry entry, FileMode mode = FileMode.Read)
        {
            var parentBlock = await Disk.ReadEntryBlock(volume, entry.Parent);
            return await Open(volume, parentBlock, entry.Name, mode);
        }

        public static async Task<Stream> Open(Volume volume, EntryBlock parent, string name, FileMode mode)
        {
            var write = mode == FileMode.Write || mode == FileMode.Append;

            if (!volume.Stream.CanWrite && write)
            {
                throw new IOException("device is mounted 'read only'");
            }

            var result = await Directory.GetEntryBlock(volume, parent.HashTable, name, false);
            var nSect = result.NSect;
            if (!write && nSect == uint.MaxValue)
            {
                if (!volume.IgnoreErrors)
                {
                    throw new IOException($"file \"{name}\" not found.");
                }
                volume.Logs.Add($"ERROR: File \"{name}\" not found.");
            }

            if (!write && result.EntryBlock == null)
            {
                if (!volume.IgnoreErrors)
                {
                    throw new IOException($"file \"{name}\" has no entry block.");
                }

                volume.Logs.Add($"ERROR: File \"{name}\" has no entry block.");
                return null;
            }

            var entry = result.EntryBlock;
            switch (write)
            {
                case false when Macro.hasR(entry.Access):
                    throw new IOException("access denied");
                case true when nSect != uint.MaxValue:
                    throw new IOException("file already exists");
            }

            var fileHdr = mode == FileMode.Write ? new FileHeaderBlock() : entry;

            var eof = mode == FileMode.Write || mode == FileMode.Append;

            if (mode == FileMode.Write)
            {
                    fileHdr = await Directory.CreateFile(volume, parent, name);
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
        public static async Task AdfFreeFileBlocks(Volume vol, EntryBlock entry)
        {
            // int i;
            // struct FileBlocks fileBlocks;
            // RETCODE rc = RC_OK;

            var fileBlocks = await AdfGetFileBlocks(vol, entry);

            for(var i=0; i < fileBlocks.nbData; i++)
            {
                Bitmap.AdfSetBlockFree(vol, fileBlocks.data[i]);
            }
            for(var i=0; i < fileBlocks.nbExtens; i++)
            {
                Bitmap.AdfSetBlockFree(vol, fileBlocks.extens[i]);
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
        public static async Task<FileBlocks> AdfGetFileBlocks(Volume vol, EntryBlock entry)
        {
            // int32_t n, m;
            // SECTNUM nSect;
            // struct bFileExtBlock extBlock;
            // int32_t i;
            var fileBlocks = new FileBlocks
            {
                header = entry.HeaderKey,
            };
            // adfFileRealSize( entry.ByteSize, vol.DataBlockSize, &(fileBlocks->nbData), &(fileBlocks->nbExtens) );
            AdfFileRealSize(entry.ByteSize, vol.DataBlockSize, fileBlocks);

            fileBlocks.data = new uint[fileBlocks.nbData];
            // fileBlocks->data=(SECTNUM*)malloc(fileBlocks->nbData * sizeof(SECTNUM));
            // if (!fileBlocks->data) {
            //     (*adfEnv.eFct)("adfGetFileBlocks : malloc");
            //     return RC_MALLOC;
            // }

            fileBlocks.extens= new uint[fileBlocks.nbExtens];
            // fileBlocks.extens=(SECTNUM*)malloc(fileBlocks->nbExtens * sizeof(SECTNUM));
            // if (!fileBlocks->extens) {
            //     (*adfEnv.eFct)("adfGetFileBlocks : malloc");
            //     return RC_MALLOC;
            // }

            var n = 0;
            var m = 0;	
            /* in file header block */
            for(var i=0; i < entry.HighSeq; i++)
                fileBlocks.data[n++] = entry.DataBlocks[Constants.MAX_DATABLK - 1 - i];

            /* in file extension blocks */
            var nSect = entry.Extension;
            while(nSect!=0)
            {
                fileBlocks.extens[m++] = nSect;
                var extBlock = await Disk.ReadFileExtBlock(vol, nSect);
                for(var i=0; i<extBlock.HighSeq; i++)
                    fileBlocks.data[n++] = extBlock.Index[Constants.MAX_DATABLK - 1 - i];
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
        public static uint AdfFileRealSize(uint size, uint blockSize, FileBlocks fileBlocks) // int32_t *dataN, int32_t *extN
        {
            // int32_t data, ext;

            /*--- number of data blocks ---*/
            var data = size / blockSize;
            if ( size % blockSize != 0)
                data++;

            /*--- number of header extension blocks ---*/
            var ext = 0U;
            if (data > Constants.MAX_DATABLK) {
                ext = (data - Constants.MAX_DATABLK) / Constants.MAX_DATABLK;
                if ((data - Constants.MAX_DATABLK) % Constants.MAX_DATABLK != 0)
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