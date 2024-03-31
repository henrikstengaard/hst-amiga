using System.Text;
using Xunit;

namespace Hst.Amiga.Tests;

public class GivenTextHelper
{
    [Fact]
    public void When_ReadNullTerminatedUnicodeString_Then_TextIsRead()
    {
        // arrange
        var bytes = new byte[] { 65, 0, 66, 0, 67, 0, 0, 0 };

        // act
        var text = TextHelper.ReadNullTerminatedString(Encoding.Unicode, bytes, 0, bytes.Length);
            
        // assert
        Assert.Equal("ABC", text);
    }
}