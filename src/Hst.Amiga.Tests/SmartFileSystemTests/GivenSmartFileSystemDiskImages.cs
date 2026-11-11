namespace Hst.Amiga.Tests.SmartFileSystemTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DiscUtils;
using DiscUtils.Streams;
using FileSystems;
using FileSystems.SmartFileSystem;
using Xunit;

/// <summary>
/// Tests reading and writing smart file system disk images created and formatted with
/// AmigaOS 3.1, AmigaOS 4.1 Final Edition and AROS One x86.
/// Each operating system has an empty disk image and a disk image with following data:
/// dir1/file1 (with comment and script bit), dir1/link1 (soft link to file1) and dir2/file2.
/// AROS One x86 disk images use a MBR with an extended partition containing a logical partition of type 0x30,
/// which contains a rigid disk block.
/// </summary>
public class GivenSmartFileSystemDiskImages : SmartFileSystemTestBase
{
    private const byte MbrExtendedPartitionType = 0x05;
    private const byte MbrExtendedLbaPartitionType = 0x0f;
    private const byte MbrAmigaRdbPartitionType = 0x30;

    public GivenSmartFileSystemDiskImages()
    {
        DiscUtilsHelper.Setup();
    }

    public static IEnumerable<object[]> EmptyImages =>
    [
        ["amigaos_3.1_sfs0_empty.vhd"],
        ["amigaos_4.1_sfs0_empty.vhd"],
        ["aros_one_x86_sfs0_empty.vhd"]
    ];

    public static IEnumerable<object[]> DataImages =>
    [
        ["amigaos_3.1_sfs0_data.vhd"],
        ["amigaos_4.1_sfs0_data.vhd"],
        ["aros_one_x86_sfs0_data.vhd"]
    ];

    public static IEnumerable<object[]> AllImages => EmptyImages.Concat(DataImages);

    public static IEnumerable<object[]> AllImagesWithVolumeName =>
    [
        ["amigaos_3.1_sfs0_empty.vhd", "Workbench"],
        ["amigaos_3.1_sfs0_data.vhd", "Workbench"],
        ["amigaos_4.1_sfs0_empty.vhd", "Workbench"],
        ["amigaos_4.1_sfs0_data.vhd", "Workbench"],
        ["aros_one_x86_sfs0_empty.vhd", "Workbench"],
        ["aros_one_x86_sfs0_data.vhd", "AROS"]
    ];

    [Theory]
    [MemberData(nameof(AllImagesWithVolumeName))]
    public async Task When_MountingImage_Then_VolumeIsValid(string imageFile, string expectedVolumeName)
    {
        // arrange - open disk image
        using var diskImage = DiskImage.Open(GetImagePath(imageFile));
        var stream = diskImage.Stream;
        var partitionBlock = await ReadPartitionBlock(stream);

        // act - mount volume
        var volume = await SmartFileSystemVolume.Mount(stream, partitionBlock);

        // assert - volume is valid
        Assert.Equal(expectedVolumeName, volume.Name);
        Assert.True(volume.Free > 0);
        Assert.True(volume.Free < volume.Size);
        var errors = await SmartFileSystemVerifier.Verify(volume.Volume, partitionBlock.PreAlloc);
        Assert.Empty(errors);
    }

    [Theory]
    [MemberData(nameof(EmptyImages))]
    public async Task When_ListingEntriesInEmptyImage_Then_OnlyHiddenRecycledDirectoryExists(string imageFile)
    {
        // arrange - open disk image and mount volume
        using var diskImage = DiskImage.Open(GetImagePath(imageFile));
        var stream = diskImage.Stream;
        var volume = await MountVolume(stream);

        // act - list entries
        var entries = (await volume.ListEntries()).ToList();
        var rawEntries = (await volume.ListRawEntries()).ToList();

        // assert - no entries are listed and only hidden recycled directory exists
        Assert.Empty(entries);
        var rawEntry = Assert.Single(rawEntries);
        Assert.Equal(".recycled", rawEntry.Object.Name);
        Assert.True(rawEntry.Object.IsHidden);
    }

    [Theory]
    [MemberData(nameof(DataImages))]
    public async Task When_ReadingEntriesInDataImage_Then_EntriesAndDataAreReadCorrectly(string imageFile)
    {
        // arrange - open disk image and mount volume
        using var diskImage = DiskImage.Open(GetImagePath(imageFile));
        var stream = diskImage.Stream;
        var volume = await MountVolume(stream);

        // act & assert - root contains dir1 and dir2
        var rootEntries = (await volume.ListEntries()).OrderBy(x => x.Name).ToList();
        Assert.Equal(["dir1", "dir2"], rootEntries.Select(x => x.Name));
        Assert.All(rootEntries, x => Assert.Equal(EntryType.Dir, x.Type));

        // act & assert - dir1 contains file1 and link1
        await volume.ChangeDirectory("dir1");
        var dir1Entries = (await volume.ListEntries()).OrderBy(x => x.Name).ToList();
        Assert.Equal(["file1", "link1"], dir1Entries.Select(x => x.Name));

        var file1 = dir1Entries[0];
        Assert.Equal(EntryType.File, file1.Type);
        Assert.Equal(20, file1.Size);
        Assert.Equal("a comment", file1.Comment);
        Assert.True(file1.ProtectionBits.HasFlag(ProtectionBits.Script));
        Assert.Equal("this is a test file\n", Encoding.ASCII.GetString(await ReadFile(volume, "file1")));

        var link1 = dir1Entries[1];
        Assert.Equal(EntryType.SoftLink, link1.Type);
        Assert.Equal("file1", link1.LinkPath);

        // act & assert - dir2 contains file2
        await volume.ChangeDirectory("/dir2");
        var dir2Entries = (await volume.ListEntries()).ToList();
        var file2 = Assert.Single(dir2Entries);
        Assert.Equal("file2", file2.Name);
        Assert.Equal(EntryType.File, file2.Type);
        Assert.Equal(14, file2.Size);
        Assert.Equal("2nd test file\n", Encoding.ASCII.GetString(await ReadFile(volume, "file2")));
    }

