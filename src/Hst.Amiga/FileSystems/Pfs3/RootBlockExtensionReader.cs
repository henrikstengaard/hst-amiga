namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;
    using Core.Extensions;

    public static class RootBlockExtensionReader
    {
        public static async Task<rootblockextension> Parse(byte[] blockBytes)
        {
            var blockStream = new MemoryStream(blockBytes);

            var id = await blockStream.ReadBigEndianUInt16();
            
            if (id == 0)
            {
                return null;
            }
            
            var not_used_1 = await blockStream.ReadBigEndianUInt16();
            var ext_options = await blockStream.ReadBigEndianUInt32();
            var datestamp = await blockStream.ReadBigEndianUInt32();
            var pfs2version = await blockStream.ReadBigEndianUInt32();
            var root_date = await DateHelper.ReadDate(blockStream);
            var volume_date = await DateHelper.ReadDate(blockStream);

            var tobedone_operation_id = await blockStream.ReadBigEndianUInt32();
            var tobedone_argument1 = await blockStream.ReadBigEndianUInt32();
            var tobedone_argument2 = await blockStream.ReadBigEndianUInt32();
            var tobedone_argument3 = await blockStream.ReadBigEndianUInt32();

            var reserved_roving = await blockStream.ReadBigEndianUInt32();
            var rovingbit = await blockStream.ReadBigEndianUInt16();
            var curranseqnr = await blockStream.ReadBigEndianUInt16();
            var deldirroving = await blockStream.ReadBigEndianUInt16();
            var deldirsize = await blockStream.ReadBigEndianUInt16();
            var fnsize = await blockStream.ReadBigEndianUInt16();

            // not_used_2[3]
            for (var i = 0; i < 3; i++)
            {
                await blockStream.ReadBigEndianUInt16();
            }

            var superindex = new List<uint>();
            for (var i = 0; i < Constants.MAXSUPER + 1; i++)
            {
                superindex.Add(await blockStream.ReadBigEndianUInt32());
            }

            var dd_uid = await blockStream.ReadBigEndianUInt16();
            var dd_gid = await blockStream.ReadBigEndianUInt16();
            var dd_protection = await blockStream.ReadBigEndianUInt32();
            var dd_creationdate = await DateHelper.ReadDate(blockStream);
            var not_used_3 = await blockStream.ReadBigEndianUInt16();

            var deldir = new List<uint>();
            for (var i = 0; i < 32; i++)
            {
                deldir.Add(await blockStream.ReadBigEndianUInt32());
            }

            return new rootblockextension
            {
                id = id,
                not_used_1 = not_used_1,
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
                superindex = superindex.ToArray(),
                dd_uid = dd_uid,
                dd_gid = dd_gid,
                dd_protection = dd_protection,
                dd_creationdate = dd_creationdate,
                deldir = deldir.ToArray()
            };
        }
    }
}