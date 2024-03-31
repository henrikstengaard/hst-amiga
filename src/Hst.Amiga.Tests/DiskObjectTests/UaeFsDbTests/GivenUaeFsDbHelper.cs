using System;
using System.IO;
using Hst.Amiga.DataTypes.UaeFsDbs;
using Xunit;

namespace Hst.Amiga.Tests.DiskObjectTests.UaeFsDbTests;

public class GivenUaeFsDbHelper
{
    [Theory]
    [InlineData('a')]
    [InlineData('Z')]
    [InlineData('+')]
    [InlineData('\'')]
    [InlineData('^')]
    [InlineData('@')]
    public void When_MakingSafeFilenameWithoutSpecialChars_Then_FilenameIsUnchanged(char chr)
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
    [InlineData('*')]
    [InlineData('?')]
    [InlineData('\"')]
    [InlineData('<')]
    [InlineData('>')]
    public void When_MakingSafeFilenameWithSpecialChars_Then_SpecialCharsAreReplacedWithUnderscore(char specialChar)
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
        Assert.Equal(Path.Combine(path, "__uae___file1_"), uniqueNormalName);
    }

    [Fact]
    public void When_CreatingUniqueNormalNameWithFileExisting_ThenNormalNameIsUnique()
    {
        // arrange
        var safeFilename = "file1_";
        var path = Guid.NewGuid().ToString();
        Directory.CreateDirectory(path);
        var existingFilePath = Path.Combine(path, "__uae___file1_");
        File.WriteAllText(existingFilePath, string.Empty);

        try
        {
            // act
            var uniqueNormalName = UaeFsDbNodeHelper.CreateUniqueNormalName(path, safeFilename);
            
            // assert
            Assert.NotEqual(existingFilePath, uniqueNormalName);
        }
        finally
        {
            Directory.Delete(path, true);
        }
    }
}