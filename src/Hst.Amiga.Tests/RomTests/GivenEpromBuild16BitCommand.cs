using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.ConsoleApp.Commands;
using Hst.Amiga.Roms;
using Xunit;

namespace Hst.Amiga.Tests.RomTests;

public class GivenEpromBuild16BitCommand
{
    [Theory]
    [InlineData("a500", null, null, 524288)]
    [InlineData("a500", null, 524288, 524288)]
    [InlineData("a500", null, 1048576, 1048576)]
    [InlineData("a500", EpromType.Am27C400, null, 524288)]
    [InlineData("a500", EpromType.Am27C800, null, 1048576)]
    [InlineData("a500", EpromType.Mx29F1615, null, 2097152)]
    [InlineData("a600", null, null, 524288)]
    [InlineData("a2000", null, null, 524288)]
    public async Task When_BuildEpromFor16Bit_Then_EpromIsByteSwapped(string amigaModel, EpromType? epromType, int? size,
        int epromSize)
    {
        // arrange - set rom ic name
        const string romIcName = "u1";

        // arrange - create 16-bit kickstart rom bytes
        var kickstartRomBytes = RomTestHelper.Create16BitKickstartRomBytes();

        // arrange - create kickstart rom file path
        var kickstartRomPath = $"{Guid.NewGuid()}.rom";
        var epromRomPath = string.Empty;

        try
        {
            // arrange - create kickstart rom file
            await File.WriteAllBytesAsync(kickstartRomPath, kickstartRomBytes);

            // arrange - create eprom build 16-bit command
            var epromBuild16BitCommand = new EpromBuild16BitCommand(amigaModel, kickstartRomPath, romIcName, epromType,
                size);

            // act - execute eprom build 16-bit command
            var epromBuild16BitCommandResult = await epromBuild16BitCommand.Execute(CancellationToken.None);

            // assert - result is successful
            Assert.True(epromBuild16BitCommandResult.IsSuccess);

            // assert - eprom bin file is created
            var epromName = epromType?.ToString().ToLowerInvariant() ??
                            size?.ToString().ToLowerInvariant() ??
                            nameof(EpromType.Am27C400).ToLowerInvariant();
            epromRomPath = Path.Combine(
                Path.GetDirectoryName(kickstartRomPath) ?? string.Empty,
                $"{Path.GetFileNameWithoutExtension(kickstartRomPath)}.{amigaModel}.{romIcName.ToLowerInvariant()}.{epromName}.bin");
            Assert.True(File.Exists(epromRomPath), $"EPROM file '{epromRomPath}' was not created.");

            // arrange - create expected eprom bin bytes
            var expectedEpromBinBytes = RomTestHelper.FillRomBytes(kickstartRomBytes, epromSize);
            RomTestHelper.ByteSwapRomBytes(expectedEpromBinBytes);

            // assert - eprom bin file is byte swapped
            var epromBinBytes = await File.ReadAllBytesAsync(epromRomPath);
            Assert.Equal(expectedEpromBinBytes, epromBinBytes);
        }
        finally
        {
            File.Delete(kickstartRomPath);

            if (!string.IsNullOrEmpty(epromRomPath) && File.Exists(epromRomPath))
            {
                File.Delete(epromRomPath);
            }
        }
    }

    [Theory]
    [InlineData(524288, 1048576)]
    [InlineData(262144, 524288)]
    public async Task When_BuildEpromFor16BitWithSizeSmallerThanRom_Then_ErrorIsReturned(int? size, 
        int kickstartRomSize)
    {
        // arrange - set amiga model and rom ic name
        const string amigaModel = "a500";
        const string romIcName = "u1";

        // arrange - create 16-bit kickstart rom bytes
        var kickstartRomBytes = RomTestHelper.Create16BitKickstartRomBytes(kickstartRomSize);

        // arrange - create kickstart rom file path
        var kickstartRomPath = $"{Guid.NewGuid()}.rom";

        try
        {
            // arrange - create kickstart rom file
            await File.WriteAllBytesAsync(kickstartRomPath, kickstartRomBytes);

            // arrange - create eprom build 16-bit command
            var epromBuild16BitCommand = new EpromBuild16BitCommand(amigaModel, kickstartRomPath, romIcName, null,
                size);

            // act - execute eprom build 16-bit command
            var epromBuild16BitCommandResult = await epromBuild16BitCommand.Execute(CancellationToken.None);

            // assert - result is not successful
            Assert.True(epromBuild16BitCommandResult.IsFaulted);
            Assert.False(epromBuild16BitCommandResult.IsSuccess);
        }
        finally
        {
            File.Delete(kickstartRomPath);
        }
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public async Task When_BuildEpromFor16BitWithInvalidSize_Then_ErrorIsReturned(int? size)
    {
        // arrange - set amiga model and rom ic name
        const string amigaModel = "a500";
        const string romIcName = "u1";

        // arrange - create 16-bit kickstart rom bytes
        var kickstartRomBytes = RomTestHelper.Create16BitKickstartRomBytes();

        // arrange - create kickstart rom file path
        var kickstartRomPath = $"{Guid.NewGuid()}.rom";

        try
        {
            // arrange - create kickstart rom file
            await File.WriteAllBytesAsync(kickstartRomPath, kickstartRomBytes);

            // arrange - create eprom build 16-bit command
            var epromBuild16BitCommand = new EpromBuild16BitCommand(amigaModel, kickstartRomPath, romIcName, null, size);

            // act - execute eprom build 16-bit command
            var epromBuild16BitCommandResult = await epromBuild16BitCommand.Execute(CancellationToken.None);

            // assert - result is not successful
            Assert.True(epromBuild16BitCommandResult.IsFaulted);
            Assert.False(epromBuild16BitCommandResult.IsSuccess);
        }
        finally
        {
            File.Delete(kickstartRomPath);
        }
    }
}