namespace Hst.Amiga.Tests.HunkTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using DataTypes.Hunks;

    /// <summary>
    /// Builds Amiga hunk files in memory for tests.
    /// </summary>
    public class HunkFileBuilder
    {
        public const uint MemoryChip = 0x40000000;
        public const uint MemoryFast = 0x80000000;
        public const uint MemoryExtended = 0xC0000000;

        private class TestHunk
        {
            public uint Type;
            public byte[] Data;
            public uint BssSize;
            public uint MemoryFlags;
            public (uint Offset, uint TargetHunk)[] Relocations;
        }

        private readonly List<TestHunk> hunks = new();

        public HunkFileBuilder AddCode(byte[] data, uint memoryFlags = 0,
            params (uint Offset, uint TargetHunk)[] relocations) =>
            Add(new TestHunk { Type = HunkIdentifiers.Code, Data = data, MemoryFlags = memoryFlags, Relocations = relocations });

        public HunkFileBuilder AddData(byte[] data, uint memoryFlags = 0,
            params (uint Offset, uint TargetHunk)[] relocations) =>
            Add(new TestHunk { Type = HunkIdentifiers.Data, Data = data, MemoryFlags = memoryFlags, Relocations = relocations });

        public HunkFileBuilder AddBss(uint size) =>
            Add(new TestHunk { Type = HunkIdentifiers.Bss, BssSize = size, Relocations = Array.Empty<(uint, uint)>() });

        private HunkFileBuilder Add(TestHunk hunk)
        {
            hunks.Add(hunk);
            return this;
        }

        public byte[] Build()
        {
            using var stream = new MemoryStream();

            // header without resident library names
            Write(stream, HunkIdentifiers.Header);
            Write(stream, 0);
            Write(stream, (uint)hunks.Count);
            Write(stream, 0);
            Write(stream, (uint)hunks.Count - 1);
            foreach (var hunk in hunks)
            {
                Write(stream, SizeInLongs(hunk) | hunk.MemoryFlags);
                if (hunk.MemoryFlags == MemoryExtended)
                {
                    // extended memory attributes
                    Write(stream, 0);
                }
            }

            foreach (var hunk in hunks)
            {
                // memory flags in hunk identifier, extended memory isn't allowed here
                Write(stream, hunk.Type | (hunk.MemoryFlags == MemoryExtended ? 0 : hunk.MemoryFlags));
                Write(stream, SizeInLongs(hunk));
                if (hunk.Data != null)
                {
                    stream.Write(Pad(hunk.Data));
                }

                if (hunk.Relocations.Length > 0)
                {
                    Write(stream, HunkIdentifiers.ReLoc32);
                    foreach (var group in hunk.Relocations.GroupBy(x => x.TargetHunk))
                    {
                        Write(stream, (uint)group.Count());
                        Write(stream, group.Key);
                        foreach (var relocation in group)
                        {
                            Write(stream, relocation.Offset);
                        }
                    }

                    Write(stream, 0);
                }

                Write(stream, HunkIdentifiers.End);
            }

            return stream.ToArray();
        }

        /// <summary>
        /// Write resident structure (romtag) to data at offset with pointers to name and id string offsets.
        /// Pointers rt_MatchTag, rt_EndSkip, rt_Name and rt_IdString needs relocations, see <see cref="RomTagRelocations"/>.
        /// </summary>
        public static void WriteRomTag(byte[] data, int offset, byte flags, byte version, byte type, sbyte priority,
            uint nameOffset, uint idStringOffset)
        {
            WriteUInt16(data, offset, RomTag.MatchWord);
            WriteUInt32(data, offset + 2, (uint)offset); // rt_MatchTag
            WriteUInt32(data, offset + 6, (uint)offset + 26); // rt_EndSkip
            data[offset + 10] = flags;
            data[offset + 11] = version;
            data[offset + 12] = type;
            data[offset + 13] = (byte)priority;
            WriteUInt32(data, offset + 14, nameOffset); // rt_Name
            WriteUInt32(data, offset + 18, idStringOffset); // rt_IdString
            WriteUInt32(data, offset + 22, 0); // rt_Init
        }

        /// <summary>
        /// Relocations for pointers in resident structure at offset.
        /// </summary>
        public static (uint Offset, uint TargetHunk)[] RomTagRelocations(int offset, uint romTagHunk,
            uint nameHunk, uint idStringHunk) =>
            new[]
            {
                ((uint)offset + 2, romTagHunk), // rt_MatchTag
                ((uint)offset + 6, romTagHunk), // rt_EndSkip
                ((uint)offset + 14, nameHunk), // rt_Name
                ((uint)offset + 18, idStringHunk) // rt_IdString
            };

        public static void WriteString(byte[] data, int offset, string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            Array.Copy(bytes, 0, data, offset, bytes.Length);
            data[offset + bytes.Length] = 0;
        }

        /// <summary>
        /// Create hunk file with a single code hunk containing a resident structure with name and id string.
        /// </summary>
        public static byte[] CreateRomTagHunkFile(string name, string idString, byte version = 1)
        {
            const int romTagOffset = 0x10;
            const int nameOffset = 0x40;
            var idStringOffset = nameOffset + name.Length + 1;

            var code = new byte[idStringOffset + idString.Length + 1 + 4];

            // moveq #-1,d0 and rts, returning when run as a program
            code[0] = 0x70;
            code[1] = 0xff;
            code[2] = 0x4e;
            code[3] = 0x75;

            WriteRomTag(code, romTagOffset, 1, version, 0, 0, nameOffset, (uint)idStringOffset);
            WriteString(code, nameOffset, name);
            WriteString(code, idStringOffset, idString);

            return new HunkFileBuilder()
                .AddCode(code, 0, RomTagRelocations(romTagOffset, 0, 0, 0))
                .Build();
        }

        private static uint SizeInLongs(TestHunk hunk) =>
            hunk.Data == null ? (hunk.BssSize + 3) / 4 : (uint)(hunk.Data.Length + 3) / 4;

        private static byte[] Pad(byte[] data)
        {
            var padded = new byte[(data.Length + 3) / 4 * 4];
            Array.Copy(data, padded, data.Length);
            return padded;
        }

        private static void Write(Stream stream, uint value)
        {
            var bytes = new byte[4];
            WriteUInt32(bytes, 0, value);
            stream.Write(bytes);
        }

        private static void WriteUInt32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)(value >> 24);
            data[offset + 1] = (byte)(value >> 16);
            data[offset + 2] = (byte)(value >> 8);
            data[offset + 3] = (byte)value;
        }

        private static void WriteUInt16(byte[] data, int offset, ushort value)
        {
            data[offset] = (byte)(value >> 8);
            data[offset + 1] = (byte)value;
        }
    }
}
