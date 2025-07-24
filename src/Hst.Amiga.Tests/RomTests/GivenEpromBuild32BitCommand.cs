using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.ConsoleApp.Commands;
using Hst.Amiga.Roms;
using Xunit;

namespace Hst.Amiga.Tests.RomTests;

public class GivenEpromBuild32BitCommand
{
    [Theory]
    [InlineData("a1200", null, null, 524288)]
    [InlineData("a1200", null, 262144, 262144)]
    [InlineData("a1200", null, 524288, 524288)]
    [InlineData("a1200", EpromType.Am27C400, null, 524288)]
    [InlineData("a1200", EpromType.Am27C800, null, 1048576)]
    [InlineData("a1200", EpromType.Mx29F1615, null, 2097152)]
    [InlineData("a3000", null, null, 524288)]
    [InlineData("a4000", null, null, 524288)]
    public async Task When_BuildEpromFor32Bit_Then_EpromsAreSplitAndByteSwapped(string amigaModel,
        EpromType? epromType, int? size, int epromSize)
    {
        // arrange - set hi and lo rom ic names
        const string hiRomIcName = "u1";
        const string loRomIcName = "u2";

        // arrange - create 32-bit kickstart rom bytes
        var kickstartRomBytes = RomTestHelper.Create32BitKickstartRomBytes();

        // arrange - create kickstart rom file path
        var kickstartRomPath = $"{Guid.NewGuid()}.rom";
        var hiEpromBinPath = string.Empty;
        var loEpromBinPath = string.Empty;

        try
        {
            // arrange - create kickstart rom file
            await File.WriteAllBytesAsync(kickstartRomPath, kickstartRomBytes);

            // arrange - create eprom build 32-bit command
            var epromBuild32BitCommand = new EpromBuild32BitCommand(amigaModel, kickstartRomPath, hiRomIcName,
                loRomIcName, epromType, size);

            // act - execute eprom build 32-bit command
            var epromBuild32BitCommandResult = await epromBuild32BitCommand.Execute(CancellationToken.None);

            // assert - result is successful
            Assert.True(epromBuild32BitCommandResult.IsSuccess);
            
            // assert - hi eprom bin file is created
            var kickstartName = Path.GetFileNameWithoutExtension(kickstartRomPath);
            var epromName = epromType?.ToString().ToLowerInvariant() ??
                            size?.ToString().ToLowerInvariant() ??
                            nameof(EpromType.Am27C400).ToLowerInvariant();
            hiEpromBinPath = Path.Combine(
                Path.GetDirectoryName(kickstartRomPath) ?? string.Empty,
                $"{kickstartName}.{amigaModel}.hi.{hiRomIcName.ToLowerInvariant()}.{epromName}.bin");
            Assert.True(File.Exists(hiEpromBinPath), $"HI EPROM file '{hiEpromBinPath}' was not created.");
            
            // assert - hi eprom bin file is created
            loEpromBinPath = Path.Combine(
                Path.GetDirectoryName(kickstartRomPath) ?? string.Empty,
                $"{kickstartName}.{amigaModel}.lo.{loRomIcName.ToLowerInvariant()}.{epromName}.bin");
            Assert.True(File.Exists(loEpromBinPath), $"LO EPROM file '{loEpromBinPath}' was not created.");
            
            // arrange - create expected lo eprom bin bytes split from kickstart rom bytes even 16-bit words 
            var hiRomBytes = RomTestHelper.SplitHiRomBytes(kickstartRomBytes);
            RomTestHelper.ByteSwapRomBytes(hiRomBytes);
            var expectedHiEpromBinBytes = RomTestHelper.FillRomBytes(hiRomBytes, epromSize);

            // assert - hi eprom bin file contains hi eprom bytes and is byte swapped
            var hiEpromBinBytes = await File.ReadAllBytesAsync(hiEpromBinPath);
            Assert.Equal(expectedHiEpromBinBytes, hiEpromBinBytes);
            
            // arrange - create expected lo eprom bin bytes split from kickstart rom bytes odd 16-bit words 
            var loRomBytes = RomTestHelper.SplitLoRomBytes(kickstartRomBytes);
            RomTestHelper.ByteSwapRomBytes(loRomBytes);
            var expectedLoEpromBinBytes = RomTestHelper.FillRomBytes(loRomBytes, epromSize);

            // assert - lo eprom bin file is byte swapped
            var loEpromBinBytes = await File.ReadAllBytesAsync(loEpromBinPath);
            Assert.Equal(expectedLoEpromBinBytes, loEpromBinBytes);
        }
        finally
        {
            File.Delete(kickstartRomPath);
            
            if (!string.IsNullOrEmpty(hiEpromBinPath) && File.Exists(hiEpromBinPath))
            {
                File.Delete(hiEpromBinPath);
            }

            if (!string.IsNullOrEmpty(loEpromBinPath) && File.Exists(loEpromBinPath))
            {
                File.Delete(loEpromBinPath);
            }
        }
    }

