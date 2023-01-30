namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;
    using Core.Extensions;

    public static class DirBlockReader
    {
        public static async Task<dirblock> Parse(byte[] blockBytes, globaldata g)
        {
            var blockStream = new MemoryStream(blockBytes);

            var id = await blockStream.ReadBigEndianUInt16();
            var not_used = await blockStream.ReadBigEndianUInt16();
            var datestamp = await blockStream.ReadBigEndianUInt32();
            
            // not_used_2
            for (var i = 0; i < 2; i++)
            {
                await blockStream.ReadBigEndianUInt16();
            }
            var anodenr = await blockStream.ReadBigEndianUInt32();
            var parent = await blockStream.ReadBigEndianUInt32();

            if (id == 0)
            {
                return null;
            }

            var entries = await blockStream.ReadBytes(SizeOf.DirBlock.Entries(g));
            
            var maxDirEntries = (SizeOf.DirBlock.Entries(g) / SizeOf.DirEntry.Struct) + 5;
            var entryIndex = 0;
            var dirEntriesNo = 0;
            var dirEntries = new List<direntry>(maxDirEntries);
            var position = 0;
            do
            {
                var entry = DirEntryReader.Read(entries, entryIndex, g);
                if (entry.Next == 0)
                {
                    break;
                }

                entry.Position = position++;
                
                dirEntries.Add(entry);
                dirEntriesNo++;
                if (dirEntriesNo >= maxDirEntries)
                {
                    throw new IOException($"Read entries from dir block exceeded max entries, possibly corrupt dir block");
                }
                entryIndex += entry.Next;
            } while (entryIndex < entries.Length);
            
            return new dirblock(g)
            {
                id = id,
                not_used_1 = not_used,
                datestamp = datestamp,
                anodenr = anodenr,
                parent = parent,
                DirEntries = dirEntries
            };
        }
    }
}