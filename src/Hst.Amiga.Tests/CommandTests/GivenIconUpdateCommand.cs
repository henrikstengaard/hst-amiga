﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.ConsoleApp.Commands;
using Hst.Amiga.DataTypes.DiskObjects;
using Hst.Core.Converters;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Amiga.Tests.CommandTests;

public class GivenIconUpdateCommand
{
    [Fact]
    public async Task When_UpdatingCurrentX_Then_OnlyCurrentXDataIsChanged()
    {
        var iconPath = $"{Guid.NewGuid()}.info";
        var currentX = 123;
        
        var diskObject = DiskObjectHelper.CreateDiskInfo();
        var dummyColorIconData = new byte[100];
        Array.Fill<byte>(dummyColorIconData, 1);

        byte[] iconData;
        using (var iconStream = new MemoryStream())
        {
            await DiskObjectWriter.Write(diskObject, iconStream);
            iconStream.Write(dummyColorIconData, 0, dummyColorIconData.Length);
            iconData = iconStream.ToArray();
        }

        try
        {
            // arrange - write icon data
            await File.WriteAllBytesAsync(iconPath, iconData);

            // arrange - set the current x position in the disk object
            var iconUpdateCommand = new IconUpdateCommand(
                new NullLogger<IconUpdateCommand>(),
                iconPath,
                null,
                currentX,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null);

            // act - execute the command to update the current x position
            var iconUpdateResult = await iconUpdateCommand.Execute(CancellationToken.None);
            
            // assert - icon update command was successful
            Assert.True(iconUpdateResult.IsSuccess);
            
            // arrange - expected icon data with current x update at offset 0x3a
            var expectedIconData = new byte[iconData.Length];
            Array.Copy(iconData, expectedIconData, expectedIconData.Length);
            var currentXBytes = BigEndianConverter.ConvertInt32ToBytes(currentX);
            Array.Copy(currentXBytes, 0, expectedIconData, 0x3a, currentXBytes.Length);
            
            // arrange - read icon data
            var actualIconData = await File.ReadAllBytesAsync(iconPath);

            // assert
            Assert.Equal(expectedIconData.Length, actualIconData.Length);
            Assert.Equal(expectedIconData, actualIconData);
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