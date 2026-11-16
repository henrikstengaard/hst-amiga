namespace Hst.Amiga.Tests.VmTests;

using System.Collections.Generic;
using Vm.Libraries.Dos;
using Xunit;

public class GivenReadArgsParser
{
    [Fact]
    public void When_ParsingPositionalKeywordAndSwitchArguments_Then_ValuesAreSet()
    {
        var result = ReadArgsParser.Parse("FROM/A,TO/K,ALL/S,QUIET/S", "file.txt TO ram:out ALL\n");

        Assert.Equal(0, result.Error);
        Assert.Equal("file.txt", result.Values[0]);
        Assert.Equal("ram:out", result.Values[1]);
        Assert.Equal(true, result.Values[2]);
        Assert.Null(result.Values[3]);
    }

    [Fact]
    public void When_ParsingKeywordWithEqualSign_Then_ValueIsSet()
    {
        var result = ReadArgsParser.Parse("FILE=F/A,NUM/N", "f=test.txt NUM=42\n");

        Assert.Equal(0, result.Error);
        Assert.Equal("test.txt", result.Values[0]);
        Assert.Equal(42, result.Values[1]);
    }

    [Fact]
    public void When_ParsingQuotedArgumentWithEscapes_Then_QuotesAndEscapesAreRemoved()
    {
        var result = ReadArgsParser.Parse("TEXT/A", "\"hello *\"world*\"*N\"\n");

        Assert.Equal(0, result.Error);
        Assert.Equal("hello \"world\"\n", result.Values[0]);
    }

    [Fact]
    public void When_ParsingMultipleArgumentsBeforeRequiredArgument_Then_LastValueIsTakenByRequiredArgument()
    {
        var result = ReadArgsParser.Parse("FROM/A/M,TO/A", "a b c dest\n");

        Assert.Equal(0, result.Error);
        Assert.Equal(new List<string> { "a", "b", "c" }, result.Values[0]);
        Assert.Equal("dest", result.Values[1]);
    }

    [Fact]
    public void When_ParsingRestOfLine_Then_RemainingLineIsValue()
    {
        var result = ReadArgsParser.Parse("NAME/A,TEXT/F", "first the rest of line\n");

        Assert.Equal(0, result.Error);
        Assert.Equal("first", result.Values[0]);
        Assert.Equal("the rest of line", result.Values[1]);
    }

    [Theory]
    [InlineData("FILE/A", "\n", DosErrors.RequiredArgMissing)]
    [InlineData("FILE/A", "a b\n", DosErrors.TooManyArgs)]
    [InlineData("NUM/N", "abc\n", DosErrors.BadNumber)]
    [InlineData("TO/K", "TO\n", DosErrors.KeyNeedsArg)]
    [InlineData("FILE", "\"abc\n", DosErrors.UnmatchedQuotes)]
    public void When_ParsingInvalidArguments_Then_ErrorIsReturned(string template, string arguments,
        int expectedError)
    {
        var result = ReadArgsParser.Parse(template, arguments);

        Assert.Equal(expectedError, result.Error);
    }
}
