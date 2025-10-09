using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.ConsoleApp.Commands;
using Hst.Amiga.Roms;
using Xunit;

namespace Hst.Amiga.Tests.RomTests;

public class GivenEpromFillCommand
{
    [Theory]
    [InlineData(null, 524288, 524288)]
    [InlineData(null, 1048576, 1048576)]
    [InlineData(EpromType.Am27C400, null, 524288)]
    [InlineData(EpromType.Am27C800, null, 1048576)]
    [InlineData(EpromType.Mx29F1615, null, 2097152)]
    public async Task When_FillEprom_Then_RomIsConcatenatedMultipleTimesUntilSizeOfEprom(EpromType? epromType, int? size, int epromSize)
    {
        // arrange - no zero fill
        const bool zeroFill = false;
        
        // arrange - create 256kb kickstart rom bytes
        var kickstartRomBytes = RomTestHelper.Create16BitKickstartRomBytes(Constants.EpromSize.Eprom256KbSize);

        // arrange - create kickstart rom file path
        var kickstartRomPath = $"{Guid.NewGuid()}.rom";
        var epromRomPath = string.Empty;

        try
        {
            // arrange - create kickstart rom file
            await File.WriteAllBytesAsync(kickstartRomPath, kickstartRomBytes);

            // arrange - create eprom fill command
            var epromFillCommand = new EpromFillCommand(kickstartRomPath, epromType, size, zeroFill);

            // act - execute eprom fill command
            var epromFillCommandResult = await epromFillCommand.Execute(CancellationToken.None);

            // assert - result is successful
            Assert.True(epromFillCommandResult.IsSuccess);

            // assert - eprom bin file is created
            var epromName = epromType?.ToString().ToLowerInvariant() ??
                            size?.ToString().ToLowerInvariant() ??
                            nameof(EpromType.Am27C400).ToLowerInvariant();
            epromRomPath = Path.Combine(
                Path.GetDirectoryName(kickstartRomPath) ?? string.Empty,
                $"{Path.GetFileNameWithoutExtension(kickstartRomPath)}.filled.{epromName}.bin");
            Assert.True(File.Exists(epromRomPath), $"EPROM file '{epromRomPath}' was not created.");
            
            // arrange - create expected eprom bin bytes
            var expectedEpromBinBytes = RomTestHelper.FillRomBytes(kickstartRomBytes, epromSize);

            // assert - eprom bin file is filled up to expected eprom size
            var epromBinBytes = await File.ReadAllBytesAsync(epromRomPath);
            Assert.Equal(expectedEpromBinBytes, epromBinBytes);
        }
        finally
        {
            if (File.Exists(kickstartRomPath))
            {
                File.Delete(kickstartRomPath);
            }

            if (!string.IsNullOrEmpty(epromRomPath) && File.Exists(epromRomPath))
            {
                File.Delete(epromRomPath);
            }
        }
    }

    [Theory]
    [InlineData(524288, 1048576)]
    [InlineData(262144, 524288)]
    public async Task When_FillEpromWithSizeSmallerThanRom_Then_ErrorIsReturned(int? size,
        int kickstartRomSize)
    {
        // arrange - no zero fill
        const bool zeroFill = false;
        
        // arrange - create kickstart rom bytes
        var kickstartRomBytes = RomTestHelper.Create16BitKickstartRomBytes(kickstartRomSize);

        // arrange - create kickstart rom file path
        var kickstartRomPath = $"{Guid.NewGuid()}.rom";

        try
        {
            // arrange - create kickstart rom file
            await File.WriteAllBytesAsync(kickstartRomPath, kickstartRomBytes);

            // arrange - create eprom fill command
            var epromFillCommand = new EpromFillCommand(kickstartRomPath, null, size, zeroFill);

            // act - execute eprom fill command
            var epromFillCommandResult = await epromFillCommand.Execute(CancellationToken.None);

            // assert - result is not successful
            Assert.True(epromFillCommandResult.IsFaulted);
            Assert.False(epromFillCommandResult.IsSuccess);
        }
        finally
        {
            File.Delete(kickstartRomPath);
        }
    }

    [Theory]
    [InlineData(200000, 524288)]
    [InlineData(400000, 524288)]
    public async Task When_FillEpromWithSizeSmallerThanRomNotMultipleOf_Then_ErrorIsReturned(int? size,
        int kickstartRomSize)
    {
        // arrange - no zero fill
        const bool zeroFill = false;
        
        // arrange - create kickstart rom bytes
        var kickstartRomBytes = RomTestHelper.Create16BitKickstartRomBytes(kickstartRomSize);

        // arrange - create kickstart rom file path
        var kickstartRomPath = $"{Guid.NewGuid()}.rom";

        try
        {
            // arrange - create kickstart rom file
            await File.WriteAllBytesAsync(kickstartRomPath, kickstartRomBytes);

            // arrange - create eprom fill command
            var epromFillCommand = new EpromFillCommand(kickstartRomPath, null, size, zeroFill);

            // act - execute eprom fill command
            var epromFillCommandResult = await epromFillCommand.Execute(CancellationToken.None);

            // assert - result is not successful
            Assert.True(epromFillCommandResult.IsFaulted);
            Assert.False(epromFillCommandResult.IsSuccess);
        }
        finally
        {
            File.Delete(kickstartRomPath);
        }
    }
    
