namespace Hst.Amiga.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileSystems;
using FileSystems.FastFileSystem;
using FileSystems.Pfs3;
using FileSystems.SmartFileSystem;
using Xunit;
using static FileSystemVolumeTestHelper;
using FileMode = FileSystems.FileMode;

public class GivenFileSystemVolumeWithCancelledToken
{
    private const string ExistingFile = "File";
    private const string NewEntry = "New";

    private static readonly IReadOnlyDictionary<string, Func<IFileSystemVolume, CancellationToken, Task>>
        Operations = new Dictionary<string, Func<IFileSystemVolume, CancellationToken, Task>>
        {
            ["ListEntries"] = (v, ct) => v.ListEntries(ct),
            ["FindEntry"] = (v, ct) => v.FindEntry(ExistingFile, ct),
            ["ChangeDirectory"] = (v, ct) => v.ChangeDirectory("/", ct),
            ["CreateDirectory"] = (v, ct) => v.CreateDirectory(NewEntry, ct),
            ["CreateFile"] = (v, ct) => v.CreateFile(NewEntry, false, false, ct),
            ["CreateLink"] = (v, ct) => v.CreateLink(NewEntry, ExistingFile, false, false, ct),
            ["OpenFile"] = (v, ct) => v.OpenFile(ExistingFile, FileMode.Read, false, false, ct),
            ["Rename"] = (v, ct) => v.Rename(ExistingFile, NewEntry, ct),
            ["Delete"] = (v, ct) => v.Delete(ExistingFile, false, ct),
            ["SetComment"] = (v, ct) => v.SetComment(ExistingFile, "Comment", ct),
            ["SetProtectionBits"] = (v, ct) => v.SetProtectionBits(ExistingFile, ProtectionBits.Read, ct),
            ["SetDate"] = (v, ct) => v.SetDate(ExistingFile, new DateTime(2020, 1, 1), ct),
            ["Flush"] = (v, ct) => v.Flush(ct),
            ["GetCurrentPath"] = (v, ct) => v.GetCurrentPath(ct),
        };

    public static IEnumerable<object[]> FileSystemsAndOperations =>
        from fileSystem in FileSystems
        from operation in Operations.Keys
        select new object[] { fileSystem, operation };

    [Theory]
    [MemberData(nameof(FileSystemsAndOperations))]
    public async Task WhenOperationIsCalledWithCancelledTokenThenOperationIsCancelledAndVolumeIsUnchanged(
        string fileSystem, string operation)
    {
        // arrange - formatted disk with one file in root directory
        var stream = new MemoryStream();
        await CreateFormattedDisk(fileSystem, stream);
        await using (var volume = await MountVolume(fileSystem, stream))
        {
            await volume.CreateFile(ExistingFile, false, false);
        }

        // act - call operation with cancelled token
        await using (var volume = await MountVolume(fileSystem, stream))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                Operations[operation](volume, new CancellationToken(true)));
        }

        // assert - root directory only contains the existing file with unchanged comment
        await using (var volume = await MountVolume(fileSystem, stream))
        {
            var entries = (await volume.ListEntries()).ToList();
            var entry = Assert.Single(entries);
            Assert.Equal(ExistingFile, entry.Name);
            Assert.Equal(EntryType.File, entry.Type);
            Assert.True(string.IsNullOrEmpty(entry.Comment));
        }
    }

    [Theory]
    [InlineData(Ffs)]
    [InlineData(Pfs3)]
    [InlineData(Sfs)]
    public async Task WhenFormatPartitionWithCancelledTokenThenFormatIsCancelledAndNothingIsWritten(
        string fileSystem)
    {
        // arrange - disk with rigid disk block and unformatted partition
        var stream = new MemoryStream();
        var partitionBlock = await CreateDisk(fileSystem, stream);
        var bytesBefore = stream.ToArray();

        // act - format partition with cancelled token
        var cancellationToken = new CancellationToken(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fileSystem switch
        {
            Ffs => FastFileSystemFormatter.FormatPartition(stream, partitionBlock, "Workbench",
                cancellationToken: cancellationToken),
            Pfs3 => Pfs3Formatter.FormatPartition(stream, partitionBlock, "Workbench", cancellationToken),
            _ => SmartFileSystemFormatter.FormatPartition(stream, partitionBlock, "Workbench",
                cancellationToken: cancellationToken)
        });

        // assert - disk is unchanged
        Assert.Equal(bytesBefore, stream.ToArray());
    }
}
