namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;
    using Core.Extensions;

    public static class DirBlockWriter
    {
        public static async Task<byte[]> BuildBlock(dirblock dirblock, globaldata g)
        {
            var blockStream = dirblock.BlockBytes == null || dirblock.BlockBytes.Length == 0 ?
                new MemoryStream() : new MemoryStream(dirblock.BlockBytes);
                
            await blockStream.WriteBigEndianUInt16(dirblock.id);
            await blockStream.WriteBigEndianUInt16(dirblock.not_used_1);
            await blockStream.WriteBigEndianUInt32(dirblock.datestamp);
            
            // not_used_2
            for (var i = 0; i < 2; i++)
            {
                await blockStream.WriteBigEndianUInt16(0);
            }
            
            await blockStream.WriteBigEndianUInt32(dirblock.anodenr);
            await blockStream.WriteBigEndianUInt32(dirblock.parent);

            var entriesSize = SizeOf.DirBlock.Entries(g);
            var maxDirEntries = (SizeOf.DirBlock.Entries(g) / SizeOf.DirEntry.Struct) + 5;

            var entries = new byte[entriesSize];

            var dirEntriesNo = 0;
            var offset = 0;
            foreach (var dirEntry in dirblock.DirEntries)
            {
                var next = direntry.EntrySize(dirEntry, g);
                // if (dirEntry.next == 0)
                // {
                //     throw new IOException("Dir entry has next 0");
                // }

                if (offset + next >= entriesSize)
                {
                    throw new IOException($"Dir entry at offset {offset} with next {next} exceeds size of entries {entriesSize}");
                }
                
                DirEntryWriter.Write(entries, offset, next, dirEntry, g);
                offset += next;
                
                dirEntriesNo++;
                if (dirEntriesNo >= maxDirEntries)
                {
                    throw new IOException($"Read entries from dir block exceeded max entries, possibly corrupt dir block");
                }
            }
            
            await blockStream.WriteBytes(entries);
                
            var blockBytes = blockStream.ToArray();
            dirblock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}