﻿namespace Hst.Amiga.Tests.FastFileSystemTests;

using System.Linq;
using FileSystems.FastFileSystem.Blocks;
using RigidDiskBlocks;
using Xunit;

public class GivenBootBlockBuilder
{
    private static readonly byte[] ExpectedBootBlockBytes =
    {
        0x44, 0x4f, 0x53, 0x00, 0xe3, 0x3d, 0x0e, 0x73, 0x00, 0x00, 0x03, 0x70, 0x43, 0xfa, 0x00, 0x3e, 0x70, 0x25, 
        0x4e, 0xae, 0xfd, 0xd8, 0x4a, 0x80, 0x67, 0x0c, 0x22, 0x40, 0x08, 0xe9, 0x00, 0x06, 0x00, 0x22, 0x4e, 0xae, 
        0xfe, 0x62, 0x43, 0xfa, 0x00, 0x18, 0x4e, 0xae, 0xff, 0xa0, 0x4a, 0x80, 0x67, 0x0a, 0x20, 0x40, 0x20, 0x68, 
        0x00, 0x16, 0x70, 0x00, 0x4e, 0x75, 0x70, 0xff, 0x4e, 0x75, 0x64, 0x6f, 0x73, 0x2e, 0x6c, 0x69, 0x62, 0x72, 
        0x61, 0x72, 0x79, 0x00, 0x65, 0x78, 0x70, 0x61, 0x6e, 0x73, 0x69, 0x6f, 0x6e, 0x2e, 0x6c, 0x69, 0x62, 0x72, 
        0x61, 0x72, 0x79
    };
    
    [Fact]
    public void WhenBuildBootBlockThenBytes()
    {
        // arrange - boot block
        var bootBlock = new BootBlock
        {
            DosType = DosTypeHelper.FormatDosType("DOS0")
        };
        
        // act - boot block bytes
        var blockBytes = BootBlockBuilder.Build(bootBlock, 2 * 512);

        // assert - first 93 bytes of block bytes are equal to expected boot block bytes
        Assert.Equal(ExpectedBootBlockBytes, blockBytes.Take(ExpectedBootBlockBytes.Length));
    }
}