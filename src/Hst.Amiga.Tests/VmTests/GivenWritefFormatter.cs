namespace Hst.Amiga.Tests.VmTests;

using System.Text;
using Vm;
using Vm.Libraries.Dos;
using Xunit;

public class GivenWritefFormatter
{
    private const uint FormatAddress = 0x1000;
    private const uint ArgumentsAddress = 0x2000;
    private const uint StringAddress = 0x3000;
    private const uint BStringAddress = 0x3100;

    private static string Format(Ram ram, string format, params uint[] arguments)
    {
        ram.WriteCString(FormatAddress, format);
        for (var i = 0; i < arguments.Length; i++)
        {
            ram.WriteLong(ArgumentsAddress + (uint)i * 4, arguments[i]);
        }

        return Encoding.ASCII.GetString(WritefFormatter.Format(ram, FormatAddress, ArgumentsAddress));
    }

    [Fact]
    public void When_FormattingNumbers_Then_NumbersAreLimitedToWidth()
    {
        var ram = new Ram(0x10000);

        var result = Format(ram, "%N|%I3|%I9|%X4|%O3|%U5|%N", unchecked((uint)-42), 12345, 7, 0xBEEF, 8,
            0xFFFFFFFF, 0);

        Assert.Equal("-42|123|7|BEEF|10|42949|0", result);
    }

    [Fact]
    public void When_FormattingStringsAndCharacters_Then_ArgumentsAreWritten()
    {
        var ram = new Ram(0x10000);
        ram.WriteCString(StringAddress, "text");
        ram.WriteBString(BStringAddress, "ab");

        var result = Format(ram, "[%S] [%T5] [%C] %$%N%%", StringAddress, BStringAddress >> 2, 'Z', 99, 5);

        Assert.Equal("[text] [ab   ] [Z] 5%", result);
    }

    [Fact]
    public void When_FormattingLowerCaseCodes_Then_CodesAreHandled()
    {
        var ram = new Ram(0x10000);

        Assert.Equal("255 FF", Format(ram, "%n %x2", 255, 255));
    }
}
