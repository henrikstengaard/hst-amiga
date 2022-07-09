﻿namespace Hst.Amiga.FileSystems.FastFileSystem
{
    using System;
    using System.IO;
    using Core.Converters;

    public static class DirCacheBlockReader
    {
        public static DirCacheBlock Read(byte[] blockBytes)
        {
            var type = BigEndianConverter.ConvertBytesToInt32(blockBytes);
            if (type != Constants.T_DIRC)
            {
                throw new IOException("Invalid dir cache block type");
            }
            
            var headerKey = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x4);
            var parent = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x8);
            var recordsNb = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0xc);
            var nextDirC = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x10);
            var checksum = BigEndianConverter.ConvertBytesToInt32(blockBytes, 0x14);
            
            var calculatedChecksum = ChecksumHelper.CalculateChecksum(blockBytes, 0x14);
            if (checksum != calculatedChecksum)
            {
                throw new IOException("Invalid dir cache block checksum");
            }

            var records = new byte[488];
            Array.Copy(blockBytes, 0x18, records, 0, 488);
            
            return new DirCacheBlock
            {
                BlockBytes = blockBytes,
                HeaderKey = headerKey,
                Parent = parent,
                RecordsNb = recordsNb,
                NextDirC = nextDirC,
                Checksum = checksum,
                Records = records
            };
        }
    }
}