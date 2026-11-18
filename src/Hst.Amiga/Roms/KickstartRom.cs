using System;
using System.Collections.Generic;
using System.IO;

namespace Hst.Amiga.Roms
{
    /// <summary>
    /// A decoded Kickstart ROM and the resident modules discovered in it.
    /// </summary>
    public class KickstartRom
    {
        internal KickstartRom(byte[] bytes, bool isEncrypted, uint baseAddress,
            IReadOnlyList<KickstartRomModule> modules)
        {
            Bytes = bytes;
            IsEncrypted = isEncrypted;
            BaseAddress = baseAddress;
            Modules = modules;
        }

        /// <summary>
        /// Decoded ROM image bytes.
        /// </summary>
        public byte[] Bytes { get; }

        /// <summary>
        /// True if the source image used the Amiga Forever encrypted ROM wrapper.
        /// </summary>
        public bool IsEncrypted { get; }

        /// <summary>
        /// Amiga address at which the ROM image is mapped, as discovered from resident tags.
        /// </summary>
        public uint BaseAddress { get; }

        /// <summary>
        /// Resident modules found in the ROM, in image order.
        /// </summary>
        public IReadOnlyList<KickstartRomModule> Modules { get; }

        /// <summary>
        /// Return a copy of the bytes belonging to a resident module.
        /// </summary>
        public byte[] GetModuleBytes(KickstartRomModule module)
        {
            if (module == null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            if (module.Offset < 0 || module.Size < 0 || module.Offset > Bytes.Length - module.Size)
            {
                throw new ArgumentException("Module range is outside the ROM image.", nameof(module));
            }

            var result = new byte[module.Size];
            Array.Copy(Bytes, module.Offset, result, 0, module.Size);
            return result;
        }

        /// <summary>
        /// Return the module bytes wrapped in a single relocatable Amiga code hunk.
        /// Absolute pointers into the module are rebased and recorded as HUNK_RELOC32 relocations.
        /// </summary>
        public byte[] GetModuleHunkBytes(KickstartRomModule module)
        {
            var moduleBytes = GetModuleBytes(module);
            if (moduleBytes.Length == 0)
            {
                throw new ArgumentException("Module is empty.", nameof(module));
            }

            var scsiPrelude = module.Name == "scsi.device" ? GetScsiCompatibility.FindPrelude(Bytes, module.Offset) : 0;
            var prefixSize = scsiPrelude != 0 ? scsiPrelude : FindExecutablePrefixSize(module);
            var codeOffset = module.Offset - prefixSize;
            var codeLength = prefixSize + module.Size;
            var code = new byte[codeLength];
            Array.Copy(Bytes, codeOffset, code, 0, codeLength);

            if (scsiPrelude != 0)
            {
                var scsiRelocations = GetScsiCompatibility.RebaseRomCode(code, scsiPrelude,
                    BaseAddress + (uint)codeOffset, false);
                return WriteCodeHunk(code, scsiRelocations);
            }

            var codeAddress = (ulong)BaseAddress + (uint)codeOffset;
            var moduleEndAddress = (ulong)BaseAddress + (uint)module.Offset + (uint)module.Size;
            var relocations = new List<uint>();

            // Resident structures and other pointers can be only word-aligned, so examine
            // each even byte offset for a longword pointing into this ROM slice.
            for (var offset = 0; offset <= code.Length - 4; offset += 2)
            {
                var address = ReadUInt32(code, offset);
                if (address < codeAddress || address > moduleEndAddress)
                {
                    continue;
                }

                WriteUInt32(code, offset, (uint)(address - codeAddress));
                relocations.Add((uint)offset);
            }

            return WriteCodeHunk(code, relocations);
        }

        internal static byte[] WriteCodeHunk(byte[] hunkCode, IReadOnlyList<uint> relocations)
        {
            using var stream = new MemoryStream();
            WriteUInt32(stream, 0x3F3); // HUNK_HEADER
            WriteUInt32(stream, 0); // resident library names terminator
            WriteUInt32(stream, 1); // table size
            WriteUInt32(stream, 0); // first hunk
            WriteUInt32(stream, 0); // last hunk
            WriteUInt32(stream, (uint)((hunkCode.Length + 3) / 4));

            WriteUInt32(stream, 0x3E9); // HUNK_CODE
            WriteUInt32(stream, (uint)((hunkCode.Length + 3) / 4));
            stream.Write(hunkCode, 0, hunkCode.Length);
            var padding = (4 - hunkCode.Length % 4) % 4;
            for (var i = 0; i < padding; i++)
            {
                stream.WriteByte(0);
            }

            if (relocations.Count > 0)
            {
                WriteUInt32(stream, 0x3EC); // HUNK_RELOC32
                WriteUInt32(stream, (uint)relocations.Count);
                WriteUInt32(stream, 0); // target hunk number
                foreach (var relocation in relocations)
                {
                    WriteUInt32(stream, relocation);
                }

                WriteUInt32(stream, 0); // relocation table terminator
            }

            WriteUInt32(stream, 0x3F2); // HUNK_END
            return stream.ToArray();
        }

        private int FindExecutablePrefixSize(KickstartRomModule module)
        {
            // Some resident modules have an entry stub immediately before the resident tag.
            // Retain a branch prefix only when its target falls inside the module range.
            var minimumOffset = Math.Max(0, module.Offset - 0x100);
            for (var candidate = module.Offset - 2; candidate >= minimumOffset; candidate -= 2)
            {
                var opcode = ReadUInt16(Bytes, candidate);
                if ((opcode & 0xFF00) != 0x6000)
                {
                    continue;
                }

                int branchTarget;
                var displacement = opcode & 0x00FF;
                if (displacement == 0)
                {
                    if (candidate + 4 > Bytes.Length)
                    {
                        continue;
                    }

                    // A 68000 word branch is relative to the displacement word.
                    branchTarget = candidate + 2 + (short)ReadUInt16(Bytes, candidate + 2);
                }
                else
                {
                    branchTarget = candidate + 2 + (sbyte)displacement;
                }

                if (branchTarget >= module.Offset && branchTarget < module.Offset + module.Size)
                {
                    return module.Offset - candidate;
                }
            }

            return 0;
        }

        private static ushort ReadUInt16(byte[] bytes, int offset) =>
            (ushort)(bytes[offset] << 8 | bytes[offset + 1]);

        private static uint ReadUInt32(byte[] bytes, int offset) =>
            (uint)(bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3]);

        private static void WriteUInt32(byte[] bytes, int offset, uint value)
        {
            bytes[offset] = (byte)(value >> 24);
            bytes[offset + 1] = (byte)(value >> 16);
            bytes[offset + 2] = (byte)(value >> 8);
            bytes[offset + 3] = (byte)value;
        }

        private static void WriteUInt32(Stream stream, uint value)
        {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }
    }
}
