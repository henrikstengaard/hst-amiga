namespace Hst.Amiga.Tests.VmTests;

using Vm.Libraries.Dos;
using Xunit;

public class GivenAmigaPattern
{
    [Theory]
    [InlineData("#?", "anything", true)]
    [InlineData("#?.info", "disk.info", true)]
    [InlineData("#?.info", "disk.txt", false)]
    [InlineData("a?c", "abc", true)]
    [InlineData("a?c", "ac", false)]
    [InlineData("(foo|bar).c", "bar.c", true)]
    [InlineData("(foo|bar).c", "baz.c", false)]
    [InlineData("~(#?.info)", "readme", true)]
    [InlineData("~(#?.info)", "readme.info", false)]
    [InlineData("[a-c]#?", "beta", true)]
    [InlineData("[~a-c]#?", "beta", false)]
    [InlineData("#a", "aaa", true)]
    [InlineData("#a", "", true)]
    [InlineData("'?", "?", true)]
    [InlineData("'?", "a", false)]
    [InlineData("file", "FILE", true)]
    [InlineData("a.b", "a.b", true)]
    [InlineData("a.b", "axb", false)]
    public void When_MatchingPatternIgnoringCase_Then_ResultIsAsExpected(string pattern, string value,
        bool expected)
    {
        Assert.Equal(expected, new AmigaPattern(pattern, true).IsMatch(value));
    }

    [Fact]
    public void When_MatchingPatternWithCase_Then_CaseIsCompared()
    {
        Assert.False(new AmigaPattern("file", false).IsMatch("FILE"));
    }

    [Theory]
    [InlineData("#?.c", true)]
    [InlineData("file.c", false)]
    [InlineData("'?", false)]
    public void When_CheckingPatternIsWild_Then_WildcardsAreDetected(string pattern, bool expected)
    {
        Assert.Equal(expected, AmigaPattern.IsWild(pattern));
    }
}
