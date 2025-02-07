using System;
using System.IO;
using System.Text;
using Hst.Amiga.DataTypes.UaeFsDbs;
using Xunit;

namespace Hst.Amiga.Tests.UaeFsDbTests;

public class GivenUaeFsDbHelper
{
    [Theory]
    [InlineData('a')]
    [InlineData('Z')]
    [InlineData('+')]
    [InlineData('\'')]
    [InlineData('^')]
    [InlineData('@')]
    public void When_MakingFilenameWithoutASpecialCharSafe_Then_FilenameIsUnchanged(char chr)
    {
        // arrange
        var filename = string.Concat("file1", chr);
        
        // act
        var safeFilename = UaeFsDbNodeHelper.MakeSafeFilename(filename);
        
        // assert
        Assert.Equal(filename, safeFilename);
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
    public void When_MakingFilenameWithASpecialCharSafe_Then_SpecialCharIsReplacedWithUnderscore(char specialChar)
    {
        // arrange
        var filename = string.Concat("file1", specialChar);
        
        // act
        var safeFilename = UaeFsDbNodeHelper.MakeSafeFilename(filename);
        
        // assert
        Assert.Equal("file1_", safeFilename);
    }

    [Fact]
    public void When_CreatingUniqueNormalName_ThenNormalNameIsUnique()
    {
        // arrange
        var safeFilename = "file1_";
        var path = Directory.GetCurrentDirectory();

        // act
        var uniqueNormalName = UaeFsDbNodeHelper.CreateUniqueNormalName(path, safeFilename);
        
        // assert
        Assert.Equal("__uae___file1_", uniqueNormalName);
    }

    [Fact]
    public void When_CreatingUniqueNormalNameWithFileExisting_ThenNormalNameIsUnique()
    {
        // arrange
        const string safeFilename = "file1_";
        const string existingFileName = "__uae___file1_";
        var path = Guid.NewGuid().ToString();
        Directory.CreateDirectory(path);
        var existingFilePath = Path.Combine(path, existingFileName);
        File.WriteAllText(existingFilePath, string.Empty);

        try
        {
            // act
            var uniqueNormalName = UaeFsDbNodeHelper.CreateUniqueNormalName(path, safeFilename);
            
            // assert
            Assert.Equal(existingFileName, uniqueNormalName.Substring(0, existingFileName.Length));
            Assert.True(uniqueNormalName.Length > existingFileName.Length);
        }
        finally
        {
            Directory.Delete(path, true);
        }
    }

    [Fact]
    public void When_CreatingUniqueNormalNameWithDirectoryExisting_ThenNormalNameIsUnique()
    {
        // arrange
        const string safeFilename = "dir1_";
        const string existingDirName = "__uae___dir1_";
        var path = Guid.NewGuid().ToString();
        var existingDirPath = Path.Combine(path, existingDirName);
        Directory.CreateDirectory(existingDirPath);

        try
        {
            // act
            var uniqueNormalName = UaeFsDbNodeHelper.CreateUniqueNormalName(path, safeFilename);
            
            // assert
            Assert.Equal(existingDirName, uniqueNormalName.Substring(0, existingDirName.Length));
            Assert.True(uniqueNormalName.Length > existingDirName.Length);
        }
        finally
        {
            Directory.Delete(path, true);
        }
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
        var hasSpecialFilenameChars = UaeFsDbNodeHelper.HasSpecialFilenameChars(amigaName);

        // assert
        Assert.Equal(expectedHasSpecialFilenameChars, hasSpecialFilenameChars);
    }

    [Fact]
    public void When_DetectingFilenameWithNonPrintableChars_Then_SpecialCharsAreDetected()
    {
        // arrange
        var amigaName = Encoding.UTF8.GetString(new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff });

        // arrange
        var hasSpecialFilenameChars = UaeFsDbNodeHelper.HasSpecialFilenameChars(amigaName);

        // assert
        Assert.True(hasSpecialFilenameChars);
    }


    [Theory]
    [InlineData("dir1*", "dir1_")]
    [InlineData("dir2", "dir2")]
    [InlineData("file1*", "file1_")]
    [InlineData("file2<", "file2_")]
    [InlineData("file3", "file3")]
    [InlineData("file4.", "file4_")]
    [InlineData("file5..", "file5__")]
    [InlineData("file6.t", "file6.t")]
    [InlineData("file7..t", "file7..t")]
    [InlineData("file8...", "file8___")]
    [InlineData(".file9", ".file9")]
    [InlineData("", "")]
    [InlineData("   ", "___")]
    [InlineData(".   ", "____")]
    [InlineData(" .  ", "____")]
    [InlineData("  . ", "____")]
    [InlineData("   .", "____")]
    [InlineData(".  f", ".  f")]
    [InlineData("f  f", "f  f")]
    [InlineData("f  f ", "f__f_")]
    [InlineData("f   ", "f___")]
    [InlineData("f  .", "f___")]
    [InlineData("f . ", "f___")]
    public void When_MakingExampleFilenameWithSpecialCharsSafe_Then_SpecialCharsAreReplacedWithUnderscore(string amigaName, string expectedSafeFilename)
    {
        // arrange & act
        var safeFilename = UaeFsDbNodeHelper.MakeSafeFilename(amigaName);
        
        // assert
        Assert.Equal(expectedSafeFilename, safeFilename);
    }

    [Fact]
    public void When_MakingFilenameWithNonPrintableChars_Then_SpecialCharsAreReplacedWithUnderscore()
    {
        // arrange
        var amigaName = Encoding.UTF8.GetString(new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff });

        // arrange
        var safeFilename = UaeFsDbNodeHelper.MakeSafeFilename(amigaName);

        // assert
        Assert.Equal("________", safeFilename);
    }
}