namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Blocks;

    public class EntryStream : Stream
    {
        private readonly Volume volume;

        private readonly uint length;

        private bool eof;
        private readonly EntryBlock fileHdr;

        private uint pos;
        private uint posInExtBlk;
        private uint posInDataBlk;
        private uint curDataPtr;
        private FileExtBlock currentExt;
        private uint nDataBlock;
        private DataBlock currentData;
        private readonly bool writeMode;

        public EntryStream(Volume volume, bool writeMode, bool eof, EntryBlock entryBlock)
        {
            this.length = entryBlock.ByteSize;
            this.volume = volume;
            this.writeMode = writeMode;
            this.eof = eof;
            this.fileHdr = entryBlock;
            this.pos = 0;
            this.posInExtBlk = 0;
            this.posInDataBlk = 0;
            this.currentData = new DataBlock
            {
                Data = new byte[volume.DataBlockSize]
            };
        }

        protected override void Dispose(bool disposing)
        {
            AdfCloseFile().GetAwaiter().GetResult();
            base.Dispose(disposing);
        }

        public override void Flush()
        {
            AdfFlushFile().GetAwaiter().GetResult();
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await AdfFlushFile();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (offset != 0)
            {
                throw new ArgumentException("Only offset 0 is supported", nameof(offset));
            }

            return AdfReadFile(count, buffer).GetAwaiter().GetResult();
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            if (offset != 0)
            {
                throw new ArgumentException("Only offset 0 is supported", nameof(offset));
            }

            return await AdfReadFile(count, buffer);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            AdfFileSeek((uint)offset).GetAwaiter().GetResult();
            return this.pos;
        }

        public override void SetLength(long value)
        {
            throw new IOException(
                "Set length not supported for entries. Use write buffer instead with buffer set to length");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (offset != 0)
            {
                throw new ArgumentException("Only offset 0 is supported", nameof(offset));
            }

            AdfWriteFile(count, buffer).GetAwaiter().GetResult();
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (offset != 0)
            {
                throw new ArgumentException("Only offset 0 is supported", nameof(offset));
            }

            await AdfWriteFile(count, buffer);
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => writeMode;
        public override long Length => length;
        public override long Position
        {
            get => this.pos;
            set => Seek(value, SeekOrigin.Begin);
        }

        private async Task AdfFileSeek(uint position)
        {
            int i;

            var nPos = Math.Min(position, length);
            this.pos = nPos;
            var extBlock = Pos2DataBlock(nPos);
            if (extBlock == uint.MaxValue)
            {
                currentData =
                    await Disk.ReadDataBlock(volume, fileHdr.DataBlocks[volume.IndexSize - 1 - curDataPtr]);
            }
            else
            {
                var nSect = fileHdr.Extension;
                i = 0;
                while (i < extBlock && nSect != 0)
                {
                    currentExt = await Disk.ReadFileExtBlock(volume, nSect);
                    nSect = currentExt.Extension;
                }

                if (i != extBlock)
                {
                    throw new IOException("error");
                }

                currentData = await Disk.ReadDataBlock(volume, currentExt.Index[posInExtBlk]);
            }
        }

        /*
        * adfPos2DataBlock
            *
            */
        private uint Pos2DataBlock(uint position) //, int *posInExtBlk, int *posInDataBlk, int32_t *curDataN )
        {
            posInDataBlk = (uint)(position % volume.FileSystemBlockSize);
            curDataPtr = (uint)(position / volume.FileSystemBlockSize);
            if (posInDataBlk == 0)
                curDataPtr++;
            if (curDataPtr < 72)
            {
                posInExtBlk = 0;
                return uint.MaxValue;
            }

            posInExtBlk = (uint)((position - 72 * volume.FileSystemBlockSize) % volume.FileSystemBlockSize);
            var extBlock = (uint)((position - 72 * volume.FileSystemBlockSize) / volume.FileSystemBlockSize);
            if (posInExtBlk == 0)
                extBlock++;
            return extBlock;
        }

/*
 * adfReadFile
 *
 */
        private async Task<int> AdfReadFile(int n, byte[] buffer)
        {
            // https://github.com/lclevy/ADFlib/blob/be8a6f6e8d0ca8fda963803eef77366c7584649a/src/adf_file.c#L369

            //uint8_t *dataPtr, *bufPtr;

            if (n > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(n),
                    $"Count '{n}' is larger than buffer size '{buffer.Length}'");
            }

            if (n == 0)
                return n;
            var blockSize = volume.DataBlockSize;
/*puts("adfReadFile");*/
            if (pos + n > fileHdr.ByteSize)
                n = (int)(fileHdr.ByteSize - pos);


            if (pos == 0 || posInDataBlk == blockSize)
            {
                await AdfReadNextFileBlock();
                posInDataBlk = 0;
            }

            var dataPtr = currentData.Data;

            // if (Macro.isOFS(volume.DosType))
            // {
            //     var ofsCurrentData = new byte[currentData.BlockBytes.Length - 24];
            //     Array.Copy(currentData.BlockBytes);
            //     dataPtr = (uint8_t*)(file->currentData)+24;
            // }
            // else
            // {
            //     dataPtr = currentData.BlockBytes;
            // }

            var bytesRead = 0;
            var bufPtr = 0;
            while (bytesRead < n)
            {
                var size = (int)Math.Min(n - bytesRead, blockSize - posInDataBlk);

                Array.Copy(dataPtr, posInDataBlk, buffer, bufPtr, size);
                bufPtr += size;
                pos += (uint)size;
                bytesRead += size;
                posInDataBlk += (uint)size;
                if (posInDataBlk == blockSize && bytesRead < n)
                {
                    await AdfReadNextFileBlock();
                    posInDataBlk = 0;
                }
            }

            eof = pos == fileHdr.ByteSize;
            return bytesRead;
        }

/*
 * adfReadNextFileBlock
 *
 */
        public async Task AdfReadNextFileBlock()
        {
            uint nSect;
            // struct bOFSDataBlock *data;
            // RETCODE rc = RC_OK;

            // data =(struct bOFSDataBlock *) currentData;
            var data = currentData;
            //
            // if (data == null)
            // {
            //     throw new IOException("currentData is not OFSDataBlock");
            // }

            if (nDataBlock == 0)
            {
                nSect = fileHdr.FirstData;
            }
            else if (volume.UseOfs)
            {
                nSect = data.NextData;
            }
            else
            {
                if (nDataBlock < volume.IndexSize)
                    nSect = fileHdr.DataBlocks[volume.IndexSize - 1 - nDataBlock];
                else
                {
                    if (nDataBlock == volume.IndexSize)
                    {
                        currentExt = await Disk.ReadFileExtBlock(volume, fileHdr.Extension);
                        posInExtBlk = 0;
                    }
                    else if (posInExtBlk == volume.IndexSize)
                    {
                        currentExt = await Disk.ReadFileExtBlock(volume, currentExt.Extension);
                        posInExtBlk = 0;
                    }

                    nSect = currentExt.Index[volume.IndexSize - 1 - posInExtBlk];
                    posInExtBlk++;
                }
            }

            currentData = await Disk.ReadDataBlock(volume, nSect);
            data = currentData;

            if (volume.UseOfs && data.SeqNum != nDataBlock + 1)
            {
                throw new IOException("adfReadNextFileBlock : seqnum incorrect");
            }

            nDataBlock++;
        }

/*
 * adfWriteFile
 *
 */
        public async Task<int> AdfWriteFile(int n, byte[] buffer)
        {
            if (n == 0)
            {
                return n;
            }
            var blockSize = volume.DataBlockSize;
            var dataPtr = currentData.Data;

            if (pos == 0 || posInDataBlk == blockSize)
            {
                if (await AdfCreateNextFileBlock() == uint.MaxValue)
                {
                    /* bug found by Rikard */
                    throw new IOException("adfWritefile : no more free sector availbale");
                }

                posInDataBlk = 0;
            }

            var bytesWritten = 0;
            var bufPtr = 0;
            while (bytesWritten < n)
            {
                var size = (int)Math.Min(n - bytesWritten, blockSize - posInDataBlk);

                Array.Copy(buffer, bufPtr, dataPtr, posInDataBlk, size);

                bufPtr += size;
                pos += (uint)size;
                bytesWritten += size;
                posInDataBlk += (uint)size;
                if (posInDataBlk == blockSize && bytesWritten < n)
                {
                    if (await AdfCreateNextFileBlock() == uint.MaxValue)
                    {
                        /* bug found by Rikard */
                        throw new IOException("adfWritefile : no more free sector available");
                    }

                    posInDataBlk = 0;
                }
            }

            return bytesWritten;
        }

        public async Task<uint> AdfCreateNextFileBlock()
        {
            uint nSect;
            var blockSize = volume.DataBlockSize;

            /* the first data blocks pointers are inside the file header block */
            if (nDataBlock < volume.IndexSize)
            {
                nSect = Bitmap.AdfGet1FreeBlock(volume);
                if (nSect == uint.MaxValue)
                {
                    return uint.MaxValue;
                }

                if (nDataBlock == 0)
                {
                    fileHdr.FirstData = nSect;
                }
                fileHdr.DataBlocks[volume.IndexSize - 1 - nDataBlock] = nSect;
                fileHdr.HighSeq++;
            }
            else
            {
                /* one more sector is needed for one file extension block */
                if (nDataBlock % volume.IndexSize == 0)
                {
                    var extSect = Bitmap.AdfGet1FreeBlock(volume);
                    if (extSect == uint.MaxValue)
                    {
                        return uint.MaxValue;
                    }

                    /* the future block is the first file extension block */
                    if (nDataBlock == volume.IndexSize)
                    {
                        currentExt = new FileExtBlock(volume.FileSystemBlockSize);
                        fileHdr.Extension = extSect;
                    }

                    /* not the first : save the current one, and link it with the future */
                    if (nDataBlock >= 2 * volume.IndexSize)
                    {
                        currentExt.Extension = extSect;
                        await Disk.WriteFileExtBlock(volume, currentExt.HeaderKey, currentExt);
                    }

                    /* initializes a file extension block */
                    for (var i = 0; i < volume.IndexSize; i++)
                        currentExt.Index[i] = 0;
                    currentExt.HeaderKey = extSect;
                    currentExt.Parent = fileHdr.HeaderKey;
                    currentExt.HighSeq = 0;
                    currentExt.Extension = 0;
                    posInExtBlk = 0;
                }

                nSect = Bitmap.AdfGet1FreeBlock(volume);
                if (nSect == uint.MaxValue)
                {
                    return uint.MaxValue;
                }

                currentExt.Index[volume.IndexSize - 1 - posInExtBlk] = nSect;
                currentExt.HighSeq++;
                posInExtBlk++;
            }

            /* builds OFS header */
            if (volume.UseOfs)
            {
                var data = currentData;
                /* writes previous data block and link it  */
                if (pos >= blockSize)
                {
                    data.NextData = nSect;
                    await Disk.WriteDataBlock(volume, curDataPtr, currentData);
                }

                /* initialize a new data block */
                for (var i = 0; i < blockSize; i++)
                    data.Data[i] = 0;
                data.SeqNum = nDataBlock + 1;
                data.DataSize = blockSize;
                data.NextData = 0;
                data.HeaderKey = fileHdr.HeaderKey;
            }
            else if (pos >= blockSize)
            {
                await Disk.WriteDataBlock(volume, curDataPtr, currentData);
            }

            curDataPtr = nSect;
            nDataBlock++;

            return nSect;
        }
        
        public async Task AdfCloseFile()
        {
            await AdfFlushFile();
        }
        
        public async Task AdfFlushFile()
        {
            if (currentExt != null) 
            {
                if (writeMode)
                {
                    await Disk.WriteFileExtBlock(volume, currentExt.HeaderKey, currentExt);
                }
            }
            if (currentData != null)
            {
                if (writeMode)
                {
                    fileHdr.ByteSize = pos;
                    if (volume.UseOfs)
                    {
                        //var data = currentData as OfsDataBlock;
                        currentData.DataSize = posInDataBlk;
                    }

                    if (fileHdr.ByteSize > 0)
                    {
                        await Disk.WriteDataBlock(volume, curDataPtr, currentData);
                    }
                }
            }
            if (writeMode)
            {
                fileHdr.ByteSize = pos;
                fileHdr.Date = DateTime.Now;
                await Disk.WriteFileHdrBlock(volume, fileHdr.HeaderKey, fileHdr as FileHeaderBlock);

                if (volume.UseDirCache) 
                {
                    var parent = await Disk.ReadEntryBlock(volume, fileHdr.Parent);
                    await Cache.UpdateCache(volume, parent, fileHdr, true);
                }
                await Bitmap.AdfUpdateBitmap(volume);
            }
        }        
    }
}