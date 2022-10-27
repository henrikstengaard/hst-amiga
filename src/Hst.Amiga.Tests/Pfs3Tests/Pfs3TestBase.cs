namespace Hst.Amiga.Tests.Pfs3Tests;

using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Converters;
using Core.Extensions;
using Extensions;
using FileSystems.Pfs3;
using RigidDiskBlocks;
using Constants = FileSystems.Pfs3.Constants;

public abstract class Pfs3TestBase
{
    private const long Size1Mb = 1024 * 1024;
    private const long Size1Gb = 1024 * 1024 * 1024;
    
    protected const long DiskSize100Mb = Size1Mb * 100;
    protected const long DiskSize4Gb = Size1Gb * 4;
    protected const long DiskSize16Gb = Size1Gb * 16;

    private static readonly byte[] Pfs3DosType = { 0x50, 0x44, 0x53, 0x3 };
    private static readonly string Pfs3AioPath = Path.Combine("TestData", "Pfs3", "pfs3aio");
    
    protected async Task<BlockMemoryStream> CreatePfs3FormattedDisk(long diskSize = 100 * 1024 * 1024)
    {
        var stream = new BlockMemoryStream();
        
        var rigidDiskBlock = RigidDiskBlock.Create(diskSize.ToUniversalSize());
        stream.SetLength(rigidDiskBlock.DiskSize);
        
        rigidDiskBlock.AddFileSystem(Pfs3DosType, await System.IO.File.ReadAllBytesAsync(Pfs3AioPath))
            .AddPartition("DH0", bootable: true);
        await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);
        
        var partitionBlock = rigidDiskBlock.PartitionBlocks.First();

        await Pfs3Formatter.FormatPartition(stream, partitionBlock, "Workbench");

        return stream;
    }
    
    protected async Task<Pfs3Volume> MountVolume(Stream stream)
    {
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);

        var partitionBlock = rigidDiskBlock.PartitionBlocks.First();

        return await Pfs3Volume.Mount(stream, partitionBlock);
    }

    protected async Task WriteStreamToFile(BlockMemoryStream stream, string path)
    {
        await using var fileStream = System.IO.File.OpenWrite(path);
        await stream.WriteTo(fileStream);
    }

    public string VerifyPfs3Disk(BlockMemoryStream stream, PartitionBlock partition)
    {
        var reportBuilder = new StringBuilder();

        var start = (long)partition.BlocksPerTrack * partition.Surfaces * 512 * partition.LowCyl;

        var reservedBlockSize = Init.CalculateReservedBlockSize(partition.Sectors);

        var reservedBlockEnd = 0L;
        foreach (var block in stream.Blocks.OrderBy(x => x.Key))
        {
            if (block.Key < start || block.Key < reservedBlockEnd)
            {
                continue;
            }
            
            var reservedBlockId = BigEndianConverter.ConvertBytesToUInt16(block.Value);

            var blockNo = (block.Key - start) / 512;
            
            if (BigEndianConverter.ConvertBytesToUInt32(block.Value) == Constants.ID_PFS_DISK)
            {
                if (blockNo == 0)
                {
                    reportBuilder.AppendLine($"BootBlock, block no. {blockNo}, offset {block.Key}");
                    continue;
                }
                
                if (blockNo == 2)
                {
                    reportBuilder.AppendLine($"RootBlock, block no. {blockNo}, offset {block.Key}");
                    continue;
                }
            }
            
            switch (reservedBlockId)
            {
                case Constants.DBLKID:
                    reservedBlockEnd = block.Key + reservedBlockSize;
                    reportBuilder.AppendLine($"DirBlock (DB), block no. {blockNo}, offset {block.Key}");
                    continue;
                case Constants.ABLKID:
                    reservedBlockEnd = block.Key + reservedBlockSize;
                    reportBuilder.AppendLine($"AnodeBlock (AB), block no. {blockNo}, offset {block.Key}");
                    continue;
                case Constants.IBLKID:
                    reservedBlockEnd = block.Key + reservedBlockSize;
                    reportBuilder.AppendLine($"IndexBlock (IB), block no. {blockNo}, offset {block.Key}");
                    continue;
                case Constants.BMBLKID:
                    reservedBlockEnd = block.Key + reservedBlockSize;
                    reportBuilder.AppendLine($"BitmapBlock (BM), block no. {blockNo}, offset {block.Key}");
                    continue;
                case Constants.BMIBLKID:
                    reservedBlockEnd = block.Key + reservedBlockSize;
                    reportBuilder.AppendLine($"BitmapIndexBlock (MI), block no. {blockNo}, offset {block.Key}");
                    continue;
                case Constants.DELDIRID:
                    reservedBlockEnd = block.Key + reservedBlockSize;
                    reportBuilder.AppendLine($"DelDirBlock (DD), block no. {blockNo}, offset {block.Key}");
                    continue;
                case Constants.EXTENSIONID:
                    reservedBlockEnd = block.Key + reservedBlockSize;
                    reportBuilder.AppendLine($"ExtensionBlock (EX), block no. {blockNo}, offset {block.Key}");
                    continue;
                case Constants.SBLKID:
                    reservedBlockEnd = block.Key + reservedBlockSize;
                    reportBuilder.AppendLine($"SuperBlock (SB), block no. {blockNo}, offset {block.Key}");
                    continue;
                default:
                    reservedBlockEnd = block.Key + reservedBlockSize;
                    reportBuilder.AppendLine($"Unknown block, block no. {blockNo}, offset {block.Key}");
                    continue;
            }
        }

        return reportBuilder.ToString();
    }
}