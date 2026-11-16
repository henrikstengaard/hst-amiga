namespace Hst.Amiga.Tests.VmTests;

using System.Collections.Generic;
using Vm.Libraries.Dos;
using Xunit;

public class GivenItemReader
{
    private static List<(int Type, string Item)> ReadAll(string input, int maxChars = 80)
    {
        var position = 0;
        var items = new List<(int, string)>();
        while (true)
        {
            var type = ItemReader.Read(() => position < input.Length ? input[position++] : -1,
                () => position--, maxChars, out var item);
            items.Add((type, item));
            if (type == ItemReader.ItemNothing || type == ItemReader.ItemError || items.Count > 20)
            {
                return items;
            }
        }
    }

    [Fact]
    public void When_ReadingItems_Then_UnquotedQuotedAndEqualItemsAreRead()
    {
        var items = ReadAll("  file \"big *\"world*\"*N\" key=value\n");

        Assert.Equal(new List<(int, string)>
        {
            (ItemReader.ItemUnquoted, "file"),
            (ItemReader.ItemQuoted, "big \"world\"\n"),
            (ItemReader.ItemUnquoted, "key"),
            (ItemReader.ItemEqual, string.Empty),
            (ItemReader.ItemUnquoted, "value"),
            (ItemReader.ItemNothing, string.Empty)
        }, items);
    }

    [Fact]
    public void When_ReadingAtEndOfLine_Then_NewlineIsLeftUnread()
    {
        var input = "item\nnext";
        var position = 0;

        ItemReader.Read(() => position < input.Length ? input[position++] : -1, () => position--, 80, out _);
        var type = ItemReader.Read(() => position < input.Length ? input[position++] : -1, () => position--, 80,
            out _);

        Assert.Equal(ItemReader.ItemNothing, type);
        Assert.Equal('\n', input[position]);
    }

    [Theory]
    [InlineData("\"unterminated\n")]
    [InlineData("toolongitem")]
    public void When_ReadingInvalidItem_Then_ErrorIsReturned(string input)
    {
        Assert.Equal(ItemReader.ItemError, ReadAll(input, 5)[0].Type);
    }
}
