namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;

    public static class File
    {
        public static async Task<Stream> Open(Volume volume, EntryBlock parent, string name, FileMode mode)
        {
            // https://github.com/lclevy/ADFlib/blob/be8a6f6e8d0ca8fda963803eef77366c7584649a/src/adf_file.c#L265

            var write = mode == FileMode.Write || mode == FileMode.Append;

            if (!volume.Stream.CanWrite && write)
            {
                throw new IOException("device is mounted 'read only'");
            }

            // adfReadEntryBlock(vol, vol->curDirPtr, &parent);
            // skipped as parent is provided as argument

            var result = await Directory.AdfNameToEntryBlk(volume, parent.HashTable, name, false);
            var nSect = result.NSect;
            if (!write && nSect == -1)
            {
                // sprintf(filename,"adfFileOpen : file \"%s\" not found.",name);
                // (*adfEnv.wFct)(filename);
                if (!volume.IgnoreErrors)
                {
                    throw new IOException($"file \"{name}\" not found.");
                }
                volume.Logs.Add($"ERROR: File \"{name}\" not found.");
/*fprintf(stdout,"filename %s %d, parent =%d\n",name,strlen(name),vol->curDirPtr);*/
                //return null; 
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
            if (!write && Macro.hasR(entry.Access))
            {
                throw new IOException("access denied");
            }

            // (*adfEnv.wFct)("adfFileOpen : access denied"); return NULL; }
/*    if (entry.secType!=ST_FILE) {
        (*adfEnv.wFct)("adfFileOpen : not a file"); return NULL; }
	if (write && (hasE(entry.access)||hasW(entry.access))) {
        (*adfEnv.wFct)("adfFileOpen : access denied"); return NULL; }  
*/
            if (write && nSect != -1)
            {
                //(*adfEnv.wFct)("adfFileOpen : file already exists"); return NULL;
                throw new IOException("file already exists");
            }

            // file = (struct File*)malloc(sizeof(struct File));
            // if (!file) { (*adfEnv.wFct)("adfFileOpen : malloc"); return NULL; }
            // file->fileHdr = (struct bFileHeaderBlock*)malloc(sizeof(struct bFileHeaderBlock));
            // if (!file->fileHdr) {
            //     (*adfEnv.wFct)("adfFileOpen : malloc"); 
            //     free(file); return NULL; 
            // }
            // file->currentData = malloc(512*sizeof(uint8_t));
            // if (!file->currentData) { 
            //     (*adfEnv.wFct)("adfFileOpen : malloc"); 
            //     free(file->fileHdr); free(file); return NULL; 
            // }
            var fileHdr = mode == FileMode.Write ? new EntryBlock() : EntryBlockReader.Parse(entry.BlockBytes);

            var eof = mode == FileMode.Write || mode == FileMode.Append;
            // switch (mode)
            // {
            //     case FileMode.Write:
            //         adfCreateFile(vol,vol->curDirPtr,name,file->fileHdr);
            //         eof = true;
            //         break;
            //     case FileMode.Append:
            //         eof = true;
            //         adfFileSeek(file, file->fileHdr->byteSize);
            //         break;
            // }

            //var entryStream = new EntryStream(volume, write, eof, fileHdr);

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
 * adfFileSeek
 *
 */


/*
 * adfReadDataBlock
 *
 */
        public static async Task<DataBlock> AdfReadDataBlock(Volume vol, int nSect)
        {
            // https://github.com/lclevy/ADFlib/blob/be8a6f6e8d0ca8fda963803eef77366c7584649a/src/adf_file.c#L654

            // var buf = new byte[512];
            // struct bOFSDataBlock *dBlock;
            // RETCODE rc = RC_OK;

            var buf = await Disk.ReadBlock(vol, nSect);

            // memcpy(data,buf,512);

            if (Macro.isOFS(vol.DosType))
            {
// #ifdef LITT_ENDIAN
//                 swapEndian(data, SWBL_DATA);
// #endif
//                 dBlock = (struct bOFSDataBlock*)data;
/*printf("adfReadDataBlock %ld\n",nSect);*/

                var dBlock = await DataBlockReader.Parse(buf);
                if (dBlock.Type != Constants.T_DATA)
                    throw new IOException("adfReadDataBlock : id T_DATA not found");
                if (dBlock.DataSize < 0 || dBlock.DataSize > 488)
                    throw new IOException("adfReadDataBlock : dataSize incorrect");
                if (!Disk.IsSectorNumberValid(vol, dBlock.HeaderKey))
                    throw new IOException("adfReadDataBlock : headerKey out of range");
                if (!Disk.IsSectorNumberValid(vol, dBlock.NextData))
                    throw new IOException("adfReadDataBlock : nextData out of range");

                return dBlock;
            }

            return new DataBlock
            {
                BlockBytes = buf,
                Data = buf
            };
        }
        
/*
 * adfWriteDataBlock
 *
 */
        public static async Task AdfWriteDataBlock(Volume vol, int nSect, DataBlock dataBlock)
        {
            var blockBytes = Macro.isOFS(vol.DosType)
                ? await DataBlockWriter.BuildBlock(dataBlock, vol.BlockSize)
                : dataBlock.Data;
            await Disk.WriteBlock(vol,nSect,blockBytes);
        }        

/*
 * adfReadFileExtBlock
 *
 */
        public static async Task<FileExtBlock> AdfReadFileExtBlock(Volume vol, int nSect)
        {
            // uint8_t buf[sizeof(struct bFileExtBlock)];
            // RETCODE rc = RC_OK;

            var buf = await Disk.ReadBlock(vol, nSect);
/*printf("read fext=%d\n",nSect);*/
//             memcpy(fext,buf,sizeof(struct bFileExtBlock));
// #ifdef LITT_ENDIAN
//             swapEndian((uint8_t*)fext, SWBL_FEXT);
// #endif
            var fext = await FileExtBlockReader.Parse(buf);
            //
            // if (fext.checkSum != Raw.AdfNormalSum(buf, 20, buf.Length))
            // {
            //     if (!vol.IgnoreErrors)
            //     {
            //         throw new IOException("adfReadFileExtBlock : invalid checksum");
            //     }
            //
            //     vol.Logs.Add($"ERROR: Sector '{nSect}', invalid checksum");
            // }

            // if (fext.type != Constants.T_LIST)
            // {
            //     if (!vol.IgnoreErrors)
            //     {
            //         throw new IOException("adfReadFileExtBlock : type T_LIST not found");
            //     }
            //     vol.Logs.Add($"ERROR: Sector '{nSect}', type T_LIST not found");
            // }
            //
            // if (fext.secType != Constants.ST_FILE)
            // {
            //     if (!vol.IgnoreErrors)
            //     {
            //         throw new IOException("adfReadFileExtBlock : stype  ST_FILE not found");
            //     }
            //     vol.Logs.Add($"ERROR: Sector '{nSect}', stype  ST_FILE not found");
            // }
            
            if (fext.HeaderKey != nSect)
                throw new IOException("adfReadFileExtBlock : headerKey!=nSect");
            if (fext.HighSeq < 0 || fext.HighSeq > Constants.MAX_DATABLK)
                throw new IOException("adfReadFileExtBlock : highSeq out of range");
            if (!Disk.IsSectorNumberValid(vol, fext.Parent))
                throw new IOException("adfReadFileExtBlock : parent out of range");
            if (fext.Extension != 0 && !Disk.IsSectorNumberValid(vol, fext.Extension))
                throw new IOException("adfReadFileExtBlock : extension out of range");

            return fext;
        }

// /*
//  * adfWriteFileHdrBlock
//  *
//  */
//         public static async Task AdfWriteFileHdrBlock(Volume vol, int nSect, EntryBlock fhdr)
//         {
//             // uint8_t buf[512];
//             // uint32_t newSum;
//             // RETCODE rc = RC_OK;
//
// /*printf("adfWriteFileHdrBlock %ld\n",nSect);*/
//             fhdr.Type = Constants.T_HEADER;
//             fhdr.DataSize = 0;
//             fhdr.SecType = Constants.ST_FILE;
//
// //             memcpy(buf, fhdr, sizeof(struct bFileHeaderBlock));
// // #ifdef LITT_ENDIAN
// //             swapEndian(buf, SWBL_FILE);
// // #endif
//             var buf = await FileHeaderBlockWriter.BuildBlock(fhdr, vol.BlockSize);
//             // var newSum = Raw.AdfNormalSum(buf, 20, buf.Length);
//             //swLong(buf+20, newSum);
//             // newSum applied part of build block
//
// /*    *(uint32_t*)(buf+20) = swapLong((uint8_t*)&newSum);*/
//
//             await Disk.AdfWriteBlock(vol, nSect, buf);
//         }
        
/*
 * adfWriteFileHdrBlock
 *
 */
        public static async Task AdfWriteFileHdrBlock(Volume vol, int nSect, EntryBlock fhdr)
        {
            // uint8_t buf[512];
            // uint32_t newSum;
            // RETCODE rc = RC_OK;

/*printf("adfWriteFileHdrBlock %ld\n",nSect);*/
            fhdr.Type = Constants.T_HEADER;
            //fhdr.DataSize = 0;
            fhdr.SecType = Constants.ST_FILE;

//             memcpy(buf, fhdr, sizeof(struct bFileHeaderBlock));
// #ifdef LITT_ENDIAN
//             swapEndian(buf, SWBL_FILE);
// #endif
            var buf = EntryBlockWriter.BuildBlock(fhdr, vol.BlockSize);
            // var newSum = Raw.AdfNormalSum(buf, 20, buf.Length);
            //swLong(buf+20, newSum);
            // newSum applied part of build block

/*    *(uint32_t*)(buf+20) = swapLong((uint8_t*)&newSum);*/

            await Disk.WriteBlock(vol, nSect, buf);
        }
        
/*
 * adfWriteFileExtBlock
 *
 */
        public static async Task AdfWriteFileExtBlock(Volume vol, int nSect, FileExtBlock fileExtBlock)
        {
            var blockBytes = await FileExtBlockWriter.BuildBlock(fileExtBlock, vol.BlockSize);
            await Disk.WriteBlock(vol, nSect, blockBytes);
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
            AdfFileRealSize((uint)entry.ByteSize, vol.DataBlockSize, fileBlocks);

            fileBlocks.data = new int[fileBlocks.nbData];
            // fileBlocks->data=(SECTNUM*)malloc(fileBlocks->nbData * sizeof(SECTNUM));
            // if (!fileBlocks->data) {
            //     (*adfEnv.eFct)("adfGetFileBlocks : malloc");
            //     return RC_MALLOC;
            // }

            fileBlocks.extens= new int[fileBlocks.nbExtens];
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
                var extBlock = await File.AdfReadFileExtBlock(vol, nSect);
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
        public static int AdfFileRealSize(uint size, int blockSize, FileBlocks fileBlocks) // int32_t *dataN, int32_t *extN
        {
            // int32_t data, ext;

            /*--- number of data blocks ---*/
            var data = (int)(size / blockSize);
            if ( size % blockSize != 0)
                data++;

            /*--- number of header extension blocks ---*/
            var ext = 0;
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