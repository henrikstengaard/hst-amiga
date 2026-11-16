namespace Hst.Amiga.Tests.VmTests;

using System;
using System.IO;
using System.Linq;
using DataTypes.UaeFsDbs;
using Vm.FileSystem;
using Xunit;

public class GivenHostMetadata : IDisposable
{
    // fib protection with pure bit set and execute not allowed
    private const uint PureNotExecutable = 0x20 | 0x02;

    private readonly string path;

    public GivenHostMetadata()
    {
        path = Path.Combine(Path.GetTempPath(), $"hst-amiga-vm-metadata-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
    }

    public void Dispose()
    {
        Directory.Delete(path, true);
    }

    private string HostPath(string name) => Path.Combine(path, name);

    private void CreateUaeFsDb(params UaeFsDbNode[] nodes) =>
        File.WriteAllBytes(HostPath(HostMetadata.UaeFsDbFileName),
            nodes.SelectMany(x => UaeFsDbWriter.Build(x)).ToArray());

    private static UaeFsDbNode Node(string amigaName, string normalName, uint mode, string comment) =>
        new() { AmigaName = amigaName, NormalName = normalName, Mode = mode, Comment = comment };

    [Fact]
    public void When_ReadingUaeFsDb_Then_AmigaNamesProtectionAndCommentsAreUsed()
    {
        File.WriteAllText(HostPath("__uae___file_"), "data");
        File.WriteAllText(HostPath("plain"), "data");
        CreateUaeFsDb(Node("file*", "__uae___file_", PureNotExecutable, "a comment"));
        var metadata = new HostMetadata(UaeMetadataMode.Auto);

        var entries = metadata.List(path).OrderBy(x => x.AmigaName).ToList();
        var file = metadata.Get(HostPath("__uae___file_"));

        Assert.Equal(new[] { "file*", "plain" }, entries.Select(x => x.AmigaName));
        Assert.Equal("__uae___file_", metadata.Find(path, "FILE*").HostName);
        Assert.Equal(PureNotExecutable, file.Protection);
        Assert.Equal("a comment", file.Comment);
        Assert.Equal(0U, metadata.Get(HostPath("plain")).Protection);
    }

    [Fact]
    public void When_ReadingUaeFsDbWith68Nodes_Then_AllNodesAreReadAsVersion1()
    {
        // 68 version 1 nodes is 40800 bytes, which is also a multiple of version 2 node size
        CreateUaeFsDb(Enumerable.Range(1, 68)
            .Select(i => Node($"file{i}*", $"__uae___file{i}_", (uint)i, $"comment {i}")).ToArray());
        File.WriteAllText(HostPath("__uae___file68_"), "data");
        var metadata = new HostMetadata(UaeMetadataMode.Auto);

        var file = metadata.Get(HostPath("__uae___file68_"));

        Assert.Equal("file68*", metadata.List(path).Single().AmigaName);
        Assert.Equal(68U, file.Protection);
        Assert.Equal("comment 68", file.Comment);
    }

    [Fact]
    public void When_WritingIsDisabled_Then_ChangesAreKeptInMemoryAndUaeFsDbIsNotChanged()
    {
        File.WriteAllText(HostPath("file"), "data");
        CreateUaeFsDb(Node("file", "file", 0, string.Empty));
        var before = File.ReadAllBytes(HostPath(HostMetadata.UaeFsDbFileName));
        var metadata = new HostMetadata(UaeMetadataMode.Auto);

        metadata.Set(HostPath("file"), "file", x =>
        {
            x.Protection = PureNotExecutable;
            x.Comment = "changed";
        });

        Assert.Equal(PureNotExecutable, metadata.Get(HostPath("file")).Protection);
        Assert.Equal("changed", metadata.Get(HostPath("file")).Comment);
        Assert.Equal(before, File.ReadAllBytes(HostPath(HostMetadata.UaeFsDbFileName)));
        Assert.Equal(0U, new HostMetadata(UaeMetadataMode.Auto).Get(HostPath("file")).Protection);
    }

    [Fact]
    public void When_WritingIsEnabled_Then_ChangesAreWrittenToUaeFsDb()
    {
        File.WriteAllText(HostPath("file"), "data");
        CreateUaeFsDb(Node("other", "other", 0, string.Empty));
        var metadata = new HostMetadata(UaeMetadataMode.Auto, true);

        metadata.Set(HostPath("file"), "file", x =>
        {
            x.Protection = PureNotExecutable;
            x.Comment = "written";
        });

        var reread = new HostMetadata(UaeMetadataMode.Auto).Get(HostPath("file"));
        Assert.Equal(PureNotExecutable, reread.Protection);
        Assert.Equal("written", reread.Comment);
        Assert.Equal(2 * Constants.UaeFsDbNodeVersion1Size,
            new FileInfo(HostPath(HostMetadata.UaeFsDbFileName)).Length);
    }

    [Fact]
    public void When_ReadingUaeMetafile_Then_ProtectionDateCommentAndEncodedNamesAreUsed()
    {
        File.WriteAllText(HostPath("file"), "data");
        File.WriteAllText(HostPath("file.uaem"), "--p-rw-d 2020-01-02 03:04:05.00 hello\n");
        File.WriteAllText(HostPath("a%2ab"), "data");
        var metadata = new HostMetadata(UaeMetadataMode.Auto);

        var file = metadata.Get(HostPath("file"));

        Assert.Equal(new[] { "a*b", "file" }, metadata.List(path).Select(x => x.AmigaName).OrderBy(x => x));
        Assert.Equal(PureNotExecutable, file.Protection);
        Assert.Equal("hello", file.Comment);
        Assert.Equal(new DateTime(2020, 1, 2, 3, 4, 5), file.Date);
    }

    [Fact]
    public void When_RenamingWithUaeMetafileWritingEnabled_Then_MetafileIsMovedWithDate()
    {
        File.WriteAllText(HostPath("file"), "data");
        File.WriteAllText(HostPath("file.uaem"), "--p-rw-d 2020-01-02 03:04:05.00 hello\n");
        var metadata = new HostMetadata(UaeMetadataMode.Auto, true);
        var newHostName = metadata.CreateHostName(path, "new*");
        var before = metadata.Get(HostPath("file"));

        File.Move(HostPath("file"), HostPath(newHostName));
        metadata.Renamed(HostPath("file"), HostPath(newHostName), "new*", before);

        Assert.Equal("new%2a", newHostName);
        Assert.False(File.Exists(HostPath("file.uaem")));
        Assert.False(File.Exists(HostPath(HostMetadata.UaeFsDbFileName)));
        Assert.StartsWith("--p-rw-d 2020-01-02 03:04:05.00 hello", File.ReadAllText(HostPath("new%2a.uaem")));
    }

    [Fact]
    public void When_DirectoryHasNoMetadata_Then_FormatOfParentDirectoryIsUsed()
    {
        Directory.CreateDirectory(HostPath("sub"));
        File.WriteAllText(HostPath("file.uaem"), "----rwed 2020-01-02 03:04:05.00 \n");
        var metadata = new HostMetadata(UaeMetadataMode.Auto);
        metadata.AddRoot(path);

        Assert.Equal("a%2a", metadata.CreateHostName(HostPath("sub"), "a*"));
    }

    [Fact]
    public void When_CreatingFileWithSpecialCharsInUaeFsDb_Then_NodeMapsHostNameToAmigaName()
    {
        var metadata = new HostMetadata(UaeMetadataMode.UaeFsDb, true);
        var hostName = metadata.CreateHostName(path, "file<1>");
        File.WriteAllText(HostPath(hostName), "data");

        metadata.Created(HostPath(hostName), "file<1>");

        Assert.Equal("__uae___file_1_", hostName);
        Assert.Equal("file<1>", new HostMetadata(UaeMetadataMode.Auto).List(path).Single().AmigaName);
    }

    [Fact]
    public void When_DeletingWithWritingEnabled_Then_NodeIsRemoved()
    {
        File.WriteAllText(HostPath("file"), "data");
        CreateUaeFsDb(Node("file", "file", PureNotExecutable, string.Empty));
        var metadata = new HostMetadata(UaeMetadataMode.Auto, true);

        File.Delete(HostPath("file"));
        metadata.Deleted(HostPath("file"));

        Assert.Equal(0, new FileInfo(HostPath(HostMetadata.UaeFsDbFileName)).Length);
    }

    [Fact]
    public void When_MetadataIsNone_Then_MetadataFilesAreListedAndNamesAreNotMapped()
    {
        File.WriteAllText(HostPath("__uae___file_"), "data");
        CreateUaeFsDb(Node("file*", "__uae___file_", PureNotExecutable, "a comment"));
        var metadata = new HostMetadata();

        Assert.Equal(new[] { HostMetadata.UaeFsDbFileName, "__uae___file_" },
            metadata.List(path).Select(x => x.AmigaName).OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(0U, metadata.Get(HostPath("__uae___file_")).Protection);
        Assert.False(metadata.IsValidName("file*"));
        Assert.True(new HostMetadata(UaeMetadataMode.Auto).IsValidName("file*"));
    }

    [Theory]
    [InlineData(0x00U, "----rwed")]
    [InlineData(0x20U | 0x02U, "--p-rw-d")]
    [InlineData(0xF0U | 0x0FU, "hspa----")]
    public void When_ConvertingProtectionText_Then_BitsAreConvertedBothWays(uint protection, string text)
    {
        Assert.Equal(text, HostMetadata.ToProtectionText(protection));
        Assert.Equal(protection, HostMetadata.FromProtectionText(text));
    }
}
