namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.Collections.Generic;
    using System.IO;
    using Blocks;
    using Core.Converters;

    public static class DelDirBlockReader
    {
        public static deldirblock Parse(byte[] blockBytes, globaldata g)
        {
            var id = BigEndianConverter.ConvertBytesToUInt16(blockBytes);
            if (id != Constants.DELDIRID)
            {
                throw new IOException($"Invalid del dir block id '{id}'");
            }
            
            var datestamp = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x4);
            var seqnr = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x8);
            
            var uid = BigEndianConverter.ConvertBytesToUInt16(blockBytes, 0x12);
            var gid = BigEndianConverter.ConvertBytesToUInt16(blockBytes, 0x14);
            var protection = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x16);
            var creationDate = DateHelper.ReadDate(blockBytes, 0x1a);

            var entries = new List<deldirentry>();
            var offset = 0x20; // first del dir entry offset
            for (var i = 0; i < SizeOf.DelDirBlock.Entries(g); i++)
            {
                var delDirEntry = DelDirEntryReader.Read(blockBytes, offset);
                entries.Add(delDirEntry);
                offset += SizeOf.DelDirEntry.Struct;
            }
            
            return new deldirblock(g)
            {
                id = id,
                datestamp = datestamp,
                seqnr = seqnr,
                uid = uid,
                gid = gid,
                protection = protection,
                CreationDate = creationDate,
                entries = entries.ToArray()
            };
        }
    }
}