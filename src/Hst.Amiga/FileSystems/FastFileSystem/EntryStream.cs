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
            return AdfReadFile(buffer, offset, count).GetAwaiter().GetResult();
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            return await AdfReadFile(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long position;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    position = offset;
                    break;
                case SeekOrigin.Current:
                    position = pos + offset;
                    break;
                case SeekOrigin.End:
                    position = length + offset;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(origin));
            }

            if (position < 0)
            {
                throw new IOException("An attempt was made to move the position before the beginning of the stream");
            }

            if (position == pos)
            {
                return pos;
            }

            if (writeMode)
            {
                throw new NotSupportedException("Seek is not supported for entry stream in write mode");
            }

            AdfFileSeek((uint)Math.Min(position, length)).GetAwaiter().GetResult();
            return this.pos;
        }

        public override void SetLength(long value)
        {
            throw new IOException(
                "Set length not supported for entries. Use write buffer instead with buffer set to length");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            AdfWriteFile(buffer, offset, count).GetAwaiter().GetResult();
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await AdfWriteFile(buffer, offset, count);
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
            var blockSize = volume.DataBlockSize;

            pos = position;
            eof = pos == length;

            if (position == 0)
            {
                // next read starts from first data block
                nDataBlock = 0;
                posInDataBlk = 0;
                posInExtBlk = 0;
                return;
            }

            // position at end of data block is represented by previous data block fully read,
            // so next read continues with next data block
            var dataBlockIndex = position / blockSize;
            posInDataBlk = position % blockSize;
            if (posInDataBlk == 0)
            {
                dataBlockIndex--;
                posInDataBlk = blockSize;
            }

            var dataBlockSector = await GetDataBlockSector(dataBlockIndex);
            currentData = await Disk.ReadDataBlock(volume, dataBlockSector);

            if (volume.UseOfs && currentData.SeqNum != dataBlockIndex + 1)
            {
                throw new IOException("adfFileSeek : seqnum incorrect");
            }

            nDataBlock = dataBlockIndex + 1;
        }

        /// <summary>
        /// Get sector for data block index and update current file ext block and position in file ext block
        /// to same state as after reading data block with AdfReadNextFileBlock
        /// </summary>
        private async Task<uint> GetDataBlockSector(uint dataBlockIndex)
        {
            uint dataBlockSector;
            if (dataBlockIndex < volume.IndexSize)
            {
                dataBlockSector = fileHdr.DataBlocks[volume.IndexSize - 1 - dataBlockIndex];
            }
            else
            {
                var extBlockIndex = (dataBlockIndex - volume.IndexSize) / volume.IndexSize;
                var indexInExtBlk = (dataBlockIndex - volume.IndexSize) % volume.IndexSize;

                var nSect = fileHdr.Extension;
                for (var i = 0; i <= extBlockIndex; i++)
                {
                    if (nSect == 0)
                    {
                        throw new IOException($"adfFileSeek : file extension block {i} not found");
                    }

                    currentExt = await Disk.ReadFileExtBlock(volume, nSect);
                    nSect = currentExt.Extension;
                }

                dataBlockSector = currentExt.Index[volume.IndexSize - 1 - indexInExtBlk];
                posInExtBlk = indexInExtBlk + 1;
            }

            if (dataBlockSector == 0)
            {
                throw new IOException($"adfFileSeek : data block {dataBlockIndex} not found");
            }

            return dataBlockSector;
        }

/*
 * adfReadFile
 *
 */
        private async Task<int> AdfReadFile(byte[] buffer, int offset, int n)
        {
            // https://github.com/lclevy/ADFlib/blob/be8a6f6e8d0ca8fda963803eef77366c7584649a/src/adf_file.c#L369

            //uint8_t *dataPtr, *bufPtr;

            if (offset < 0 || n < 0 || offset + n > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(n),
                    $"Offset '{offset}' and count '{n}' is outside buffer size '{buffer.Length}'");
            }

            var blockSize = volume.DataBlockSize;
/*puts("adfReadFile");*/
            if (pos + n > fileHdr.ByteSize)
            {
                n = (int)(fileHdr.ByteSize - pos);
            }

            if (n <= 0)
            {
                return 0;
            }

            if (pos == 0 || posInDataBlk == blockSize)
            {
                await AdfReadNextFileBlock();
                posInDataBlk = 0;
            }

            //var dataPtr = currentData.Data;

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
            var bufPtr = offset;
            while (bytesRead < n)
            {
                var size = (int)Math.Min(n - bytesRead, blockSize - posInDataBlk);

                Array.Copy(currentData.Data, posInDataBlk, buffer, bufPtr, size);
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
        public async Task<int> AdfWriteFile(byte[] buffer, int offset, int n)
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
            var bufPtr = offset;
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
                await Disk.WriteEntryBlock(volume, fileHdr.HeaderKey, fileHdr);

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