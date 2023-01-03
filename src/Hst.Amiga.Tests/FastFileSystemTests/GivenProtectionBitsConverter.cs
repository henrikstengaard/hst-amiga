namespace Hst.Amiga.Tests.FastFileSystemTests;

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
}