namespace Hst.Amiga.Tests.Pfs3Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FileSystems.Exceptions;
using FileSystems.Pfs3;
using Xunit;

public class GivenPfs3MaxFileNameLength : Pfs3TestBase
{
    private const string LongName =
        "1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890.txt";

    [Fact]
    public async Task WhenMountingFormattedVolumeThenMaxFileNameLengthIs106()
    {
        var stream = await CreatePfs3FormattedDisk();
        await using var volume = await MountVolume(stream);

        Assert.Equal(106, volume.MaxFileNameLength);
    }

    [Fact]
    public async Task WhenSettingMaxFileNameLengthThenItIsPersistedAfterRemount()
    {
        var stream = await CreatePfs3FormattedDisk();
        await using (var volume = await MountVolume(stream))
        {
            await volume.SetMaxFileNameLength(40);
            Assert.Equal(40, volume.MaxFileNameLength);
        }

        await using (var volume = await MountVolume(stream))
        {
            Assert.Equal(40, volume.MaxFileNameLength);
        }
    }

    [Fact]
    public async Task WhenSettingMaxFileNameLengthThenNewNamesAreTruncatedFromTheStart()
    {
        var stream = await CreatePfs3FormattedDisk();
        await using var volume = await MountVolume(stream);
        await volume.SetMaxFileNameLength(40);

        await volume.CreateFile(LongName);
        await volume.CreateDirectory("Dir" + LongName);

        var names = (await volume.ListEntries()).Select(x => x.Name).OrderBy(x => x).ToList();
        Assert.Equal(new[] { LongName.Substring(0, 40), ("Dir" + LongName).Substring(0, 40) }, names);

        // full name finds the truncated entry
        var result = await volume.FindEntry(LongName);
        Assert.NotNull(result.Entry);
        Assert.Equal(LongName.Substring(0, 40), result.Entry.Name);
    }

    [Fact]
    public async Task WhenIncreasingMaxFileNameLengthThenLongNamesAreKept()
    {
        var stream = await CreatePfs3FormattedDisk();
        await using (var volume = await MountVolume(stream))
        {
            await volume.SetMaxFileNameLength(31);
            await volume.SetMaxFileNameLength(Pfs3Volume.MaxSupportedMaxFileNameLength);
            await volume.CreateFile(LongName);
        }

        await using (var volume = await MountVolume(stream))
        {
            Assert.Equal(107, volume.MaxFileNameLength);
            var entry = Assert.Single(await volume.ListEntries());
            Assert.Equal(LongName, entry.Name);
        }
    }

    [Fact]
    public async Task WhenLoweringMaxFileNameLengthBelowExistingNameThenExceptionIsThrown()
    {
        var stream = await CreatePfs3FormattedDisk();
        await using var volume = await MountVolume(stream);
        await volume.CreateDirectory("Dir1");
        await volume.ChangeDirectory("Dir1");
        await volume.CreateFile(LongName.Substring(0, 50));
        await volume.ChangeDirectory("/");

        var exception = await Assert.ThrowsAsync<FileNameTooLongException>(() => volume.SetMaxFileNameLength(40));

        Assert.Equal("/Dir1/" + LongName.Substring(0, 50), exception.Path);
        Assert.Equal(50, exception.NameLength);
        Assert.Equal(106, volume.MaxFileNameLength);
    }

    [Fact]
    public async Task WhenLoweringMaxFileNameLengthBelowExistingNameWithForceThenLimitIsChanged()
    {
        var stream = await CreatePfs3FormattedDisk();
        await using var volume = await MountVolume(stream);
        await volume.CreateFile(LongName.Substring(0, 50));

        await volume.SetMaxFileNameLength(40, force: true);

        Assert.Equal(40, volume.MaxFileNameLength);
        var entry = Assert.Single(await volume.ListEntries());
        Assert.Equal(LongName.Substring(0, 50), entry.Name);
    }

    [Fact]
    public async Task WhenLoweringMaxFileNameLengthToLongestExistingNameThenLimitIsChanged()
    {
        var stream = await CreatePfs3FormattedDisk();
        await using var volume = await MountVolume(stream);
        await volume.CreateDirectory("Dir1");
        await volume.ChangeDirectory("Dir1");
        await volume.CreateFile(LongName.Substring(0, 50));
        await volume.ChangeDirectory("/");

        await volume.SetMaxFileNameLength(50);

        Assert.Equal(50, volume.MaxFileNameLength);
    }

    [Fact]
    public async Task WhenLoweringMaxFileNameLengthThenScanProgressIsReportedPerDirectory()
    {
        var stream = await CreatePfs3FormattedDisk();
        await using var volume = await MountVolume(stream);
        await volume.CreateFile("File1");
        await volume.CreateDirectory("Dir1");
        await volume.ChangeDirectory("Dir1");
        await volume.CreateFile("File2");
        await volume.CreateFile("File3");
        await volume.CreateDirectory("Dir2");
        await volume.ChangeDirectory("Dir2");
        await volume.CreateFile("File4");
        await volume.ChangeDirectory("/");

        var progress = new CollectingProgress();
        await volume.SetMaxFileNameLength(40, progress: progress);

        Assert.Equal(new[] { 1, 2, 3 }, progress.Reports.Select(x => x.DirectoriesScanned));
        Assert.Equal(new[] { "/", "/Dir1", "/Dir1/Dir2" }, progress.Reports.Select(x => x.CurrentPath));
        Assert.Equal(6, progress.Reports.Last().EntriesScanned);
    }

    [Theory]
    [InlineData(50, false)]
    [InlineData(107, false)]
    [InlineData(50, true)]
    public async Task WhenNotScanningThenNoProgressIsReported(int maxFileNameLength, bool force)
    {
        var stream = await CreatePfs3FormattedDisk();
        await using var volume = await MountVolume(stream);
        await volume.CreateFile("File1");
        if (!force)
        {
            // lower once, so setting the same limit or raising it does not scan
            await volume.SetMaxFileNameLength(50);
        }

        var progress = new CollectingProgress();
        await volume.SetMaxFileNameLength(maxFileNameLength, force, progress);

        Assert.Empty(progress.Reports);
    }

    private class CollectingProgress : IProgress<FileNameScanProgress>
    {
        public List<FileNameScanProgress> Reports { get; } = new();

        public void Report(FileNameScanProgress value) => Reports.Add(value);
    }

    [Theory]
    [InlineData(29)]
    [InlineData(108)]
    public async Task WhenSettingMaxFileNameLengthOutOfRangeThenExceptionIsThrown(int maxFileNameLength)
    {
        var stream = await CreatePfs3FormattedDisk();
        await using var volume = await MountVolume(stream);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => volume.SetMaxFileNameLength(maxFileNameLength));
        Assert.Equal(106, volume.MaxFileNameLength);
    }
}
