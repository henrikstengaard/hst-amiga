﻿using Hst.Amiga.DataTypes.UaeMetafiles;
using System.Text;
using Xunit;

namespace Hst.Amiga.Tests.UaeMetafileTests;

public class GivenUaeMetafileHelper
{
    private readonly Encoding iso88591Encoding;

    public GivenUaeMetafileHelper()
    {
        iso88591Encoding = Encoding.GetEncoding("ISO-8859-1");
    }

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
    [InlineData("dir1*", true)]
    [InlineData("dir2", false)]
    [InlineData("file1*", true)]
    [InlineData("file2<", true)]
    [InlineData("file3", false)]
    [InlineData("file4.", true)]
    [InlineData("file5..", true)]
    [InlineData("file6.t", false)]
    [InlineData("file7..t", false)]
    [InlineData("file8...", true)]
    [InlineData(".file9", false)]
    [InlineData("", false)]
    [InlineData("   ", true)]
    [InlineData(".   ", true)]
    [InlineData(" .  ", true)]
    [InlineData("  . ", true)]
    [InlineData("   .", true)]
    [InlineData(".  f", false)]
    [InlineData("f  f", false)]
    [InlineData("f  f ", true)]
    [InlineData("f   ", true)]
    [InlineData("f  .", true)]
    [InlineData("f . ", true)]
    public void When_DetectingExampleFilenameForSpecialChars_Then_FilenamesWithSpecialCharsAreDetected(
        string amigaName, bool expectedHasSpecialFilenameChars)
    {
        // arrange & act
        var hasSpecialFilenameChars = UaeMetafileHelper.HasSpecialFilenameChars(amigaName);

        // assert
        Assert.Equal(expectedHasSpecialFilenameChars, hasSpecialFilenameChars);
    }

    [Fact]
    public void When_DetectingFilenameWithNonPrintableChars_Then_SpecialCharsAreDetected()
    {
        // arrange
        var amigaName = Encoding.UTF8.GetString(new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff });

        // act
        var hasSpecialFilenameChars = UaeMetafileHelper.HasSpecialFilenameChars(amigaName);

        // assert
        Assert.True(hasSpecialFilenameChars);
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
    [InlineData("", "")]
    [InlineData("   ", "  %20")]
    [InlineData(".   ", ".  %20")]
    [InlineData(" .  ", " . %20")]
    [InlineData("  . ", "  .%20")]
    [InlineData("   .", "   %2e")]
    [InlineData(".  f", ".  f")]
    [InlineData("f  f", "f  f")]
    [InlineData("f  f ", "f  f%20")]
    [InlineData("f   ", "f  %20")]
    [InlineData("f  .", "f  %2e")]
    [InlineData("f . ", "f .%20")]
    public void When_EncodingExampleFilenameWithSpecialChars_Then_SpecialCharsAreReplacedWithHexValues(string amigaName, string expectedEncodedFilename)
    {
        // arrange & act
        var encodedFilename = UaeMetafileHelper.EncodeFilenameSpecialChars(amigaName);
        
        // assert
        Assert.Equal(expectedEncodedFilename, encodedFilename);
    }

    [Fact]
    public void When_EncodingFilenameWithNonPrintableChars_Then_SpecialCharsAreReplacedWithHexValues()
    {
        // arrange
        var amigaName = iso88591Encoding.GetString(new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff });

        // act
        var encodedFilename = UaeMetafileHelper.EncodeFilenameSpecialChars(amigaName);

        // assert
        Assert.Equal("%ff%ff%ff%ff%ff%ff%ff%ff", encodedFilename);
    }
}