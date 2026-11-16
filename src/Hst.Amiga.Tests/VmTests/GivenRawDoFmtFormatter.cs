namespace Hst.Amiga.Tests.VmTests;

using System.Text;
using Vm;
using Vm.Libraries.Exec;
using Xunit;

public class GivenRawDoFmtFormatter
{
    private const uint FormatAddress = 0x1000;
    private const uint DataAddress = 0x2000;
    private const uint StringAddress = 0x3000;

    [Fact]
    public void When_FormattingWordAndLongArguments_Then_ArgumentsAreReadFromDataStream()
    {
        var ram = new Ram(0x10000);
        ram.WriteCString(FormatAddress, "%d %ld %lx %s %c|%5ld|%-4ld|%04lx");
        ram.WriteCString(StringAddress, "text");
        var data = DataAddress;
        ram.WriteWord(data, 0xFFFF); // -1 as word
        ram.WriteLong(data + 2, 123456);
        ram.WriteLong(data + 6, 0xBEEF);
        ram.WriteLong(data + 10, StringAddress);
        ram.WriteWord(data + 14, 'A');
        ram.WriteLong(data + 16, 42);
        ram.WriteLong(data + 20, 7);
        ram.WriteLong(data + 24, 0xAB);

        var bytes = RawDoFmtFormatter.Format(ram, FormatAddress, DataAddress, out var nextData);

        Assert.Equal("-1 123456 beef text A|   42|7   |00ab", Encoding.ASCII.GetString(bytes));
        Assert.Equal(DataAddress + 28, nextData);
    }

    [Fact]
    public void When_FormattingStringWithLimit_Then_StringIsTruncated()
    {
        var ram = new Ram(0x10000);
        ram.WriteCString(FormatAddress, "[%.3s]");
        ram.WriteCString(StringAddress, "abcdef");
        ram.WriteLong(DataAddress, StringAddress);

        var bytes = RawDoFmtFormatter.Format(ram, FormatAddress, DataAddress, out _);

        Assert.Equal("[abc]", Encoding.ASCII.GetString(bytes));
    }
}
