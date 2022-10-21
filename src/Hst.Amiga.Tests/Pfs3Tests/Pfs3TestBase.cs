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
    protected static readonly byte[] Pfs3DosType = { 0x50, 0x44, 0x53, 0x3 };
    protected static readonly string Pfs3AioPath = Path.Combine("TestData", "Pfs3", "pfs3aio");
    protected readonly RigidDiskBlock RigidDiskBlock = RigidDiskBlock
        .Create(100.MB().ToUniversalSize());
    protected static readonly BlockMemoryStream Stream = new BlockMemoryStream();

    protected async Task CreatePfs3FormattedDisk()
    {
        Stream.SetLength(RigidDiskBlock.DiskSize);
        
        RigidDiskBlock.AddFileSystem(Pfs3DosType, await System.IO.File.ReadAllBytesAsync(Pfs3AioPath))
            .AddPartition("DH0", bootable: true);
        await RigidDiskBlockWriter.WriteBlock(RigidDiskBlock, Stream);
        
        var partitionBlock = RigidDiskBlock.PartitionBlocks.First();

        await Pfs3Formatter.FormatPartition(Stream, partitionBlock, "Workbench");
    }

    protected async Task WriteStreamToFile(string path)
    {
        await using var fileStream = System.IO.File.OpenWrite(path);
        await Stream.WriteTo(fileStream);
    }

    public string VerifyPfs3Disk(PartitionBlock partition)
    {
        var reportBuilder = new StringBuilder();

        var start = (long)partition.BlocksPerTrack * partition.Surfaces * 512 * partition.LowCyl;

        var reservedBlockSize = Init.CalculateReservedBlockSize(partition.Sectors);

        var reservedBlockEnd = 0L;
        foreach (var block in Stream.Blocks.OrderBy(x => x.Key))
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