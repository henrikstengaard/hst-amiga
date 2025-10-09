using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.ConsoleApp.Commands;
using Hst.Amiga.Roms;
using Xunit;

namespace Hst.Amiga.Tests.RomTests;

public class GivenEpromByteSwapCommand
{
    [Theory]
    [InlineData(Constants.EpromSize.Eprom256KbSize)]
    [InlineData(Constants.EpromSize.Eprom512KbSize)]
    [InlineData(Constants.EpromSize.Eprom1024KbSize)]
    public async Task When_FillEprom_Then_RomIsConcatenatedMultipleTimesUntilSizeOfEprom(int epromSize)
    {
        // arrange - create kickstart rom bytes
        var kickstartRomBytes = RomTestHelper.Create16BitKickstartRomBytes(epromSize);

        // arrange - create kickstart rom file path
        var kickstartRomPath = $"{Guid.NewGuid()}.rom";
        var epromRomPath = string.Empty;

        try
        {
            // arrange - create kickstart rom file
            await File.WriteAllBytesAsync(kickstartRomPath, kickstartRomBytes);

            // arrange - create eprom byteswap command
            var epromByteSwapCommand = new EpromByteSwapCommand(kickstartRomPath);

            // act - execute eprom byteswap command
            var epromByteSwapCommandResult = await epromByteSwapCommand.Execute(CancellationToken.None);

            // assert - result is successful
            Assert.True(epromByteSwapCommandResult.IsSuccess);

            // assert - eprom bin file is created
            epromRomPath = Path.Combine(
                Path.GetDirectoryName(kickstartRomPath) ?? string.Empty,
                $"{Path.GetFileNameWithoutExtension(kickstartRomPath)}.byteswapped.bin");
            Assert.True(File.Exists(epromRomPath), $"EPROM file '{epromRomPath}' was not created.");
            
            // arrange - create expected eprom bin bytes
            var expectedEpromBinBytes = new byte[kickstartRomBytes.Length];
            Array.Copy(kickstartRomBytes, expectedEpromBinBytes, kickstartRomBytes.Length);
            RomTestHelper.ByteSwapRomBytes(expectedEpromBinBytes);

            // assert - eprom bin file is byte swapped
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
    [InlineData(1)]
    [InlineData(999)]
    public async Task When_ByteSwapEpromWithInvalidSize_Then_ErrorIsReturned(int size)
    {
        // arrange - create kickstart rom bytes
        var kickstartRomBytes = new byte[size];

        // arrange - create kickstart rom file path
        var kickstartRomPath = $"{Guid.NewGuid()}.rom";

        try
        {
            // arrange - create kickstart rom file
            await File.WriteAllBytesAsync(kickstartRomPath, kickstartRomBytes);

            // arrange - create eprom byte swap command
            var epromByteSwapCommand = new EpromByteSwapCommand(kickstartRomPath);

            // act - execute eprom byte swap command
            var epromByteSwapCommandResult = await epromByteSwapCommand.Execute(CancellationToken.None);

            // assert - result is not successful
            Assert.True(epromByteSwapCommandResult.IsFaulted);
            Assert.False(epromByteSwapCommandResult.IsSuccess);
        }
        finally
        {
            File.Delete(kickstartRomPath);
        }
    }
}