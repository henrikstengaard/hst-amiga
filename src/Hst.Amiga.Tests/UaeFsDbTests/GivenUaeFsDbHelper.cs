using System;
using System.IO;
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
    public void When_MakingExampleFilenameWithSpecialCharsSafe_Then_SpecialCharsAreReplacedWithUnderscore(string amigaName, string expectedSafeFilename)
    {
        var safeFilename = UaeFsDbNodeHelper.HasSpecialFilenameChars(amigaName)
            ? UaeFsDbNodeHelper.MakeSafeFilename(amigaName)
            : amigaName;
        
        // assert
        Assert.Equal(expectedSafeFilename, safeFilename);
    }
}