namespace Hst.Amiga.Tests.RigidDiskBlockTests;

using System;
using System.Threading.Tasks;
using Core.Converters;
using FileSystems;
using RigidDiskBlocks;
using Xunit;

public class GivenFileSystemHeaderBlockWriter : RigidDiskBlockTestBase
{
    [Fact]
    public async Task WhenBuildBlockThenBytesMatchesBinaryStructure()
    {
        // arrange - create file system header block
        var fileSystemHeaderBlock = await CreateFileSystemHeaderBlock();

        // act - build file system header block
        var blockBytes = await FileSystemHeaderBlockWriter.BuildBlock(fileSystemHeaderBlock);

        // act - read file system block header structure
        var identifier = BitConverter.ToUInt32(blockBytes, 0);
        var size = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 4);
        var checksum = BigEndianConverter.ConvertBytesToInt32(blockBytes, 8);
        var hostId = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0xc);
        var nextFileSysHeaderBlock = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x10);
        var flags = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x14);
        var dosType = new byte[4];
        Array.Copy(blockBytes, 0x20, dosType, 0, 4);
        var version = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x24);
        var patchFlags = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x28);
        var type = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x2c);
        var task = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x30);
        var fileSysLock = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x34);
        var handler = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x38);
        var stackSize = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x3c);
        var priority = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x40);
        var startup = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x44);
        var segListBlocks = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x48);
        var globalVec = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x4c);
        var fileSystemName = AmigaTextHelper.GetNullTerminatedString(blockBytes, 0xac, 50);

        // assert - checksum is equal to calculated checksum
        var calculatedChecksum = ChecksumHelper.CalculateChecksum(blockBytes, 8);
        Assert.Equal(calculatedChecksum, checksum);
        
        // assert - file system header block properties
        Assert.Equal(BlockIdentifiers.FileSystemHeaderBlock, identifier);
        Assert.Equal(BlockSize.FileSystemHeaderBlock, (int)size);
        Assert.Equal(7U, hostId);
        Assert.Equal(BlockIdentifiers.EndOfBlock, nextFileSysHeaderBlock);
        Assert.Equal(0U, flags);
        Assert.Equal(Pds3DosType, dosType);
        Assert.Equal((uint)((FileSystemVersion << 16) | FileSystemRevision), version);
        Assert.Equal(0x180U, patchFlags);
        Assert.Equal(0U, type);
        Assert.Equal(0U, task);
        Assert.Equal(0U, fileSysLock);
        Assert.Equal(0U, handler);
        Assert.Equal(0U, stackSize);
        Assert.Equal(0U, priority);
        Assert.Equal(0U, startup);
        Assert.Equal(0U, segListBlocks);
        Assert.Equal(uint.MaxValue, globalVec);
        Assert.Equal("pfs3aio", fileSystemName);
    }
}