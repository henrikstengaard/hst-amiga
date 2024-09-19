namespace Hst.Amiga.Tests.FastFileSystemTests;

using System;
using FileSystems;
using Xunit;

public class GivenProtectionBitsConverter
{
    [Fact]
    public void WhenConvertValue0ThenReadWriteExecutableDeleteBitsAreSet()
    {
        // arrange - protection value representing attributes: read, write, executable, delete
        var protectionValue = 0;

        // act - convert protection value to bits
        var protectionBits = ProtectionBitsConverter.ToProtectionBits(protectionValue);

        // assert - protection bits is equal to read, write, executable, delete
        Assert.Equal(ProtectionBits.Read | ProtectionBits.Write | ProtectionBits.Executable | ProtectionBits.Delete,
            protectionBits);
    }

    [Fact]
    public void WhenConvertValue4ThenWriteBitIsNotSet()
    {
        // arrange - protection value representing attributes: read, executable, delete
        var protectionValue = 4;

        // act - convert protection value to bits
        var protectionBits = ProtectionBitsConverter.ToProtectionBits(protectionValue);

        // assert - protection bits is equal to read, executable, delete
        Assert.Equal(ProtectionBits.Read | ProtectionBits.Executable | ProtectionBits.Delete, protectionBits);
    }

    [Fact]
    public void WhenConvertValue6ThenWriteExecutableBitsAreNotSet()
    {
        // arrange - protection value representing attributes: read, delete
        var protectionValue = 6;

        // act - convert protection value to bits
        var protectionBits = ProtectionBitsConverter.ToProtectionBits(protectionValue);

        // assert - protection bits is equal to read, delete
        Assert.Equal(ProtectionBits.Read | ProtectionBits.Delete, protectionBits);
    }

    [Fact]
    public void WhenConvertValue64ThenWriteExecutableBitsAreNotSet()
    {
        // arrange - protection value representing attributes: script, read, write, executable, delete
        var protectionValue = 64;

        // act - convert protection value to bits
        var protectionBits = ProtectionBitsConverter.ToProtectionBits(protectionValue);

        // assert - protection bits is equal to script, read, write, executable, delete
        Assert.Equal(
            ProtectionBits.Script | ProtectionBits.Read | ProtectionBits.Write | ProtectionBits.Executable |
            ProtectionBits.Delete, protectionBits);
    }
    
    [Fact]
    public void WhenConvertBitsReadWriteExecutableDeleteFlagsThenValueIs0()
    {
        // arrange - protection bits
        var protectionBits = ProtectionBits.Read | ProtectionBits.Write | ProtectionBits.Executable | ProtectionBits.Delete;

        // act - convert protection bits to value
        var protectionValue = ProtectionBitsConverter.ToProtectionValue(protectionBits);

        // assert - protection bits is equal to 0 representing attributes: read, write, executable, delete
        Assert.Equal(0, protectionValue);
    }
    
    [Fact]
    public void WhenConvertBitsReadExecutableDeleteFlagsThenValueIs4()
    {
        // arrange - protection bits
        var protectionBits = ProtectionBits.Read | ProtectionBits.Executable | ProtectionBits.Delete;

        // act - convert protection bits to value
        var protectionValue = ProtectionBitsConverter.ToProtectionValue(protectionBits);

        // assert - protection bits is equal to 0 representing attributes: read, executable, delete
        Assert.Equal(4, protectionValue);
    }

    [Fact]
    public void WhenConvertBitsReadDeleteFlagsThenValueIs6()
    {
        // arrange - protection bits
        var protectionBits = ProtectionBits.Read | ProtectionBits.Delete;

        // act - convert protection bits to value
        var protectionValue = ProtectionBitsConverter.ToProtectionValue(protectionBits);

        // assert - protection bits is equal to 0 representing attributes: read, delete
        Assert.Equal(6, protectionValue);
    }
    
    [Fact]
    public void WhenConvertBitsScriptReadWriteExecutableDeleteFlagsThenValueIs64()
    {
        // arrange - protection bits
        var protectionBits = ProtectionBits.Script | ProtectionBits.Read | ProtectionBits.Write | ProtectionBits.Executable | ProtectionBits.Delete;

        // act - convert protection bits to value
        var protectionValue = ProtectionBitsConverter.ToProtectionValue(protectionBits);

        // assert - protection bits is equal to 0 representing attributes: script, read, write, executable, delete
        Assert.Equal(64, protectionValue);
    }

    [Theory]
    [InlineData("HSPARWED", ProtectionBits.HeldResident | ProtectionBits.Script | ProtectionBits.Pure | ProtectionBits.Archive | ProtectionBits.Read | ProtectionBits.Write | ProtectionBits.Executable | ProtectionBits.Delete)]
    [InlineData("----RWED", ProtectionBits.Read | ProtectionBits.Write | ProtectionBits.Executable | ProtectionBits.Delete)]
    public void When_Parse_ProtectionBits_Text_Then_Value_Matches(string text, ProtectionBits expected)
    {
        // act - parse protection bits text
        var protectionBits = ProtectionBitsConverter.ParseProtectionBits(text);

        // assert - protection bits is equal to expected
        Assert.Equal(expected, protectionBits);
    }

    [Theory]
    [InlineData("-")]
    [InlineData("----DRWE")]
    [InlineData("-R------")]
    public void When_Parse_Invalid_ProtectionBits_Text_Then_Value_Matches(string protectionBitsText)
    {
        // act & assert - parse invalid protection bits text throws exception
        Assert.Throws<ArgumentException>(() => ProtectionBitsConverter.ParseProtectionBits(protectionBitsText));
    }
}