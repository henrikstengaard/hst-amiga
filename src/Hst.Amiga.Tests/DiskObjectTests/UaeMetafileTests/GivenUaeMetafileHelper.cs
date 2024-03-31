using Hst.Amiga.DataTypes.UaeMetafiles;
using Xunit;

namespace Hst.Amiga.Tests.DiskObjectTests.UaeMetafileTests;

public class GivenUaeMetafileHelper
{
    [Theory]
    [InlineData('a')]
    [InlineData('Z')]
    [InlineData('+')]
    [InlineData('\'')]
    [InlineData('^')]
    [InlineData('@')]
    public void When_EncodingFilenameWithoutSpecialChars_Then_FilenameIsUnchanged(char chr)
    {
        // arrange
        var filename = string.Concat("file1", chr);
        
        // act
        var encodedFilename = UaeMetafileHelper.EncodeFilename(filename);
        
        // assert
        Assert.Equal(filename, encodedFilename);
    }

    [Theory]
    [InlineData('\\')]
    [InlineData('*')]
    [InlineData('?')]
    [InlineData('"')]
    [InlineData('<')]
    [InlineData('>')]
    public void When_EncodingFilenameWithSpecialChars_Then_SpecialCharsAreReplacedWithHexValues(char specialChar)
    {
        // arrange
        var filename = string.Concat("file1", specialChar.ToString());

        // act
        var encodedFilename = UaeMetafileHelper.EncodeFilename(filename);
        
        // assert
        Assert.Equal(string.Concat("file1", $"%{(int)specialChar:x2}"), encodedFilename);
    }

    [Fact]
    public void When_DecodingFilename_Then_HexValuesAreReplacedWithSpecialChars()
    {
        // arrange
        var filename = "file1%2a%3c";

        // act
        var decodedFilename = UaeMetafileHelper.DecodeFilename(filename);
        
        // assert
        Assert.Equal("file1*<", decodedFilename);
    }
}