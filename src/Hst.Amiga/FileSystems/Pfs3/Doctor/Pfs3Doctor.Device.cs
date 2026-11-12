namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Port of device.c. Amiga specific device access (TD64, NSD, direct SCSI and access mode detection)
    /// is replaced with stream access.
    /// </summary>
    public partial class Pfs3Doctor
    {
        /**************************************
         * Cache
         *
         * Cache module. Implemented as proxy on device. This module has a problem
         * at the end of the disk: it does not take partial cachelines into
         * account. This is no real problem since it is only used for the reserved
         * area which is located at the start of the partition.
         **************************************/

        /* create cache
         * cacheline size: 4-8-16
         * cachelines: 8-16-32-64-128
         * size = linesize * nolines * blocksize
         */
        public async Task<Error> InitCache(uint linesize, uint nolines, uint blocksize)
        {
            int i;
            var cache = volume.Cache;

            await FreeCache();
            if (linesize != 0 && nolines != 0)
            {
                cache.LineSize = linesize;
                cache.NoLines = nolines;
            }

            if (blocksize != 0)
            {
                cache.BlockSize = blocksize;
            }

            /* allocate memory for the cache */
            cache.CacheLines = new CacheLine[cache.NoLines];
            cache.LruQueue.Clear();
            cache.LruPool.Clear();
            for (i = 0; i < cache.NoLines; i++)
            {
                cache.CacheLines[i] = new CacheLine
                {
                    BlockNr = DoctorConstants.CL_UNUSED, /* a bloknr that is never used */
                    Data = new byte[cache.LineSize * cache.BlockSize]
                };
                cache.LruPool.AddFirst(cache.CacheLines[i].Node);
            }

            return Error.None;
        }

        /// <summary>
        /// Check if aborted (__chkabort).
        /// </summary>
        private void ChkAbort()
        {
            if (cancellationToken.IsCancellationRequested)
            {
                aborting = true;
            }
        }

        /* get block (of size 'bytes') from cache. Cache is per >device<.
         */
        private Task<Error> CGetBlock(byte[] data, uint bloknr, uint bytes) => CGetBlock(data, 0, bloknr, bytes);

        private async Task<Error> CGetBlock(byte[] data, int dataOffset, uint bloknr, uint bytes)
        {
            uint lineblnr, offset, total;
            CacheLine cl;
            var cache = volume.Cache;

            ChkAbort();
            for (total = 0; total < bytes; total += volume.BlockSize, bloknr++, dataOffset += (int)volume.BlockSize)
            {
                lineblnr = (bloknr / cache.LineSize) * cache.LineSize;
                offset = (bloknr % cache.LineSize);
                cl = await CGetCacheLine(lineblnr);
                if (cl == null)
                    return Error.ReadError;

                Array.Copy(cl.Data, offset * volume.BlockSize, data, dataOffset, volume.BlockSize);
            }

            return Error.None;
        }

        /* write block (of size 'bytes') to cache
         */
        private Task<Error> CWriteBlock(byte[] data, uint bloknr, uint bytes) => CWriteBlock(data, 0, bloknr, bytes);

        private async Task<Error> CWriteBlock(byte[] data, int dataOffset, uint bloknr, uint bytes)
        {
            uint lineblnr, offset, total;
            CacheLine cl;
            var cache = volume.Cache;

            ChkAbort();
            for (total = 0; total < bytes; total += volume.BlockSize, bloknr++, dataOffset += (int)volume.BlockSize)
            {
                lineblnr = (bloknr / cache.LineSize) * cache.LineSize;
                offset = (bloknr % cache.LineSize);
                cl = await CGetCacheLine(lineblnr);
                if (cl == null)
                    return Error.ReadError;

                Array.Copy(data, dataOffset, cl.Data, offset * volume.BlockSize, volume.BlockSize);
                cl.Dirty = true;
            }

            return Error.None;
        }

        /* (private) locale function to search a cacheline
         */
        private async Task<CacheLine> CGetCacheLine(uint bloknr)
        {
            uint i;
            CacheLine cl = null;
            Error error;
            var cache = volume.Cache;

            for (i = 0; i < cache.NoLines; i++)
            {
                if (cache.CacheLines[i].BlockNr == bloknr)
                {
                    cl = cache.CacheLines[i];
                    break;
                }
            }

            if (cl == null)
            {
                uint tries;

                /* Find a line to reuse. A dirty line must be written out first;
                 * if that write fails the line is the only copy of its contents,
                 * so it is kept cached and dirty (moved to the head, to be
                 * retried by UpdateCache) and another line is tried instead.
                 */
                for (tries = cache.NoLines; tries > 0; tries--)
                {
                    if (cache.LruPool.Count > 0)
                    {
                        cl = cache.LruPool.First.Value;
                        break;
                    }

                    cl = cache.LruQueue.Last.Value;
                    if (!cl.Dirty)
                        break;

                    error = await volume.WriteRawBlocks(cl.Data, (int)cache.LineSize, cl.BlockNr);
                    if (error == Error.None)
                    {
                        cl.Dirty = false;
                        break;
                    }

                    cl.Node.List.Remove(cl.Node);
                    cache.LruQueue.AddFirst(cl.Node);
                    cl = null;
                }

                if (cl == null)
                    return null;

                error = await volume.GetRawBlocks(cl.Data, (int)cache.LineSize, bloknr);
                if (error != Error.None)
                    return null;
                cl.BlockNr = bloknr;
                cl.Dirty = false;
            }

            cl.Node.List.Remove(cl.Node);
            cache.LruQueue.AddFirst(cl.Node);
            return cl;
        }

        /* update alle dirty blocks in the cache
         */
        private async Task UpdateCache()
        {
            Error error;
            var cache = volume.Cache;

            for (var node = cache.LruQueue.First; node != null; node = node.Next)
            {
                var cl = node.Value;
                if (cl.Dirty)
                {
                    error = await volume.WriteRawBlocks(cl.Data, (int)cache.LineSize, cl.BlockNr);
                    if (error != Error.None)
                        AddError("write error");
                    else
                        cl.Dirty = false; // not in original code, prevents writing the same line again
                }
            }
        }

        /* destroy cache
         */
        private async Task FreeCache()
        {
            var cache = volume.Cache;

            if (cache.CacheLines != null)
            {
                await UpdateCache();
                cache.CacheLines = null;
                cache.LruQueue.Clear();
                cache.LruPool.Clear();
            }
        }

        /**************************************
         * Device functions
         **************************************/

        /*
         * Get blocks from device. Blocknrs are disk (not partition!) based
         */
        public static async Task<Error> DevGetBlocks(Volume volume, byte[] buffer, int blocks, uint blocknr)
        {
            if (blocknr == uint.MaxValue) // blocknr of uninitialised anode
                return Error.ReadError;

            var ioLength = (long)blocks << volume.BlockShift;
            var ioOffset = (long)blocknr << volume.BlockShift;
            var stream = volume.Stream;

            try
            {
                if (ioOffset >= stream.Length)
                {
                    volume.ShowMsg("READ ERROR\n");
                    return Error.ReadError;
                }

                stream.Position = ioOffset;
                var total = 0;
                while (total < ioLength)
                {
                    var bytesRead = await stream.ReadAsync(buffer, total, (int)ioLength - total);
                    if (bytesRead == 0)
                        break;
                    total += bytesRead;
                }

                /* partial cacheline at end of disk */
                if (total < ioLength)
                    Array.Clear(buffer, total, (int)ioLength - total);
            }
            catch (IOException)
            {
                volume.ShowMsg("READ ERROR\n");
                return Error.ReadError;
            }

            return Error.None;
        }

        public static Task<Error> DevWriteBlocksDummy(Volume volume, byte[] buffer, int blocks, uint blocknr)
        {
            return Task.FromResult(Error.None);
        }

        /*
         * Write blocks to device. Blocknrs are disk (not partition!) based
         */
        public static async Task<Error> DevWriteBlocks(Volume volume, byte[] buffer, int blocks, uint blocknr)
        {
            var ioLength = (long)blocks << volume.BlockShift;
            var ioOffset = (long)blocknr << volume.BlockShift;
            var stream = volume.Stream;

            try
            {
                if (ioOffset >= stream.Length)
                {
                    volume.ShowMsg("WRITE ERROR\n");
                    return Error.WriteError;
                }

                /* partial cacheline at end of disk, don't write beyond end of disk */
                if (ioOffset + ioLength > stream.Length)
                    ioLength = stream.Length - ioOffset;

                stream.Position = ioOffset;
                await stream.WriteAsync(buffer, 0, (int)ioLength);
            }
            catch (IOException)
            {
                volume.ShowMsg("WRITE ERROR\n");
                return Error.WriteError;
            }

            return Error.None;
        }
    }
}
