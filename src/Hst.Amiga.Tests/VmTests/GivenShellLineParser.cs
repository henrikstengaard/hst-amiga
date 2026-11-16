namespace Hst.Amiga.Tests.VmTests;

using System.Collections.Generic;
using Vm.Libraries.Dos;
using Xunit;

public class GivenShellLineParser
{
    [Theory]
    [InlineData("echo hello ; comment", "echo hello ")]
    [InlineData("echo \"a;b\" ; comment", "echo \"a;b\" ")]
    [InlineData("echo \"a*\";b\"", "echo \"a*\";b\"")]
    [InlineData("; only comment", "")]
    public void When_StrippingComment_Then_CommentOutsideQuotesIsRemoved(string line, string expected)
    {
        Assert.Equal(expected, ShellLineParser.StripComment(line));
    }

    [Theory]
    [InlineData("echo $x", "echo 5")]
    [InlineData("echo ${x}y", "echo 5y")]
    [InlineData("echo $unknown", "echo $unknown")]
    [InlineData("echo *$x", "echo $x")]
    [InlineData("echo \"*$x\"", "echo \"*$x\"")]
    [InlineData("echo \"$x\"", "echo \"5\"")]
    [InlineData("echo $", "echo $")]
    public void When_ExpandingVariables_Then_DefinedVariablesAreReplaced(string line, string expected)
    {
        var variables = new Dictionary<string, string> { ["x"] = "5" };

        var result = ShellLineParser.ExpandVariables(line, name => variables.TryGetValue(name, out var v) ? v : null);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void When_SubstitutingBackquotes_Then_OutputReplacesCommand()
    {
        var result = ShellLineParser.SubstituteBackquotes("echo `date` and `ver`",
            command => command == "date" ? "today\n" : "v40\nline2\n");

        Assert.Equal("echo today and v40 line2", result);
    }

    [Fact]
    public void When_ParsingRedirectionAfterCommand_Then_FilesAndArgumentsAreParsed()
    {
        var result = ShellLineParser.Parse("list >>ram:out <ram:in sys: all");

        Assert.Equal("list", result.Command);
        Assert.Equal("ram:out", result.OutputFile);
        Assert.True(result.AppendOutput);
        Assert.Equal("ram:in", result.InputFile);
        Assert.Equal("sys: all", result.Arguments);
    }

    [Fact]
    public void When_ParsingRedirectionWithSpaceAndQuotedCommand_Then_FileIsParsed()
    {
        var result = ShellLineParser.Parse("\"my command\" > \"ram:my file\" arg");

        Assert.Equal("my command", result.Command);
        Assert.Equal("ram:my file", result.OutputFile);
        Assert.False(result.AppendOutput);
        Assert.Equal("arg", result.Arguments);
    }

    [Fact]
    public void When_ParsingRedirectionAfterArguments_Then_RedirectionIsPartOfArguments()
    {
        var result = ShellLineParser.Parse("eval 1 + 2 >ram:out");

        Assert.Null(result.OutputFile);
        Assert.Equal("1 + 2 >ram:out", result.Arguments);
    }
}
