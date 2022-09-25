namespace Hst.Amiga.RigidDiskBlocks
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Core.Extensions;
    using Extensions;
    using FileSystems;

    public static class FileSystemHeaderBlockWriter
    {
        public static async Task<byte[]> BuildBlock(FileSystemHeaderBlock fileSystemHeaderBlock)
        {
            var blockStream =
                new MemoryStream(
                    fileSystemHeaderBlock.BlockBytes == null || fileSystemHeaderBlock.BlockBytes.Length == 0
                        ? new byte[Constants.BlockSize]
                        : fileSystemHeaderBlock.BlockBytes);

            await blockStream.WriteBytes(BitConverter.GetBytes(BlockIdentifiers.FileSystemHeaderBlock));
            await blockStream.WriteBigEndianUInt32(BlockSize.FileSystemHeaderBlock); // size

            // skip checksum, calculated when block is built
            blockStream.Seek(4, SeekOrigin.Current);

            await blockStream.WriteBigEndianUInt32(fileSystemHeaderBlock
                .HostId); // SCSI Target ID of host, not really used 
            await blockStream.WriteBigEndianUInt32(fileSystemHeaderBlock
                .NextFileSysHeaderBlock); // Block number of the next FileSysHeaderBlock
            await blockStream.WriteBigEndianUInt32(fileSystemHeaderBlock.Flags); // Flags

            // skip reserved
            blockStream.Seek(4 * 2, SeekOrigin.Current);

            await blockStream
                .WriteBytes(fileSystemHeaderBlock
                    .DosType); // # Dostype of the file system, file system description: match this with partition environment's DE_DOSTYPE entry
            
            var version = (uint)((fileSystemHeaderBlock.Version << 16) | fileSystemHeaderBlock.Revision);
            
            await blockStream.WriteBigEndianUInt32(version); // filesystem version 0x0027001b == 39.27
            await blockStream.WriteBigEndianUInt32(fileSystemHeaderBlock.PatchFlags);
            await blockStream.WriteBigEndianUInt32(fileSystemHeaderBlock.Type);
            await blockStream.WriteBigEndianUInt32(fileSystemHeaderBlock.Task);
            await blockStream.WriteBigEndianUInt32(fileSystemHeaderBlock.Lock);
            await blockStream.WriteBigEndianUInt32(fileSystemHeaderBlock.Handler);
            await blockStream.WriteBigEndianUInt32(fileSystemHeaderBlock.StackSize);
            await blockStream.WriteBigEndianInt32(fileSystemHeaderBlock.Priority);
            await blockStream.WriteBigEndianInt32(fileSystemHeaderBlock.Startup);
            await blockStream.WriteBigEndianInt32(fileSystemHeaderBlock
                .SegListBlocks); // first of linked list of LoadSegBlocks
            await blockStream.WriteBigEndianInt32(fileSystemHeaderBlock.GlobalVec);

            blockStream.Seek(0xac, SeekOrigin.Begin);
            await blockStream.WriteString(fileSystemHeaderBlock.FileSystemName, 84);
            blockStream.WriteByte(0);

            // calculate and update checksum
            var blockBytes = blockStream.ToArray();
            fileSystemHeaderBlock.Checksum = ChecksumHelper.UpdateChecksum(blockBytes, 8, BlockSize.FileSystemHeaderBlock * SizeOf.Long);
            fileSystemHeaderBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}