    [Theory]
    [InlineData(null, 524288, 1000)]
    [InlineData(null, 1048576, 4896)]
    [InlineData(EpromType.Am27C400, null, 28000)]
    [InlineData(EpromType.Am27C800, null, 75000)]
    [InlineData(EpromType.Mx29F1615, null, 100000)]
    public async Task When_FillEpromSmallerThanSize_Then_ErrorIsReturned(EpromType? epromType, int? size,
        int kickstartRomSize)
    {
        // arrange - no zero fill
        const bool zeroFill = false;
        
        // arrange - create kickstart rom bytes
        var kickstartRomBytes = new byte[kickstartRomSize];
        Array.Fill<byte>(kickstartRomBytes, 1);

        // arrange - create kickstart rom file path
        var kickstartRomPath = $"{Guid.NewGuid()}.rom";
        var epromRomPath = string.Empty;

        try
        {
            // arrange - create kickstart rom file
            await File.WriteAllBytesAsync(kickstartRomPath, kickstartRomBytes);

            // arrange - create eprom fill command
            var epromFillCommand = new EpromFillCommand(kickstartRomPath, epromType, size, zeroFill);

            // act - execute eprom fill command
            var epromFillCommandResult = await epromFillCommand.Execute(CancellationToken.None);

            // assert - result is not successful
            Assert.True(epromFillCommandResult.IsFaulted);
            Assert.False(epromFillCommandResult.IsSuccess);
        }
        finally
        {
            if (File.Exists(kickstartRomPath))
            {
                File.Delete(kickstartRomPath);
            }

            if (!string.IsNullOrEmpty(epromRomPath) && File.Exists(epromRomPath))
            {
                File.Delete(epromRomPath);
            }
        }
    }
    [Theory]
    [InlineData(null, 524288, 1000)]
    [InlineData(null, 1048576, 4896)]
    [InlineData(EpromType.Am27C400, null, 28000)]
    [InlineData(EpromType.Am27C800, null, 75000)]
    [InlineData(EpromType.Mx29F1615, null, 100000)]
    public async Task When_FillEpromWithZeroes_Then_RomIsCopiedAndFilledWithZeroes(EpromType? epromType, int? size,
        int kickstartRomSize)
    {
        // arrange - zero fill
        const bool zeroFill = true;
        
        // arrange - create kickstart rom bytes
        var kickstartRomBytes = new byte[kickstartRomSize];
        Array.Fill<byte>(kickstartRomBytes, 1);

        // arrange - create kickstart rom file path
        var kickstartRomPath = $"{Guid.NewGuid()}.rom";
        var epromRomPath = string.Empty;

        try
        {
            // arrange - create kickstart rom file
            await File.WriteAllBytesAsync(kickstartRomPath, kickstartRomBytes);

            // arrange - create eprom fill command
            var epromFillCommand = new EpromFillCommand(kickstartRomPath, epromType, size, zeroFill);

            // act - execute eprom fill command
            var epromFillCommandResult = await epromFillCommand.Execute(CancellationToken.None);

            // assert - eprom fill result is successful
            Assert.True(epromFillCommandResult.IsSuccess);
            
            // assert - eprom bin file is created
            var epromName = epromType?.ToString().ToLowerInvariant() ??
                            size?.ToString().ToLowerInvariant() ??
                            nameof(EpromType.Am27C400).ToLowerInvariant();
            epromRomPath = Path.Combine(
                Path.GetDirectoryName(kickstartRomPath) ?? string.Empty,
                $"{Path.GetFileNameWithoutExtension(kickstartRomPath)}.filled.{epromName}.bin");
            Assert.True(File.Exists(epromRomPath), $"EPROM file '{epromRomPath}' was not created.");
            
            var epromSizeResult = EpromBuilder.GetEpromSize(epromType, size);
            Assert.True(epromSizeResult.IsSuccess);

            var epromSize = epromSizeResult.Value;
            
            // arrange - create expected eprom bin bytes with zeroes filled up to eprom size
            var expectedEpromBinBytes = new byte[epromSize];
            Array.Copy(kickstartRomBytes, expectedEpromBinBytes, kickstartRomBytes.Length);

            // assert - eprom bin file is filled with zeroes up to expected eprom size
            var epromBinBytes = await File.ReadAllBytesAsync(epromRomPath);
            Assert.Equal(expectedEpromBinBytes, epromBinBytes);
        }
        finally
        {
            if (File.Exists(kickstartRomPath))
            {
                File.Delete(kickstartRomPath);
            }

            if (!string.IsNullOrEmpty(epromRomPath) && File.Exists(epromRomPath))
            {
                File.Delete(epromRomPath);
            }
        }
    }
}