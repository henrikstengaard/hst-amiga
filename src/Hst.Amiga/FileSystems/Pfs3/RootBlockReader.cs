﻿namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;
    using Core.Extensions;

    // https://github.com/tonioni/pfs3aio
    public class RootBlockReader
    {
        public static async Task<RootBlock> Parse(byte[] blockBytes)
        {
            var blockStream = new MemoryStream(blockBytes);
            
            var diskType = await blockStream.ReadBigEndianInt32();
            if (diskType != 0x50465301)
            {
                throw new IOException("Invalid root block");
            }

            var options = await blockStream.ReadBigEndianUInt32();
            var datestamp = await blockStream.ReadBigEndianUInt32(); /* current datestamp */
            var creationDate = await DateHelper.ReadDate(blockStream);
            // var creationDay = await blockStream.ReadUInt16(); /* days since Jan. 1, 1978 (like ADOS; WORD instead of LONG) */
            // var creationMinute = await blockStream.ReadUInt16(); /* minutes past modnight            */
            // var creationTick = await blockStream.ReadUInt16(); /* ticks past minute                */
            var protection = await blockStream.ReadBigEndianUInt16(); /* protection bits (ala ADOS)       */
            var diskNameLength = blockStream.ReadByte(); /* disk label (pascal string)       */
            var diskNameBytes = await blockStream.ReadBytes(31);
            var diskName = AmigaTextHelper.GetString(diskNameBytes, 0, diskNameLength);
            var lastReserved = await blockStream.ReadBigEndianUInt32(); /* reserved area. sector number of last reserved block */
            var firstReserved = await blockStream.ReadBigEndianUInt32(); /* sector number of first reserved block */
            var reservedFree = await blockStream.ReadBigEndianUInt32(); /* number of reserved blocks (blksize blocks) free  */
            var reservedBlkSize = await blockStream.ReadBigEndianUInt16(); /* size of reserved blocks in bytes */
            var rblkCluster = await blockStream.ReadBigEndianUInt16(); /* number of sectors in rootblock, including bitmap  */
            var blocksFree = await blockStream.ReadBigEndianUInt32(); /* blocks free                      */
            var alwaysFree = await blockStream.ReadBigEndianUInt32(); /* minimum number of blocks free    */
            var rovingPtr = await blockStream.ReadBigEndianUInt32(); /* current LONG bitmapfield nr for allocation       */
            var delDir = await blockStream.ReadBigEndianUInt32(); /* deldir location (<= 17.8)        */
            var diskSize = await blockStream.ReadBigEndianUInt32(); /* disksize in sectors              */
            var extension = await blockStream.ReadBigEndianUInt32(); /* rootblock extension (16.4)       offset=88 $58 */
            await blockStream.ReadBigEndianUInt32(); // not used

            var idxUnion = new List<uint>();
            for (var i = 0; i < SizeOf.RootBlock.IdxUnion; i++)
            {
                idxUnion.Add(await blockStream.ReadBigEndianUInt32());
            }
            
            return new RootBlock
            {
                DiskType = diskType,
                Options = (RootBlock.DiskOptionsEnum)options,
                Datestamp = datestamp,
                CreationDate = creationDate,
                Protection = protection,
                DiskName = diskName,
                LastReserved = lastReserved,
                FirstReserved = firstReserved,
                ReservedFree = reservedFree,
                ReservedBlksize = reservedBlkSize,
                RblkCluster = rblkCluster,
                BlocksFree = blocksFree,
                AlwaysFree = alwaysFree,
                RovingPtr = rovingPtr,
                DelDir = delDir,
                DiskSize = diskSize,
                Extension = extension,
                idx = new RootBlockIndex(idxUnion.ToArray())
            };
        }
    }
}