    [Theory]
    [InlineData(524288, 2097152)]
    [InlineData(262144, 1048576)]
    public async Task When_BuildEpromFor32BitWithSizeSmallerThanRom_Then_ErrorIsReturned(int? size,
        int kickstartRomSize)
    {
        // arrange - set amiga model, hi and lo rom ic names
        const string amigaModel = "a1200";
        const string hiRomIcName = "u1";
        const string loRomIcName = "u2";

        // arrange - create 32-bit kickstart rom bytes
        var kickstartRomBytes = RomTestHelper.Create32BitKickstartRomBytes(kickstartRomSize);

        // arrange - create kickstart rom file path
        var kickstartRomPath = $"{Guid.NewGuid()}.rom";
        try
        {
            // arrange - create kickstart rom file
            await File.WriteAllBytesAsync(kickstartRomPath, kickstartRomBytes);

            // arrange - create eprom build 32-bit command
            var epromBuild32BitCommand = new EpromBuild32BitCommand(amigaModel, kickstartRomPath, hiRomIcName,
                loRomIcName, null, size);

            // act - execute eprom build 32-bit command
            var epromBuild32BitCommandResult = await epromBuild32BitCommand.Execute(CancellationToken.None);

            // assert - result is not successful
            Assert.True(epromBuild32BitCommandResult.IsFaulted);
            Assert.False(epromBuild32BitCommandResult.IsSuccess);
        }
        finally
        {
            File.Delete(kickstartRomPath);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public async Task When_BuildEpromFor32BitWithInvalidSize_Then_ErrorIsReturned(int? size)
    {
        // arrange - set amiga model, hi and lo rom ic names
        const string amigaModel = "a1200";
        const string hiRomIcName = "u1";
        const string loRomIcName = "u2";

        // arrange - create 32-bit kickstart rom bytes
        var kickstartRomBytes = RomTestHelper.Create32BitKickstartRomBytes();

        // arrange - create kickstart rom file path
        var kickstartRomPath = $"{Guid.NewGuid()}.rom";
        try
        {
            // arrange - create kickstart rom file
            await File.WriteAllBytesAsync(kickstartRomPath, kickstartRomBytes);

            // arrange - create eprom build 32-bit command
            var epromBuild32BitCommand = new EpromBuild32BitCommand(amigaModel, kickstartRomPath, hiRomIcName,
                loRomIcName, null, size);

            // act - execute eprom build 32-bit command
            var epromBuild32BitCommandResult = await epromBuild32BitCommand.Execute(CancellationToken.None);

            // assert - result is not successful
            Assert.True(epromBuild32BitCommandResult.IsFaulted);
            Assert.False(epromBuild32BitCommandResult.IsSuccess);
        }
        finally
        {
            File.Delete(kickstartRomPath);
        }
    }
}