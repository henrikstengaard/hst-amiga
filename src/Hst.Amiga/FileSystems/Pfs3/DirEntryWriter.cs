namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using Blocks;
    using Core.Converters;

    public static class DirEntryWriter
    {
        public static void Write(byte[] data, int offset, direntry dirEntry)
        {
            data[offset] = dirEntry.next;
            data[offset + 1] = (byte)dirEntry.type;
            BigEndianConverter.ConvertUInt32ToBytes(dirEntry.anode, data, offset + 2);
            BigEndianConverter.ConvertUInt32ToBytes(dirEntry.fsize, data, offset + 6);
            DateHelper.WriteDate(dirEntry.CreationDate, data, offset + 10);
            data[offset + 16] = dirEntry.protection;
            data[offset + 17] = dirEntry.nlength;
            var nameBytes = AmigaTextHelper.GetBytes(dirEntry.Name);
            Array.Copy(nameBytes, 0, data, offset + 18, nameBytes.Length);

            if (dirEntry.ExtraFields != null)
            {
                // TODO: write extrafields after comment, if present
                // offset = (ushort)(SizeOf.DirEntry.Struct + (direntry.nlength) + (Macro.COMMENT(direntry) & 0xfffe));
                // dirext = (UWORD *)((UBYTE *)(direntry) + offset);
                // direntry.next = offset + 2 * j + 2;
            }
        }
    }
}