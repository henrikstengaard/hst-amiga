namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.IO;
    using Blocks;
    using Core.Converters;

    public static class RootBlockExtensionReader
    {
        public static rootblockextension Parse(byte[] blockBytes)
        {
            var id = BigEndianConverter.ConvertBytesToUInt16(blockBytes);
            if (id != Constants.EXTENSIONID)
            {
                throw new IOException($"Invalid root block extension id '{id}'");
            }

            var ext_options = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x4);
            var datestamp = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x8);
            var pfs2version = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0xc);
            var root_date = DateHelper.ReadDate(blockBytes, 0x10);
            var volume_date = DateHelper.ReadDate(blockBytes, 0x16);

            var tobedone_operation_id = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x1c);
            var tobedone_argument1 = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x20);
            var tobedone_argument2 = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x24);
            var tobedone_argument3 = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x28);

            var reserved_roving = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x2c);
            var rovingbit = BigEndianConverter.ConvertBytesToUInt16(blockBytes, 0x30);
            var curranseqnr = BigEndianConverter.ConvertBytesToUInt16(blockBytes, 0x32);
            var deldirroving = BigEndianConverter.ConvertBytesToUInt16(blockBytes, 0x34);
            var deldirsize = BigEndianConverter.ConvertBytesToUInt16(blockBytes, 0x36);
            var fnsize = BigEndianConverter.ConvertBytesToUInt16(blockBytes, 0x38);

            var offset = 0x40;
            var superIndex = new uint[Constants.MAXSUPER + 1];
            for (var i = 0; i < superIndex.Length; i++)
            {
                superIndex[i] = BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset);
                offset += Amiga.SizeOf.ULong;
            }

            var dd_uid = BigEndianConverter.ConvertBytesToUInt16(blockBytes, 0x80);
            var dd_gid = BigEndianConverter.ConvertBytesToUInt16(blockBytes, 0x82);
            var dd_protection = BigEndianConverter.ConvertBytesToUInt32(blockBytes, 0x84);
            var dd_creationdate = DateHelper.ReadDate(blockBytes, 0x88);

            offset = 0x90;
            var deldir = new uint[32];
            for (var i = 0; i < deldir.Length; i++)
            {
                deldir[i] = BigEndianConverter.ConvertBytesToUInt32(blockBytes, offset);
                offset += Amiga.SizeOf.ULong;
            }

            return new rootblockextension
            {
                id = id,
                ext_options = ext_options,
                datestamp = datestamp,
                pfs2version = pfs2version,
                RootDate = root_date,
                VolumeDate = volume_date,
                tobedone = new postponed_op
                {
                    operation_id = tobedone_operation_id,
                    argument1 = tobedone_argument1,
                    argument2 = tobedone_argument2,
                    argument3 = tobedone_argument3
                },
                reserved_roving = reserved_roving,
                rovingbit = rovingbit,
                curranseqnr = curranseqnr,
                deldirroving = deldirroving,
                deldirsize = deldirsize,
                fnsize = fnsize,
                superindex = superIndex,
                dd_uid = dd_uid,
                dd_gid = dd_gid,
                dd_protection = dd_protection,
                dd_creationdate = dd_creationdate,
                deldir = deldir
            };
        }
    }
}