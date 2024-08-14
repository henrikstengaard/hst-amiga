using Hst.Amiga.DataTypes.UaeMetafiles;
using Xunit;

namespace Hst.Amiga.Tests.UaeMetafileTests;

public class GivenUaeMetafileHelper
{
    [Theory]
    [InlineData('a')]
    [InlineData('Z')]
    [InlineData('+')]
    [InlineData('\'')]
    [InlineData('^')]
    [InlineData('@')]
    public void When_EncodingFilenameWithoutASpecialChar_Then_FilenameIsUnchanged(char chr)
    {
        // arrange
        var filename = string.Concat("file1", chr);
        
        // act
        var encodedFilename = UaeMetafileHelper.EncodeFilenameSpecialChars(filename);
        
        // assert
        Assert.Equal(filename, encodedFilename);
    }

    [Theory]
    [InlineData('\\')]
    [InlineData('/')]
    [InlineData(':')]
    [InlineData('*')]
    [InlineData('?')]
    [InlineData('\"')]
    [InlineData('<')]
    [InlineData('>')]
    [InlineData('|')]
    [InlineData('#')]
    public void When_EncodingFilenameWithASpecialChar_Then_SpecialCharIsReplacedWithHexValues(char specialChar)
    {
        // arrange
        var filename = string.Concat("file1", specialChar.ToString());

        // act
        var encodedFilename = UaeMetafileHelper.EncodeFilenameSpecialChars(filename);
        
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
    
    [Theory]
    [InlineData("dir1*", "dir1%2a")]
    [InlineData("dir2", "dir2")]
    [InlineData("file1*", "file1%2a")]
    [InlineData("file2<", "file2%3c")]
    [InlineData("file3", "file3")]
    [InlineData("file4.", "file4%2e")]
    [InlineData("file5..", "file5.%2e")]
    [InlineData("file6.t", "file6.t")]
    [InlineData("file7..t", "file7..t")]
    [InlineData("file8...", "file8..%2e")]
    [InlineData(".file9", ".file9")]
    public void When_EncodingExampleFilenameWithSpecialChars_Then_SpecialCharsAreReplacedWithHexValues(string amigaName, string expectedEncodedFilename)
    {
        // arrange & act
        var encodedFilename = UaeMetafileHelper.EncodeFilenameSpecialChars(amigaName);
        
        // assert
        Assert.Equal(expectedEncodedFilename, encodedFilename);
    }
}