namespace Hst.Amiga.Tests.Pfs3Tests;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Converters;
using FileSystems.Pfs3.Doctor;
using RigidDiskBlocks;
using Xunit;
using Xunit.Abstractions;
using Constants = FileSystems.Pfs3.Constants;
using FileMode = FileSystems.FileMode;

[Trait("Category", "PFS3")]
public class GivenPfs3Doctor : Pfs3TestBase
{
    private readonly ITestOutputHelper output;

    public GivenPfs3Doctor(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task When_CheckingFormattedDisk_Then_NoErrorsAreFound()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // act - check disk
        var (doctor, messages) = await RunDoctor(stream, Mode.Check);

        // assert - no errors are found
        Assert.Equal(Error.None, doctor.Result);
        Assert.Equal(0, doctor.Stats.NumErrors);
        Assert.DoesNotContain(messages, x => x.StartsWith("ERROR"));
    }

    [Fact]
    public async Task When_GettingInfoForFormattedDisk_Then_DiskInfoIsShown()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // act - get info for disk
        var (doctor, messages) = await RunDoctor(stream, Mode.Info);

        // assert - disk info is shown
        Assert.Equal(Error.None, doctor.Result);
        Assert.Contains("Checking disk Workbench", messages);
        Assert.Contains(messages, x => x.StartsWith("Disk formatted with PFS-III"));
    }

    [Fact]
    public async Task When_CheckingDiskWithDirectoriesAndFiles_Then_NoErrorsAreFoundAndObjectsAreCounted()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - create directories and files
        await CreateDirectoriesAndFiles(stream);

        // act - check disk
        var (doctor, messages) = await RunDoctor(stream, Mode.Check);

        // assert - no errors are found
        Assert.Equal(Error.None, doctor.Result);
        Assert.Equal(0, doctor.Stats.NumErrors);
        Assert.DoesNotContain(messages, x => x.StartsWith("ERROR"));

        // assert - directories (incl. root) and files are counted
        Assert.Equal(3, doctor.Stats.NumDirs);
        Assert.Equal(3, doctor.Stats.NumFiles);
    }

    [Fact]
    public async Task When_CheckingDiskWithCorruptBootBlock_Then_ErrorIsFoundAndDiskIsNotChanged()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - corrupt boot block id
        var bootBlockOffset = await GetPartitionOffset(stream);
        await WriteUInt32(stream, bootBlockOffset, 0);

        // act - check disk
        var (doctor, messages) = await RunDoctor(stream, Mode.Check);

        // assert - error is found
        Assert.Equal(1, doctor.Stats.NumErrors);
        Assert.Contains("ERROR:Bootblock has wrong ID", messages);

        // assert - boot block is not changed in check mode
        Assert.Equal(0U, await ReadUInt32(stream, bootBlockOffset));
    }

    [Fact]
    public async Task When_RepairingDiskWithCorruptBootBlock_Then_BootBlockIsRepaired()
    {
        // arrange - create pfs3 formatted disk
        var stream = await CreatePfs3FormattedDisk();

        // arrange - corrupt boot block id
        var bootBlockOffset = await GetPartitionOffset(stream);
        await WriteUInt32(stream, bootBlockOffset, 0);

        // act - repair disk
        var (doctor, messages) = await RunDoctor(stream, Mode.Repair);

        // assert - error is fixed
        Assert.Equal(Error.None, doctor.Result);
        Assert.Equal(1, doctor.Stats.ErrorsFixed);
        Assert.Contains("FIXED:Bootblock had wrong ID", messages);
        Assert.Equal((uint)Constants.ID_PFS_DISK, await ReadUInt32(stream, bootBlockOffset));

        // assert - check disk finds no errors after repair
        var (checkDoctor, _) = await RunDoctor(stream, Mode.Check);
        Assert.Equal(0, checkDoctor.Stats.NumErrors);
    }

    [Fact]
    public async Task When_RepairingDiskWithCorruptReservedBitmap_Then_BitmapIsRepaired()
    {
        // arrange - create pfs3 formatted disk with directories and files
        var stream = await CreatePfs3FormattedDisk();
        await CreateDirectoriesAndFiles(stream);

        // arrange - mark first reserved blocks as free in reserved bitmap stored after rootblock
        var reservedBitmapOffset = await GetPartitionOffset(stream) + 3 * 512;
        Assert.Equal(Constants.BMBLKID, (ushort)(await ReadUInt32(stream, reservedBitmapOffset) >> 16));
        await WriteUInt32(stream, reservedBitmapOffset + 12, 0xffffffff);
        await WriteUInt32(stream, reservedBitmapOffset + 16, 0xffffffff);

        // arrange - check disk finds errors
        var (checkDoctor, checkMessages) = await RunDoctor(stream, Mode.Check);
        Assert.NotEqual(0, checkDoctor.Stats.NumErrors);
        Assert.Contains(checkMessages, x => x.Contains("reserved blocks used but not allocated"));

        // act - repair disk
        var (doctor, _) = await RunDoctor(stream, Mode.Repair);

        // assert - repair succeeded
        Assert.Equal(Error.None, doctor.Result);

        // assert - check disk finds no errors after repair
        var (afterDoctor, afterMessages) = await RunDoctor(stream, Mode.Check);
        Assert.Equal(0, afterDoctor.Stats.NumErrors);
        Assert.DoesNotContain(afterMessages, x => x.StartsWith("ERROR"));
    }

    [Fact]
    public async Task When_RepairingDiskWithCorruptMainBitmap_Then_BitmapIsRepaired()
    {
        // arrange - create pfs3 formatted disk with directories and files
        var stream = await CreatePfs3FormattedDisk();
        await CreateDirectoriesAndFiles(stream);

        // arrange - mark all data blocks in first bitmap block as free.
        // bitmap block is found via rootblock bitmap index and bitmap index block
        var partitionOffset = await GetPartitionOffset(stream);
        var bitmapIndexBlockNr = await ReadUInt32(stream, partitionOffset + 2 * 512 + 96);
        var bitmapBlockNr = await ReadUInt32(stream, partitionOffset + bitmapIndexBlockNr * 512L + 12);
        var bitmapBlockOffset = partitionOffset + bitmapBlockNr * 512L;
        Assert.Equal(Constants.BMBLKID, (ushort)(await ReadUInt32(stream, bitmapBlockOffset) >> 16));
        for (var i = 0; i < 16; i++)
        {
            await WriteUInt32(stream, bitmapBlockOffset + 12 + i * 4, 0xffffffff);
        }

        // arrange - check disk finds errors
        var (checkDoctor, _) = await RunDoctor(stream, Mode.Check);
        Assert.NotEqual(0, checkDoctor.Stats.NumErrors);

        // act - repair disk
        var (doctor, messages) = await RunDoctor(stream, Mode.Repair);

        // assert - bitmap is repaired
        Assert.Equal(Error.None, doctor.Result);
        Assert.Contains(messages, x => x.StartsWith("FIXED:") && x.Contains("data blocks used but not allocated"));

        // assert - check disk finds no errors after repair
        var (afterDoctor, afterMessages) = await RunDoctor(stream, Mode.Check);
        Assert.Equal(0, afterDoctor.Stats.NumErrors);
        Assert.DoesNotContain(afterMessages, x => x.StartsWith("ERROR"));
    }

    [Fact]
    public async Task When_RepairingDiskWithCorruptDirEntry_Then_DirEntryIsRemoved()
    {
        // arrange - create pfs3 formatted disk with directories and files
        var stream = await CreatePfs3FormattedDisk();
        await CreateDirectoriesAndFiles(stream);

        // arrange - corrupt size of first dir entry in root dir block, so it doesn't match anodes
        var dirBlockOffset = await FindRootDirBlockOffset(stream);
        await WriteUInt32(stream, dirBlockOffset + 20 + 6, 0x7fffffff);

        // act - repair disk
        var (doctor, messages) = await RunDoctor(stream, Mode.Repair);

        // assert - corrupt dir entry is removed
        Assert.Equal(Error.None, doctor.Result);
        Assert.Contains("FIXED:Removing corrupt directory entry", messages);

        // assert - check disk finds no errors after repair
        var (afterDoctor, afterMessages) = await RunDoctor(stream, Mode.Check);
        Assert.Equal(0, afterDoctor.Stats.NumErrors);
        Assert.DoesNotContain(afterMessages, x => x.StartsWith("ERROR"));
    }

    [Fact]
    public async Task When_RepairingDiskWithCorruptBitmapIndexBlock_Then_BitmapIndexBlockIsReplacedWithBackup()
    {
        // arrange - create pfs3 formatted disk with directories and files
        var stream = await CreatePfs3FormattedDisk();
        await CreateDirectoriesAndFiles(stream);

        // arrange - corrupt id of bitmap index block
        var partitionOffset = await GetPartitionOffset(stream);
        var bitmapIndexBlockNr = await ReadUInt32(stream, partitionOffset + 2 * 512 + 96);
        var bitmapIndexBlockOffset = partitionOffset + bitmapIndexBlockNr * 512L;
        Assert.Equal(Constants.BMIBLKID, (ushort)(await ReadUInt32(stream, bitmapIndexBlockOffset) >> 16));
        await WriteUInt32(stream, bitmapIndexBlockOffset, 0);

        // act - repair disk
        var (doctor, messages) = await RunDoctor(stream, Mode.Repair);

        // assert - bitmap index block is replaced with backup found in reserved area
        Assert.Equal(Error.None, doctor.Result);
        Assert.Contains(messages, x => x.StartsWith($"FIXED:Replaced block {bitmapIndexBlockNr} with backup"));

        // assert - check disk finds no errors after repair
        var (afterDoctor, afterMessages) = await RunDoctor(stream, Mode.Check);
        Assert.Equal(0, afterDoctor.Stats.NumErrors);
        Assert.DoesNotContain(afterMessages, x => x.StartsWith("ERROR"));
        Assert.Equal(3, afterDoctor.Stats.NumFiles);
    }

    [Fact]
    public async Task When_UnformattingDiskWithCorruptRootBlock_Then_RootBlockIsRebuilt()
    {
        // arrange - create pfs3 formatted disk with directories and files
        var stream = await CreatePfs3FormattedDisk();
        await CreateDirectoriesAndFiles(stream);

        // arrange - clear root block
        var partitionOffset = await GetPartitionOffset(stream);
        for (var i = 0; i < 512; i += 4)
        {
            await WriteUInt32(stream, partitionOffset + 2 * 512 + i, 0);
        }

        // act - unformat disk
        var (doctor, messages) = await RunDoctor(stream, Mode.Unformat);

        // assert - root block is rebuilt
        Assert.Equal(Error.None, doctor.Result);
        Assert.Contains("FIXED:new rootblock build", messages);

        // assert - check disk finds no errors after unformat and files are found
        var (afterDoctor, afterMessages) = await RunDoctor(stream, Mode.Check);
        Assert.Equal(0, afterDoctor.Stats.NumErrors);
        Assert.DoesNotContain(afterMessages, x => x.StartsWith("ERROR"));
        Assert.Equal(3, afterDoctor.Stats.NumFiles);
    }

    private async Task<(Pfs3Doctor, IList<string>)> RunDoctor(Stream stream, Mode mode)
    {
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        var partitionBlock = rigidDiskBlock.PartitionBlocks.First();

        var messages = new List<string>();
        var line = new StringBuilder();
        var volume = Pfs3Doctor.OpenVolume(stream, partitionBlock);
        volume.ShowMsg = message =>
        {
            line.Append(message);
            var text = line.ToString();
            int index;
            while ((index = text.IndexOf('\n')) >= 0)
            {
                var msg = text.Substring(0, index);
                messages.Add(msg);
                output.WriteLine(msg);
                text = text.Substring(index + 1);
            }

            line.Clear();
            line.Append(text);
        };

        var doctor = await Pfs3Doctor.Run(volume, mode);
        output.WriteLine($"Result: {doctor.Result}, errors: {doctor.Stats.NumErrors}, fixed: {doctor.Stats.ErrorsFixed}");
        return (doctor, messages);
    }

    private async Task CreateDirectoriesAndFiles(Stream stream)
    {
        await using var pfs3Volume = await MountVolume(stream);
        await pfs3Volume.CreateFile("file1", true, true);
        await WriteFile(pfs3Volume, "file1", 5000);
        await pfs3Volume.CreateDirectory("dir1");
        await pfs3Volume.CreateDirectory("dir2");
        await pfs3Volume.ChangeDirectory("dir1");
        await pfs3Volume.CreateFile("file2", true, true);
        await WriteFile(pfs3Volume, "file2", 100000);
        await pfs3Volume.CreateFile("file3", true, true);
    }

    private static async Task WriteFile(FileSystems.Pfs3.Pfs3Volume pfs3Volume, string name, int size)
    {
        await using var fileStream = await pfs3Volume.OpenFile(name, FileMode.Write);
        var data = new byte[size];
        for (var i = 0; i < size; i++)
        {
            data[i] = (byte)(i & 0xff);
        }

        await fileStream.WriteAsync(data, 0, data.Length);
    }

    private static async Task<long> GetPartitionOffset(Stream stream)
    {
        var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
        var partitionBlock = rigidDiskBlock.PartitionBlocks.First();
        return (long)partitionBlock.LowCyl * partitionBlock.Surfaces * partitionBlock.BlocksPerTrack * 512;
    }

    private static async Task<uint> ReadUInt32(Stream stream, long offset)
    {
        var data = new byte[4];
        stream.Position = offset;
        _ = await stream.ReadAsync(data, 0, data.Length);
        return BigEndianConverter.ConvertBytesToUInt32(data);
    }

    private static async Task WriteUInt32(Stream stream, long offset, uint value)
    {
        var data = new byte[4];
        BigEndianConverter.ConvertUInt32ToBytes(value, data, 0);
        stream.Position = offset;
        await stream.WriteAsync(data, 0, data.Length);
    }

    private static async Task<long> FindRootDirBlockOffset(Stream stream)
    {
        var partitionOffset = await GetPartitionOffset(stream);
        var data = new byte[512];
        for (var blockNo = 3; blockNo < 4096; blockNo++)
        {
            var offset = partitionOffset + blockNo * 512L;
            stream.Position = offset;
            _ = await stream.ReadAsync(data, 0, data.Length);
            if (BigEndianConverter.ConvertBytesToUInt16(data) == Constants.DBLKID &&
                BigEndianConverter.ConvertBytesToUInt32(data, 12) == Constants.ANODE_ROOTDIR &&
                data[20] != 0)
            {
                return offset;
            }
        }

        throw new IOException("Root dir block not found");
    }
}
