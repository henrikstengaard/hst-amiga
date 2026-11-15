namespace Hst.Amiga.Tests;

using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Extensions;
using Extensions;
using FileSystems;
using FileSystems.FastFileSystem;
using FileSystems.Pfs3;
using FileSystems.SmartFileSystem;
using RigidDiskBlocks;

/// <summary>
/// Creates, formats and mounts disks with FFS, PFS3 or SFS for tests running against all file systems.
/// </summary>
internal static class FileSystemVolumeTestHelper
{
    public const string Ffs = "FFS";
    public const string Pfs3 = "PFS3";
    public const string Sfs = "SFS";

    public static readonly string[] FileSystems = { Ffs, Pfs3, Sfs };

    private const long DiskSize = 10 * 1024 * 1024;

    private static readonly byte[] FfsDosType = { 0x44, 0x4f, 0x53, 0x3 };
    private static readonly byte[] Pfs3DosType = { 0x50, 0x44, 0x53, 0x3 };
    private static readonly byte[] SfsDosType = { 0x53, 0x46, 0x53, 0x0 };

    /// <summary>
    /// Write rigid disk block with one unformatted partition for file system to stream.
    /// </summary>
    public static async Task<PartitionBlock> CreateDisk(string fileSystem, Stream stream)
    {
        var dosType = fileSystem switch
        {
            Ffs => FfsDosType,
            Pfs3 => Pfs3DosType,
            _ => SfsDosType
        };
        var fileSystemBytes = fileSystem == Pfs3
            ? await System.IO.File.ReadAllBytesAsync(Path.Combine("TestData", "Pfs3", "pfs3aio"))
            : Encoding.ASCII.GetBytes("$VER: FileSystem 0.1 (01/01/22) ");

        var rigidDiskBlock = RigidDiskBlock.Create(DiskSize.ToUniversalSize());
        stream.SetLength(rigidDiskBlock.DiskSize);
        rigidDiskBlock.AddFileSystem(dosType, fileSystemBytes).AddPartition("DH0", bootable: true);
        await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, stream);

        return rigidDiskBlock.PartitionBlocks.First();
    }

    /// <summary>
    /// Write rigid disk block with one partition formatted with file system to stream.
    /// </summary>
    public static async Task CreateFormattedDisk(string fileSystem, Stream stream)
    {
        var partitionBlock = await CreateDisk(fileSystem, stream);
        switch (fileSystem)
        {
            case Ffs:
                await FastFileSystemFormatter.FormatPartition(stream, partitionBlock, "Workbench");
                break;
            case Pfs3:
                await Pfs3Formatter.FormatPartition(stream, partitionBlock, "Workbench");
                break;
            default:
                await SmartFileSystemFormatter.FormatPartition(stream, partitionBlock, "Workbench");
                break;
        }
    }

    /// <summary>
    /// Mount first partition in stream as file system volume.
    /// </summary>
    public static async Task<IFileSystemVolume> MountVolume(string fileSystem, Stream stream)
    {
        var partitionBlock = (await RigidDiskBlockReader.Read(stream)).PartitionBlocks.First();
        return fileSystem switch
        {
            Ffs => await FastFileSystemVolume.MountPartition(stream, partitionBlock),
            Pfs3 => await Pfs3Volume.Mount(stream, partitionBlock),
            _ => await SmartFileSystemVolume.Mount(stream, partitionBlock)
        };
    }
}
