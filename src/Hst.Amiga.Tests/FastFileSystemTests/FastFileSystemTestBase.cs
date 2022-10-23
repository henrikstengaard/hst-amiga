namespace Hst.Amiga.Tests.FastFileSystemTests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Core.Extensions;
    using Extensions;
    using FileSystems;
    using FileSystems.FastFileSystem;
    using RigidDiskBlocks;

    public abstract class FastFileSystemTestBase
    {
        protected static readonly byte[] Dos3DosType = { 0x44, 0x4f, 0x53, 0x3 };
        protected static readonly byte[] DummyFastFileSystemBytes = Encoding.ASCII.GetBytes(
            "$VER: FastFileSystem 1.0 (12/12/22) ");  
        protected readonly RigidDiskBlock RigidDiskBlock = RigidDiskBlock
            .Create(100.MB().ToUniversalSize());
        protected static readonly BlockMemoryStream Stream = new();
        
        protected async Task CreateFastFileSystemFormattedDisk()
        {
            Stream.SetLength(RigidDiskBlock.DiskSize);
        
            RigidDiskBlock.AddFileSystem(Dos3DosType, DummyFastFileSystemBytes)
                .AddPartition("DH0", bootable: true);
            await RigidDiskBlockWriter.WriteBlock(RigidDiskBlock, Stream);
        
            var partitionBlock = RigidDiskBlock.PartitionBlocks.First();

            await FastFileSystemFormatter.FormatPartition(Stream, partitionBlock, "Workbench");
        }
        
        protected readonly DateTime Date = new(2022, 2, 3, 14, 45, 33, DateTimeKind.Utc);
        
        protected async Task<byte[]> CreateExpectedRootBlockBytes()
        {
            var blockStream = new MemoryStream(new byte[512]);

            await blockStream.WriteBigEndianInt32(2); // type

            blockStream.Seek(12, SeekOrigin.Begin);
            await blockStream.WriteBigEndianInt32(0x48); // ht_size

            blockStream.Seek(312, SeekOrigin.Begin);
            await blockStream.WriteBigEndianInt32(-1); // bm_flag				
            await blockStream.WriteBigEndianInt32(881); // bm_pages (sector with bitmap block)

            blockStream.Seek(420, SeekOrigin.Begin);

            var amigaDate = new DateTime(1978, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var diffDate = Date - amigaDate;
            var days = diffDate.Days;
            var minutes = diffDate.Hours * 60 + diffDate.Minutes;
            const int ticksPerSecond = 50;
            var ticksSeconds = diffDate.Seconds * ticksPerSecond;
            var ticksMilliseconds = diffDate.Milliseconds == 0 ? 0 : Convert.ToInt32(((double)1000 / diffDate.Milliseconds) * ticksPerSecond);
            var ticks = ticksSeconds + ticksMilliseconds;
            
            // last root alteration date
            await blockStream.WriteBigEndianInt32(days); // days since 1 jan 78
            await blockStream.WriteBigEndianInt32(minutes); // minutes past midnight
            await blockStream.WriteBigEndianInt32(ticks); // ticks (1/50 sec) past last minute

            var diskName = "HstWB";
            await blockStream.WriteBytes(new[] { Convert.ToByte(diskName.Length) });
            await blockStream.WriteString(diskName, 30);       

            // last disk alteration date
            blockStream.Seek(472, SeekOrigin.Begin);
            await blockStream.WriteBigEndianInt32(0); // days since 1 jan 78
            await blockStream.WriteBigEndianInt32(0); // minutes past midnight
            await blockStream.WriteBigEndianInt32(0); // ticks (1/50 sec) past last minute

            // filesystem creation date
            await blockStream.WriteBigEndianInt32(days); // days since 1 jan 78
            await blockStream.WriteBigEndianInt32(minutes); // minutes past midnight
            await blockStream.WriteBigEndianInt32(ticks); // ticks (1/50 sec) past last minute
            
            blockStream.Seek(504, SeekOrigin.Begin);
            await blockStream.WriteBigEndianInt32(0); // FFS: first directory cache block, 0 otherwise
            await blockStream.WriteBigEndianInt32(1); // block secondary type = ST_ROOT (value 1)
            
            // calculate and update checksum
            var rootBlockBytes = blockStream.ToArray();
            ChecksumHelper.UpdateChecksum(rootBlockBytes, 20);

            return rootBlockBytes;
        }
        
        protected async Task<byte[]> CreateExpectedBitmapBlockBytes()
        {
            var blockStream = new MemoryStream(new byte[512]);

            await blockStream.WriteBigEndianInt32(0); // dummy checksum
            
            // free blocks
            var freeSectorBytes = new byte[] { 0xff, 0xff, 0xff, 0xff };
            for (var i = 0; i < 27; i++)
            {
                await blockStream.WriteBytes(freeSectorBytes);
            }
            
            // 27 * 32 + 14
            
            // allocated blocks
            // bits: 11111111111111110011111111111111
            var rootBitmapBytes = new byte[] { 0xff, 0xff, 0x3f, 0xff };
            await blockStream.WriteBytes(rootBitmapBytes);
            
            // free sectors
            for (var i = 0; i < 27; i++)
            {
                await blockStream.WriteBytes(freeSectorBytes);
            }
            
            // unused bytes
            for (var i = 0; i < 72; i++)
            {
                await blockStream.WriteBigEndianInt32(0);
            }

            // calculate and update checksum
            var bitmapBytes = blockStream.ToArray();
            ChecksumHelper.UpdateChecksum(bitmapBytes, 0);

            return bitmapBytes;            
        }
    }
}