namespace Hst.Amiga.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

public class GivenAmigaDisk
{
    [Fact]
    public async Task WhenFindUsedSectorsOnDiskWithZerosThenNoUsedSectorsAreFound()
    {
        // arrange - stream with zeros
        var stream = new MemoryStream(new byte[1024 * 1024]);
        
        // act - find used sectors
        var usedSectors = new List<Tuple<long, byte[]>>();
        await Disk.FindUsedSectors(stream, 512, (offset, bytes) =>
        {
            usedSectors.Add(new Tuple<long, byte[]>(offset, bytes));
            return Task.CompletedTask;
        });
        
        // assert - no used sectors was found
        Assert.Empty(usedSectors);
    }
    
    [Fact]
    public async Task WhenFindUsedSectorsOnDiskWithUsedSectorsThenUsedSectorsAreFound()
    {
        // arrange - used bytes
        var usedBytes = new byte[1024 * 1024];
        
        // arrange - set byte 1 at offset 516 to indicate block at 512 is used
        usedBytes[516] = 1;

        // arrange - set byte 1 at offset 7635 to indicate block at 7168 is used
        usedBytes[7635] = 1;
        
        // arrange - stream with zeros
        var stream = new MemoryStream(usedBytes);
        
        // act - find used sectors
        var usedSectors = new List<Tuple<long, byte[]>>();
        await Disk.FindUsedSectors(stream, 512, (offset, bytes) =>
        {
            usedSectors.Add(new Tuple<long, byte[]>(offset, bytes));
            return Task.CompletedTask;
        });
        
        // assert - used sectors was found
        Assert.NotEmpty(usedSectors);
        Assert.Equal(2, usedSectors.Count);
        
        // assert - used sector 1 is equal
        var expectedBytes = new byte[512];
        expectedBytes[4] = 1;
        Assert.Equal(512, usedSectors[0].Item1);
        Assert.Equal(expectedBytes, usedSectors[0].Item2);
        
        // assert - used sector 1 is equal
        expectedBytes = new byte[512];
        expectedBytes[467] = 1;
        Assert.Equal(7168, usedSectors[1].Item1);
        Assert.Equal(expectedBytes, usedSectors[1].Item2);
    }
}