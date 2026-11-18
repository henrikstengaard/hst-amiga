namespace Hst.Amiga.BlockDevices
{
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Block device reading and writing bytes at byte offsets. Used by file systems for all block I/O.
    /// Positional I/O on file handles of file streams was measured slower than seek and read or write on file
    /// streams, as file systems reading many single blocks close to each other benefit from the read buffer of
    /// file streams.
    /// </summary>
    internal abstract class BlockDevice
    {
        /// <summary>
        /// Create block device for stream
        /// </summary>
        public static BlockDevice Create(Stream stream) => new StreamBlockDevice(stream);

        /// <summary>
        /// Stream used by block device
        /// </summary>
        public abstract Stream Stream { get; }

        /// <summary>
        /// Read bytes at offset into buffer. Reads until count bytes are read or end of device is reached.
        /// </summary>
        /// <returns>Number of bytes read, less than count if end of device is reached</returns>
        public abstract Task<int> ReadAsync(long offset, byte[] buffer, int bufferOffset, int count);

        /// <summary>
        /// Write bytes from buffer at offset
        /// </summary>
        public abstract Task WriteAsync(long offset, byte[] buffer, int bufferOffset, int count);

        /// <summary>
        /// Flush writes, so they are visible to users of stream
        /// </summary>
        public abstract Task FlushAsync();

        /// <summary>
        /// Read exactly count bytes at offset into buffer
        /// </summary>
        /// <exception cref="IOException">End of device reached before count bytes are read</exception>
        public async Task ReadExactlyAsync(long offset, byte[] buffer, int bufferOffset, int count)
        {
            var bytesRead = await ReadAsync(offset, buffer, bufferOffset, count);
            if (bytesRead < count)
            {
                throw new IOException(
                    $"Read {bytesRead} bytes at offset {offset}, expected {count} bytes");
            }
        }
    }
}
