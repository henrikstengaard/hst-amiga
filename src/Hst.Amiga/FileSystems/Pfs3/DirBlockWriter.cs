namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using System.IO;
    using Blocks;
    using Core.Converters;

    public static class DirBlockWriter
    {
        public static byte[] BuildBlock(dirblock dirBlock, globaldata g)
        {
            var blockBytes = new byte[g.RootBlock.ReservedBlksize];
            if (dirBlock.BlockBytes != null)
            {
                Array.Copy(dirBlock.BlockBytes, 0, blockBytes, 0,
                    Math.Min(dirBlock.BlockBytes.Length, g.RootBlock.ReservedBlksize));
            }
            
            BigEndianConverter.ConvertUInt16ToBytes(Constants.DBLKID, blockBytes, 0); // offset 0
            BigEndianConverter.ConvertUInt16ToBytes(0, blockBytes, 2); // offset 2, not used 1
            BigEndianConverter.ConvertUInt32ToBytes(dirBlock.datestamp, blockBytes, 4); // 4
            BigEndianConverter.ConvertUInt16ToBytes(0, blockBytes, 8); // offset 8, not used 2
            BigEndianConverter.ConvertUInt16ToBytes(0, blockBytes, 0xa); // offset 10, not used 3
            BigEndianConverter.ConvertUInt32ToBytes(dirBlock.anodenr, blockBytes, 0xc); // offset 12
            BigEndianConverter.ConvertUInt32ToBytes(dirBlock.parent, blockBytes, 0x10); // offset 16
            
            var maxDirEntries = (SizeOf.DirBlock.Entries(g) / SizeOf.DirEntry.Struct) + 5;

            var dirEntriesNo = 0;
            var offset = 0x14;
            foreach (var dirEntry in dirBlock.DirEntries)
            {
                var next = dirEntry.Next;

                if (offset + next >= g.RootBlock.ReservedBlksize)
                {
                    throw new IOException($"Dir entry '{dirEntry.Name}' at offset {offset} with next {next} exceeds block size {g.RootBlock.ReservedBlksize}");
                }
                
                DirEntryWriter.Write(blockBytes, offset, next, dirEntry, g);
                offset += next;
                
                dirEntriesNo++;
                if (dirEntriesNo >= maxDirEntries)
                {
                    throw new IOException($"Read entries from dir block exceeded max entries, possibly corrupt dir block");
                }
            }

            // end of dir entries, next = 0
            blockBytes[offset] = 0;
            
            dirBlock.BlockBytes = blockBytes;

            return blockBytes;
        }
    }
}