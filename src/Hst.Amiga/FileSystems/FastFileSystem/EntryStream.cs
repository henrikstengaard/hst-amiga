namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Blocks;

    internal class EntryStream : Stream
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
        private bool disposed;

        /// <summary>
        /// Max number of consecutive data blocks read or written with a single read or write
        /// </summary>
        private const int MaxBufferedBlocks = 128;

        // read ahead of consecutive data blocks (ffs read mode only)
        private byte[] readAheadBuffer;
        private uint readAheadFirstSector;
        private int readAheadBlocks;
        private DataBlock readAheadDataBlock;

        // pending writes of consecutive data blocks
        private byte[] writeBuffer;
        private uint writeFirstSector;
        private int writeBlocks;

        // preallocated run of consecutive data blocks (write mode only)
        private const int MinPreallocBlocks = 8;
        private uint preallocStart;
        private int preallocCount;
        private int preallocChunk = MinPreallocBlocks;

        // sectors of file ext blocks walked by seek (read mode only, as seek isn't supported in write mode)
        private readonly System.Collections.Generic.List<uint> extBlockSectors =
            new System.Collections.Generic.List<uint>();

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
            if (!disposed && disposing)
            {
                AsyncHelper.RunSync(() => AdfCloseFile());
            }

            disposed = true;
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (!disposed)
            {
                await AdfCloseFile();
            }

            disposed = true;
            await base.DisposeAsync();
        }

        public override void Flush()
        {
            AsyncHelper.RunSync(() => AdfFlushFile());
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await AdfFlushFile();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return AsyncHelper.RunSync(() => AdfReadFile(buffer, offset, count));
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

            AsyncHelper.RunSync(() => AdfFileSeek((uint)Math.Min(position, length)));
            return this.pos;
        }

        public override void SetLength(long value)
        {
            throw new IOException(
                "Set length not supported for entries. Use write buffer instead with buffer set to length");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            AsyncHelper.RunSync(() => AdfWriteFile(buffer, offset, count));
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

                // get file ext block sectors up to file ext block index, file ext blocks are only walked once
                while (extBlockSectors.Count <= extBlockIndex)
                {
                    var nSect = extBlockSectors.Count == 0
                        ? fileHdr.Extension
                        : (await Disk.ReadFileExtBlock(volume, extBlockSectors[extBlockSectors.Count - 1])).Extension;
                    if (nSect == 0)
                    {
                        throw new IOException($"adfFileSeek : file extension block {extBlockSectors.Count} not found");
                    }

                    extBlockSectors.Add(nSect);
                }

                currentExt = await Disk.ReadFileExtBlock(volume, extBlockSectors[(int)extBlockIndex]);
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

            currentData = !volume.UseOfs && !writeMode
                ? await ReadFfsDataBlock(nSect)
                : await Disk.ReadDataBlock(volume, nSect);
            data = currentData;

            if (volume.UseOfs && data.SeqNum != nDataBlock + 1)
            {
                throw new IOException("adfReadNextFileBlock : seqnum incorrect");
            }

            nDataBlock++;
        }

        /// <summary>
        /// Read ffs data block using read ahead. If sector isn't read ahead, then consecutive data block sectors
        /// following current data block are read with a single read.
        /// </summary>
        /// <param name="nSect">Sector of current data block (data block number nDataBlock)</param>
        private async Task<DataBlock> ReadFfsDataBlock(uint nSect)
        {
            var blockSize = volume.FileSystemBlockSize;

            if (readAheadBlocks == 0 || nSect < readAheadFirstSector ||
                nSect >= readAheadFirstSector + (uint)readAheadBlocks)
            {
                var blocks = await CountConsecutiveDataBlockSectors(nSect);
                if (readAheadBuffer == null || readAheadBuffer.Length < blocks * blockSize)
                {
                    // size read ahead buffer to file size, so small files don't allocate max buffer size
                    var dataBlockSize = volume.DataBlockSize;
                    var totalDataBlocks = (fileHdr.ByteSize + dataBlockSize - 1) / dataBlockSize;
                    var bufferBlocks = Math.Max(blocks, (int)Math.Min(MaxBufferedBlocks, totalDataBlocks));
                    readAheadBuffer = new byte[bufferBlocks * blockSize];
                }

                readAheadBlocks = 0;
                await Disk.ReadBlocks(volume, nSect, blocks, readAheadBuffer, 0);
                readAheadFirstSector = nSect;
                readAheadBlocks = blocks;
            }

            if (readAheadDataBlock == null)
            {
                readAheadDataBlock = new DataBlock
                {
                    Data = new byte[blockSize]
                };
                readAheadDataBlock.BlockBytes = readAheadDataBlock.Data;
            }

            Array.Copy(readAheadBuffer, (int)(nSect - readAheadFirstSector) * blockSize,
                readAheadDataBlock.Data, 0, blockSize);
            return readAheadDataBlock;
        }

        /// <summary>
        /// Count consecutive sectors starting with current data block (data block number nDataBlock). Index
        /// tables are followed from file header or current file ext block into next file ext blocks, so runs
        /// of consecutive data blocks are not limited to one index table.
        /// Must be called before nDataBlock is incremented.
        /// </summary>
        private async Task<int> CountConsecutiveDataBlockSectors(uint nSect)
        {
            var dataBlockSize = volume.DataBlockSize;
            var totalDataBlocks = (fileHdr.ByteSize + dataBlockSize - 1) / dataBlockSize;
            var maxBlocks = totalDataBlocks > nDataBlock
                ? (int)Math.Min(MaxBufferedBlocks, totalDataBlocks - nDataBlock)
                : 1;

            // limit to last sector in volume
            var lastSector = volume.LastBlock - volume.FirstBlock;
            if (nSect > lastSector)
            {
                return 1;
            }
            maxBlocks = (int)Math.Min(maxBlocks, lastSector - nSect + 1);

            // index table, index of current data block in table and next file ext block
            uint[] table;
            uint indexInTable;
            uint nextExt;
            if (nDataBlock < volume.IndexSize)
            {
                table = fileHdr.DataBlocks;
                indexInTable = nDataBlock;
                nextExt = fileHdr.Extension;
            }
            else
            {
                if (currentExt == null)
                {
                    return 1;
                }

                // position in ext block is already incremented for current data block
                table = currentExt.Index;
                indexInTable = posInExtBlk - 1;
                nextExt = currentExt.Extension;
            }

            var count = 1;
            while (count < maxBlocks)
            {
                indexInTable++;
                if (indexInTable >= volume.IndexSize)
                {
                    if (nextExt == 0)
                    {
                        break;
                    }

                    // read next file ext block from metadata block cache, stop read ahead if invalid
                    FileExtBlock nextExtBlock;
                    try
                    {
                        nextExtBlock = await Disk.ReadFileExtBlock(volume, nextExt);
                    }
                    catch (IOException)
                    {
                        break;
                    }

                    table = nextExtBlock.Index;
                    nextExt = nextExtBlock.Extension;
                    indexInTable = 0;
                }

                if (table[volume.IndexSize - 1 - indexInTable] != nSect + (uint)count)
                {
                    break;
                }

                count++;
            }

            return count;
        }

        /// <summary>
        /// Queue data block write. Consecutive data blocks are written with a single write when a non-consecutive
        /// data block is queued, max buffered blocks is reached or data block writes are flushed.
        /// </summary>
        private async Task QueueDataBlockWrite(uint sector, DataBlock dataBlock)
        {
            var blockSize = volume.FileSystemBlockSize;
            var blockBytes = volume.UseOfs
                ? DataBlockBuilder.Build(dataBlock, blockSize)
                : dataBlock.Data;

            if (writeBlocks > 0 &&
                (writeBlocks >= MaxBufferedBlocks || sector != writeFirstSector + (uint)writeBlocks))
            {
                await FlushDataBlockWrites();
            }

            if (writeBuffer == null || writeBuffer.Length < (writeBlocks + 1) * blockSize)
            {
                // grow write buffer as data blocks are queued, so small files don't allocate max buffer size
                var bufferBlocks = writeBuffer == null
                    ? 8
                    : Math.Min(MaxBufferedBlocks, writeBuffer.Length / blockSize * 2);
                var newWriteBuffer = new byte[bufferBlocks * blockSize];
                if (writeBuffer != null && writeBlocks > 0)
                {
                    Array.Copy(writeBuffer, newWriteBuffer, writeBlocks * blockSize);
                }

                writeBuffer = newWriteBuffer;
            }

            if (writeBlocks == 0)
            {
                writeFirstSector = sector;
            }

            Array.Copy(blockBytes, 0, writeBuffer, writeBlocks * blockSize, blockSize);
            writeBlocks++;
        }

        private async Task FlushDataBlockWrites()
        {
            if (writeBlocks == 0)
            {
                return;
            }

            var blocks = writeBlocks;
            writeBlocks = 0;
            await Disk.WriteBlocks(volume, writeFirstSector, blocks, writeBuffer, 0);
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
                nSect = AllocateDataBlock();
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

                nSect = AllocateDataBlock();
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
                    await QueueDataBlockWrite(curDataPtr, currentData);
                }

                /* initialize a new data block */
                Array.Clear(data.Data, 0, (int)blockSize);
                data.SeqNum = nDataBlock + 1;
                data.DataSize = blockSize;
                data.NextData = 0;
                data.HeaderKey = fileHdr.HeaderKey;
            }
            else if (pos >= blockSize)
            {
                await QueueDataBlockWrite(curDataPtr, currentData);
            }

            curDataPtr = nSect;
            nDataBlock++;

            return nSect;
        }

        /// <summary>
        /// Allocate data block from preallocated run of consecutive blocks. Runs grow from min to max buffered
        /// blocks, so small files allocate small runs and large files get long runs of consecutive data blocks.
        /// File ext blocks are allocated separately after the preallocated run, so they don't split data runs.
        /// </summary>
        private uint AllocateDataBlock()
        {
            if (preallocCount == 0)
            {
                var (start, count) = Bitmap.AllocateRun(volume, preallocChunk);
                preallocStart = start;
                preallocCount = count;
                preallocChunk = Math.Min(preallocChunk * 2, MaxBufferedBlocks);
            }

            var sector = preallocStart;
            preallocStart++;
            preallocCount--;
            return sector;
        }

        /// <summary>
        /// Free preallocated data blocks not used
        /// </summary>
        private void FreePreallocatedBlocks()
        {
            if (preallocCount == 0)
            {
                return;
            }

            Bitmap.FreeRun(volume, preallocStart, preallocCount);
            preallocCount = 0;
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
                        await QueueDataBlockWrite(curDataPtr, currentData);
                    }

                    await FlushDataBlockWrites();
                }
            }

            if (writeMode)
            {
                fileHdr.ByteSize = pos;
                fileHdr.Date = DateTime.Now;
                FreePreallocatedBlocks();
                await Disk.WriteEntryBlock(volume, fileHdr.HeaderKey, fileHdr);

                if (volume.UseDirCache)
                {
                    var parent = await Disk.ReadEntryBlock(volume, fileHdr.Parent);
                    await Cache.UpdateCache(volume, parent, fileHdr, true);
                }

                // bitmap is written when volume is flushed or disposed
                await Bitmap.AdfDeferUpdateBitmap(volume);
                await Disk.CheckpointIfNeeded(volume);
            }
        }
    }
}