    [Theory]
    [MemberData(nameof(AllImages))]
    public async Task When_WritingToImage_Then_VolumeIsValidAndDataIsReadCorrectly(string imageFile)
    {
        // arrange - copy disk image to temp file to keep test data unchanged
        var imagePath = CopyImageToTemp(imageFile);
        var data1 = CreateTestData(50000, 1);
        var data2 = CreateTestData(1500, 2);

        try
        {
            // act - write directory and files to volume
            using (var diskImage = DiskImage.Open(imagePath, true))
            {
                var stream = diskImage.Stream;
                await using var volume = await MountVolume(stream);
                await volume.CreateDirectory("newdir");
                await volume.ChangeDirectory("newdir");
                await WriteFile(volume, "newfile1.bin", data1);
                await volume.ChangeDirectory("/");
                await WriteFile(volume, "newfile2.bin", data2);
                await volume.SetComment("newfile2.bin", "new comment");
                await volume.Flush();
            }

            // assert - volume is valid and written data is read correctly
            using (var diskImage = DiskImage.Open(imagePath))
            {
                var stream = diskImage.Stream;
                await AssertVolumeIsValid(stream);

                await using var volume = await MountVolume(stream);
                var newFile2 = (await volume.FindEntry("newfile2.bin")).Entry;
                Assert.NotNull(newFile2);
                Assert.Equal("new comment", newFile2.Comment);
                Assert.Equal(data2, await ReadFile(volume, "newfile2.bin"));

                await volume.ChangeDirectory("newdir");
                Assert.Equal(data1, await ReadFile(volume, "newfile1.bin"));
            }
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    private static string GetImagePath(string imageFile) => Path.Combine("TestData", "SmartFileSystem", imageFile);

    private static string CopyImageToTemp(string imageFile)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{imageFile}");
        File.Copy(GetImagePath(imageFile), tempPath);
        return tempPath;
    }

    /// <summary>
    /// Disk image opened with stream positioned at rigid disk block. If disk image has a MBR,
    /// stream is for the first partition of type 0x30 (primary or logical).
    /// </summary>
    private sealed class DiskImage : IDisposable
    {
        private readonly VirtualDisk disk;

        private DiskImage(VirtualDisk disk, Stream stream)
        {
            this.disk = disk;
            Stream = stream;
        }

        public Stream Stream { get; }

        public static DiskImage Open(string path, bool writable = false)
        {
            var disk = VirtualDisk.OpenDisk(path, writable ? FileAccess.ReadWrite : FileAccess.Read);
            var diskStream = disk.Content;

            var mbr = ReadSector(diskStream, 0);
            var rdbPartition = mbr[510] == 0x55 && mbr[511] == 0xaa ? FindRdbPartitionInMbr(diskStream, mbr) : null;

            diskStream.Position = 0;
            return new DiskImage(disk, rdbPartition == null
                ? diskStream
                : new SubStream(diskStream, rdbPartition.Value.Offset, rdbPartition.Value.Size));
        }

        public void Dispose()
        {
            disk.Dispose();
        }
    }

    private static (long Offset, long Size)? FindRdbPartitionInMbr(Stream diskStream, byte[] mbr)
    {
        foreach (var (type, start, size) in ReadMbrPartitions(mbr))
        {
            switch (type)
            {
                case MbrAmigaRdbPartitionType:
                    return (start * 512L, size * 512L);
                case MbrExtendedPartitionType or MbrExtendedLbaPartitionType:
                {
                    // logical partitions start relative to their extended boot record,
                    // next extended boot record starts relative to the extended partition
                    var ebrSector = start;
                    while (ebrSector != 0)
                    {
                        var ebrPartitions = ReadMbrPartitions(ReadSector(diskStream, ebrSector)).ToList();
                        if (ebrPartitions.Count > 0 && ebrPartitions[0].Type == MbrAmigaRdbPartitionType)
                        {
                            return ((ebrSector + ebrPartitions[0].Start) * 512L, ebrPartitions[0].Size * 512L);
                        }

                        ebrSector = ebrPartitions.Count > 1 ? start + ebrPartitions[1].Start : 0;
                    }

                    break;
                }
            }
        }

        return null;
    }

    private static IEnumerable<(byte Type, uint Start, uint Size)> ReadMbrPartitions(byte[] sector)
    {
        for (var i = 0; i < 4; i++)
        {
            var offset = 0x1be + i * 16;
            var type = sector[offset + 4];
            if (type == 0)
            {
                continue;
            }

            yield return (type, BitConverter.ToUInt32(sector, offset + 8), BitConverter.ToUInt32(sector, offset + 12));
        }
    }

    private static byte[] ReadSector(Stream stream, long sector)
    {
        var data = new byte[512];
        stream.Position = sector * 512;
        stream.ReadExactly(data, 0, data.Length);
        return data;
    }
}
