namespace Hst.Amiga.Tests.CommandTests;

using System;
using ConsoleApp.Commands;
using Vm.FileSystem;
using Vm.Processor;
using Xunit;

public class GivenVmRunCommand
{
    [Fact]
    public void When_JoiningArguments_Then_ArgumentsWithSpacesAndQuotesAreQuoted()
    {
        var arguments = VmRunCommand.JoinArguments(new[] { "file", "big world", "say \"hi\"", "TO=ram:", "" });

        Assert.Equal("file \"big world\" \"say *\"hi*\"\" TO=ram: \"\"", arguments);
    }

    [Fact]
    public void When_CreatingOptions_Then_VolumesAssignsAndCpuAreSet()
    {
        var options = VmRunCommand.CreateOptions("68000", 2, 4096, new[] { "SYS=c:\\amiga", "Work:=d:\\work" },
            new[] { "C=SYS:C" }, new[] { "icon.library" }, "SYS:Work", 1000, false, false);

        Assert.Equal(CpuType.M68000, options.CpuType);
        Assert.Equal(2u * 1024 * 1024, options.MemorySize);
        Assert.Equal(4096u, options.StackSize);
        Assert.Equal("c:\\amiga", options.Volumes["SYS"]);
        Assert.Equal("d:\\work", options.Volumes["Work"]);
        Assert.Equal("SYS:C", options.Assigns["C"]);
        Assert.Contains("icon.library", options.StubLibraries);
        Assert.Contains("intuition.library", options.StubLibraries);
        Assert.Equal("SYS:Work", options.CurrentDirectory);
        Assert.Equal(1000, options.MaxInstructions);
    }

    [Theory]
    [InlineData(new[] { "C=DH0:" }, "DH0:")]
    [InlineData(new[] { "C=+DH0:" }, "+DH0:")]
    [InlineData(new[] { "C=SYS:C", "C=+DH0:" }, "SYS:C;DH0:")]
    [InlineData(new[] { "C=+DH0:", "C=+DH1:" }, "+DH0:;DH1:")]
    [InlineData(new[] { "C=+DH0:", "C=DH1:" }, "DH1:")]
    public void When_CreatingOptionsWithAssignsStartingWithPlus_Then_PathsAreAddedToAssign(string[] assigns,
        string expected)
    {
        var options = VmRunCommand.CreateOptions(null, null, null, null, assigns, null, null, null, false, false);

        Assert.Equal(expected, options.Assigns["C"]);
    }

    [Fact]
    public void When_CreatingOptionsWithOnlyPlus_Then_ArgumentExceptionIsThrown()
    {
        Assert.Throws<ArgumentException>(() =>
            VmRunCommand.CreateOptions(null, null, null, null, new[] { "C=+" }, null, null, null, false, false));
    }

    [Theory]
    [InlineData(null, UaeMetadataMode.Auto)]
    [InlineData("uaefsdb", UaeMetadataMode.UaeFsDb)]
    [InlineData("uaemetafile", UaeMetadataMode.UaeMetafile)]
    [InlineData("none", UaeMetadataMode.None)]
    public void When_CreatingOptionsWithUaeMetadata_Then_UaeMetadataIsSet(string uaeMetadata,
        UaeMetadataMode expected)
    {
        var options = VmRunCommand.CreateOptions("68020", null, null, null, null, null, null, null, false, false,
            uaeMetadata, true);

        Assert.Equal(expected, options.UaeMetadata);
        Assert.True(options.WriteUaeMetadata);
    }

    [Fact]
    public void When_CreatingOptionsWithInvalidVolume_Then_ArgumentExceptionIsThrown()
    {
        Assert.Throws<ArgumentException>(() => VmRunCommand.CreateOptions("68020", null, null,
            new[] { "SYS" }, null, null, null, null, false, false));
    }
}
