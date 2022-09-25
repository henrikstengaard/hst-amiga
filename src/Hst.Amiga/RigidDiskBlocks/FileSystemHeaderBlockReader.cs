namespace Hst.Amiga.RigidDiskBlocks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Core.Extensions;
    using Extensions;
    using FileSystems;
    using VersionStrings;

    public static class FileSystemHeaderBlockReader
    {
        public static async Task<IEnumerable<FileSystemHeaderBlock>> Read(
            RigidDiskBlock rigidDiskBlock, Stream stream)
        {
            if (rigidDiskBlock.FileSysHdrList == BlockIdentifiers.EndOfBlock)
            {
                return Enumerable.Empty<FileSystemHeaderBlock>();
            }
            
            var fileSystemHeaderBlocks = new List<FileSystemHeaderBlock>();

            var fileSysHdrList = rigidDiskBlock.FileSysHdrList;

            do
            {
                // calculate file system header block offset
                var fileSystemHeaderBlockOffset = rigidDiskBlock.BlockSize * fileSysHdrList;

                // seek partition block offset
                stream.Seek(fileSystemHeaderBlockOffset, SeekOrigin.Begin);

                // read block
                var blockBytes = await Disk.ReadBlock(stream, (int)rigidDiskBlock.BlockSize);

                // parse file system header block
                var fileSystemHeaderBlock = await Parse(blockBytes);

                fileSystemHeaderBlocks.Add(fileSystemHeaderBlock);

                // get next partition list block and increase partition number
                fileSysHdrList = fileSystemHeaderBlock.NextFileSysHeaderBlock;
            } while (fileSysHdrList > 0 && fileSysHdrList != BlockIdentifiers.EndOfBlock);

            foreach (var fileSystemHeaderBlock in fileSystemHeaderBlocks)
            {
                fileSystemHeaderBlock.LoadSegBlocks =
                    await LoadSegBlockReader.Read(rigidDiskBlock, fileSystemHeaderBlock, stream);

                fileSystemHeaderBlock.FileSystemSize = fileSystemHeaderBlock.LoadSegBlocks.Sum(x => x.Data.Length);
            }

            return fileSystemHeaderBlocks;
        }

        public static async Task<FileSystemHeaderBlock> Parse(byte[] blockBytes)
        {
            var blockStream = new MemoryStream(blockBytes);

            var identifier = BitConverter.ToUInt32(await blockStream.ReadBytes(4), 0);
            if (!identifier.Equals(BlockIdentifiers.FileSystemHeaderBlock))
            {
                throw new IOException("Invalid file system header block identifier");
            }

            var size = await blockStream.ReadBigEndianUInt32(); // Size of the structure for checksums
            var checksum = await blockStream.ReadBigEndianInt32(); // Checksum of the structure
            var hostId = await blockStream.ReadBigEndianUInt32(); // SCSI Target ID of host, not really used
            var nextFileSysHeaderBlock = await blockStream.ReadBigEndianUInt32(); // Block number of the next FileSysHeaderBlock
            var flags = await blockStream.ReadBigEndianUInt32(); // Flags

            // read reserved, unused word
            for (var i = 0; i < 2; i++)
            {
                await blockStream.ReadBytes(4);
            }

            var dosType =
                await blockStream
                    .ReadBytes(4); // # Dostype of the file system, file system description: match this with partition environment's DE_DOSTYPE entry
            var version = await blockStream.ReadBigEndianUInt32(); // filesystem version 0x0027001b == 39.27
            var patchFlags = await blockStream.ReadBigEndianUInt32();
            var type = await blockStream.ReadBigEndianUInt32();
            var task = await blockStream.ReadBigEndianUInt32();
            var fileSysLock = await blockStream.ReadBigEndianUInt32();
            var handler = await blockStream.ReadBigEndianUInt32();
            var stackSize = await blockStream.ReadBigEndianUInt32();
            var priority = await blockStream.ReadBigEndianInt32();
            var startup = await blockStream.ReadBigEndianInt32();
            var segListBlocks = await blockStream.ReadBigEndianInt32(); // first of linked list of LoadSegBlocks
            var globalVec = await blockStream.ReadBigEndianInt32();

            blockStream.Seek(172, SeekOrigin.Begin);
            var fileSystemName = await blockStream.ReadNullTerminatedString(84);

            var calculatedChecksum = ChecksumHelper.CalculateChecksum(blockBytes, 8, (int)size * SizeOf.Long);

            if (checksum != calculatedChecksum)
            {
                throw new IOException("Invalid file system header block checksum");
            }

            return new FileSystemHeaderBlock
            {
                BlockBytes = blockBytes,
                Checksum = checksum,
                HostId = hostId,
                NextFileSysHeaderBlock = nextFileSysHeaderBlock,
                Flags = flags,
                DosType = dosType,
                Version = (int)(version >> 16),
                Revision = (int)(version & 0xFFFF),
                PatchFlags = patchFlags,
                Type = type,
                Task = task,
                Lock = fileSysLock,
                Handler = handler,
                StackSize = stackSize,
                Priority = priority,
                Startup = startup,
                SegListBlocks = segListBlocks,
                GlobalVec = globalVec,
                FileSystemName = fileSystemName
            };
        }
    }
}