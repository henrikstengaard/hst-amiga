using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.ConsoleApp.Commands;
using Hst.Amiga.DataTypes.DiskObjects;
using Hst.Amiga.Tests.DiskObjectTests;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Amiga.Tests.CommandTests;

public class GivenIconInfoCommand
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public async Task When_DiskObjectHasPaddedZeroes_ThenInfoIsRead(int zeroBytesCount)
    {
        // arrange - paths
        var iconPath = $"{Guid.NewGuid()}.info";

        // arrange - create disk object
        var diskObject = DiskObjectHelper.CreateDiskInfo();
        
        // arrange - create icon data with disk object and zero padded bytes
        byte[] iconData;
        using (var iconStream = new MemoryStream())
        {
            await DiskObjectWriter.Write(diskObject, iconStream);
            var zeroBytes = new byte[zeroBytesCount];
            iconStream.Write(zeroBytes, 0, zeroBytes.Length);
            iconData = iconStream.ToArray();
        }
        try
        {
            // arrange - write icon data
            await File.WriteAllBytesAsync(iconPath, iconData);

            // arrange - create icon info command
            var messages = new List<string>(50);
            var command = new IconInfoCommand(new NullLogger<IconInfoCommand>(), iconPath, true);
            command.InformationMessage += (sender, message) =>
            {
                messages.Add(message);
            };
            
            // act - execute icon info command
            var result = await command.Execute(CancellationToken.None);
            
            // assert - result is successful
            Assert.True(result.IsSuccess);
            
            // assert - messages not empty
            Assert.NotEmpty(messages);
        }
        finally
        {
            if (File.Exists(iconPath))
            {
                File.Delete(iconPath);
            }
        }
    }

    [Fact]
    public async Task When_DiskObjectHasColorIconWithOddSizeChunks_ThenInfoIsRead()
    {
        // arrange - paths
        var iconPath = $"{Guid.NewGuid()}.info";

        // arrange - create disk object
        var diskObject = DiskObjectHelper.CreateDiskInfo();

        // arrange - create color icon data with odd size chunks
        var colorIconData = TestDataHelper.CreateColorIconDataWithOddSizeChunks();

        // arrange - create icon data with disk object and color icon data
        byte[] iconData;
        using (var iconStream = new MemoryStream())
        {
            await DiskObjectWriter.Write(diskObject, iconStream);
            iconStream.Write(colorIconData, 0, colorIconData.Length);
            iconData = iconStream.ToArray();
        }

        try
        {
            // arrange - write icon data
            await File.WriteAllBytesAsync(iconPath, iconData);

            // arrange - create icon info command
            var messages = new List<string>(50);
            var command = new IconInfoCommand(new NullLogger<IconInfoCommand>(), iconPath, true);
            command.InformationMessage += (sender, message) =>
            {
                messages.Add(message);
            };
            
            // act - execute icon info command
            var result = await command.Execute(CancellationToken.None);
            
            // assert - result is successful
            Assert.True(result.IsSuccess);
            
            // assert - messages not empty
            Assert.NotEmpty(messages);
            
            // assert - messages contain color icon 1
            Assert.Contains(messages, m => m.Contains("Color Icon 1:"));
        }
        finally
        {
            if (File.Exists(iconPath))
            {
                File.Delete(iconPath);
            }
        }
    }
}