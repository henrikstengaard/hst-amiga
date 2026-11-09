namespace Hst.Amiga.Tests.SmartFileSystemTests;

using System;
using Core.Converters;
using FileSystems;
using FileSystems.SmartFileSystem;
using Xunit;
using DateHelper = FileSystems.SmartFileSystem.DateHelper;
using ChecksumHelper = FileSystems.SmartFileSystem.ChecksumHelper;

public class GivenSmartFileSystemHelpers
{
    [Fact]
    public void WhenCalculatingHashOfRecycledCaseInsensitiveThenHashMatchesSmartFileSystem()
    {
        // act
        var hash = HashHelper.Hash(".recycled", false);

        // assert - hash and hash chain matches values calculated by smart file system hash()
        Assert.Equal(0x47EA, hash);
        Assert.Equal(58, HashHelper.HashChain(hash, 512));
        Assert.Equal(14, HashHelper.HashChain(hash, 1024));
        Assert.Equal(122, HashHelper.HashChain(hash, 2048));
    }

    [Fact]
    public void WhenCalculatingHashOfRecycledCaseSensitiveThenHashMatchesSmartFileSystem()
    {
        // act
        var hash = HashHelper.Hash(".recycled", true);

        // assert
        Assert.Equal(0x72EA, hash);
        Assert.Equal(30, HashHelper.HashChain(hash, 512));
    }

    [Fact]
    public void WhenCalculatingHashOfNamesWithDifferentCaseThenHashIsEqualForCaseInsensitive()
    {
        Assert.Equal(HashHelper.Hash("ReadMe.txt", false), HashHelper.Hash("README.TXT", false));
        Assert.Equal(HashHelper.Hash("æble", false), HashHelper.Hash("ÆBLE", false));
        Assert.NotEqual(HashHelper.Hash("ReadMe.txt", true), HashHelper.Hash("README.TXT", true));
    }

    [Fact]
    public void WhenComparingNamesThenCaseSensitivityIsUsed()
    {
        Assert.True(HashHelper.NamesEqual("file", "FILE", false));
        Assert.False(HashHelper.NamesEqual("file", "FILE", true));
        Assert.True(HashHelper.NamesEqual("file", "file", true));
    }

    [Fact]
    public void WhenUpdatingChecksumThenBlockIsValid()
    {
        // arrange - block with id and some data
        var blockBytes = new byte[512];
        BigEndianConverter.ConvertUInt32ToBytes(0x4F424A43, blockBytes, 0);
        BigEndianConverter.ConvertUInt32ToBytes(12345, blockBytes, 8);
        blockBytes[100] = 0xff;

        // act
        ChecksumHelper.UpdateChecksum(blockBytes);

        // assert - sum of block with initial value 1 is 0
        Assert.True(ChecksumHelper.IsValid(blockBytes));
        Assert.Equal(0U, ChecksumHelper.CalculateSum(blockBytes));

        // act - change byte in block
        blockBytes[200] = 1;

        // assert - checksum is invalid
        Assert.False(ChecksumHelper.IsValid(blockBytes));
    }

    [Fact]
    public void WhenCalculatingChecksumOfEmptyBlockThenChecksumIsMinusOne()
    {
        // arrange
        var blockBytes = new byte[512];

        // act
        var checksum = ChecksumHelper.CalculateChecksum(blockBytes);

        // assert - sum of empty block is 1, checksum is -1
        Assert.Equal(0xffffffffU, checksum);
    }

    [Fact]
    public void WhenConvertingDatesThenDatesAreSecondsSince1978()
    {
        Assert.Equal(0U, DateHelper.ConvertFromDate(new DateTime(1978, 1, 1)));
        Assert.Equal(86400U + 3661U, DateHelper.ConvertFromDate(new DateTime(1978, 1, 2, 1, 1, 1)));
        Assert.Equal(new DateTime(1978, 1, 2, 1, 1, 1), DateHelper.ConvertToDate(86400U + 3661U));
        Assert.Equal(0U, DateHelper.ConvertFromDate(new DateTime(1970, 1, 1)));
    }

    [Fact]
    public void WhenConvertingProtectionThenReadWriteExecuteDeleteBitsAreSetWhenAllowed()
    {
        Assert.Equal(ProtectionBits.Read | ProtectionBits.Write | ProtectionBits.Executable | ProtectionBits.Delete,
            ProtectionHelper.ToProtectionBits(0x0f));
        Assert.Equal(ProtectionBits.Read | ProtectionBits.Write, ProtectionHelper.ToProtectionBits(0x0c));
        Assert.Equal(0x1fU, ProtectionHelper.ToProtection(ProtectionBits.Archive | ProtectionBits.Read |
                                                           ProtectionBits.Write | ProtectionBits.Executable |
                                                           ProtectionBits.Delete));
        Assert.Equal(0x0fU, ProtectionHelper.FromAmigaDosProtection(0));
        Assert.Equal(0x1234ff08U, ProtectionHelper.ToProtection(ProtectionBits.Read, 0x1234ffffU));
    }

    [Fact]
    public void WhenUncompressingTransactionDataThenBlockIsUpdated()
    {
        // arrange - block with 0x11 bytes
        var blockBytes = new byte[32];
        Array.Fill(blockBytes, (byte)0x11);

        // compressed data: skip 1 long, clear 2 longs, set 1 long, copy 1 long
        var data = new byte[] { 0x40, 0x01, 0x80, 0xc0, 0x01, 0x02, 0x03, 0x04 };

        // act
        Transactions.Uncompress(blockBytes, data);

        // assert
        Assert.Equal(new byte[]
        {
            0x11, 0x11, 0x11, 0x11,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0xff, 0xff, 0xff, 0xff,
            0x01, 0x02, 0x03, 0x04,
            0x11, 0x11, 0x11, 0x11,
            0x11, 0x11, 0x11, 0x11,
            0x11, 0x11, 0x11, 0x11
        }, blockBytes);
    }

    [Fact]
    public void WhenParsingTransactionOperationsThenOperationsArePaddedToEvenOffsets()
    {
        // arrange - operation with length 3 (7 + 3 = 10 bytes) and operation with length 2 (7 + 2 = 9 bytes padded to 10), then end
        var data = new byte[]
        {
            0x00, 0x03, 0x00, 0x00, 0x00, 0x0a, 0x00, 0xc0, 0x01, 0x02,
            0x00, 0x02, 0x00, 0x00, 0x00, 0x0b, 0x01, 0x00, 0x00, 0x00,
            0x00, 0x00
        };

        // act
        var operations = Transactions.ParseOperations(data);

        // assert
        Assert.Equal(2, operations.Count);
        Assert.Equal(10U, operations[0].Block);
        Assert.Equal(new byte[] { 0xc0, 0x01, 0x02 }, operations[0].Data);
        Assert.Equal(11U, operations[1].Block);
        Assert.Equal(Constants.OperationEmpty, operations[1].Bits);
        Assert.Equal(new byte[] { 0x00, 0x00 }, operations[1].Data);
    }
}
