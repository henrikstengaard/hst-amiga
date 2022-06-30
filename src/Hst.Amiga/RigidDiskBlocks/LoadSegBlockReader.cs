namespace Hst.Amiga.RigidDiskBlocks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Core.Extensions;
    using FileSystems;

    public static class LoadSegBlockReader
    {
        public static async Task<IEnumerable<LoadSegBlock>> Read(
            RigidDiskBlock rigidDiskBlock, FileSystemHeaderBlock fileSystemHeaderBlock, Stream stream)
        {
            var loadSegBlocks = new List<LoadSegBlock>();
            
            var segListBlock = fileSystemHeaderBlock.SegListBlocks;
            
            do
            {
                // calculate seg list block offset
                var segListBlockOffset = rigidDiskBlock.BlockSize * segListBlock;
                
                // seek partition block offset
                stream.Seek(segListBlockOffset, SeekOrigin.Begin);
                
                // read block
                var block = await BlockHelper.ReadBlock(stream);

                // parse file system header block
                var loadSegBlock = await Parse(block);

                loadSegBlocks.Add(loadSegBlock);
                
                // get next partition list block and increase partition number
                segListBlock = loadSegBlock.NextLoadSegBlock;
            } while (segListBlock > 0);

            return loadSegBlocks;
        }

        public static async Task<LoadSegBlock> Parse(byte[] blockBytes)
        {
            var blockStream = new MemoryStream(blockBytes);
            
            var identifier = BitConverter.ToUInt32(await blockStream.ReadBytes(4), 0);
            if (!identifier.Equals(BlockIdentifiers.LoadSegBlock))
            {
                throw new IOException("Invalid load seg block identifier");
            }
            
            var size = await blockStream.ReadBigEndianUInt32();// Size of the structure for checksums
            var checksum = await blockStream.ReadBigEndianInt32(); // Checksum of the structure
            var hostId = await blockStream.ReadBigEndianUInt32(); // SCSI Target ID of host
            var nextLoadSegBlock = await blockStream.ReadBigEndianInt32(); // block number of the next LoadSegBlock, -1 for last

            var calculatedChecksum = await ChecksumHelper.CalculateChecksum(blockBytes, 8);

            if (checksum != calculatedChecksum)
            {
                throw new IOException("Invalid load seg block checksum");
            }
            
            var length = (size - 5) * 4;
            var data = new byte[length];
            Array.Copy(blockBytes, 5 * 4, data, 0, length);

            return new LoadSegBlock
            {
                BlockBytes = blockBytes,
                Checksum = checksum,
                HostId = hostId,
                NextLoadSegBlock = nextLoadSegBlock,
                Data = data
            };
        }
    }
}