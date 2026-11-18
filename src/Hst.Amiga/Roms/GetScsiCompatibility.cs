using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hst.Amiga.DataTypes.Hunks;

namespace Hst.Amiga.Roms
{
    /// <summary>
    /// Applies GetSCSI 1.1 compatibility changes to supported scsidisk layouts.
    /// </summary>
    public static class GetScsiCompatibility
    {
        private const int ResidentHeaderSize = 26;
        private const uint JsrTaggedOpenLibrary = 0x4EAEFCD6;
        private const uint JsrAddBootNode = 0x4EAEFFDC;
        private const ushort MoveqExpansionTag = 0x7006; // MOVEQ #6,D0

        private static readonly int[] ResidentPointers = { 2, 6, 14, 18, 22 };
        private static readonly byte[] DosName = Encoding.ASCII.GetBytes("dos.library\0");
        private static readonly byte[] TaggedInitialization = { 0x4E, 0xAE, 0xFE, 0xDA, 0x2A, 0x00 };

        private static readonly byte[] OpenExpansionLibrary =
        {
            0x43, 0xFA, 0x00, 0x06, // LEA expansion_name(PC),A1
            0x4E, 0xEE, 0xFE, 0x68  // JMP -408(A6): Exec OldOpenLibrary
        };

        // GetSCSI leaves an extra zero word between the terminated name and the next routine.
        private static readonly byte[] ExpansionName = Encoding.ASCII.GetBytes("expansion.library\0\0\0");

        private static readonly byte[] GuardedAddBootNode =
        {
            0x0C, 0x6E, 0x00, 0x24, 0x00, 0x14, // CMPI.W #36,20(A6): lib_Version
            0x65, 0x04,                         // BCS.S return
            0x4E, 0xEE, 0xFF, 0xDC,             // JMP -36(A6): Expansion AddBootNode
            0x4E, 0x75,                         // return: RTS
            0x2F, 0x0E                          // Trailing MOVE.L A6,-(SP) in the GetSCSI export
        };

        /// <summary>Extracts a GetSCSI-compatible Hunk from a supported decoded Kickstart ROM.</summary>
        public static byte[] Extract(KickstartRom rom)
        {
            var matches = rom.Modules.Where(module => module.Name == "scsi.device").ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidDataException("Expected exactly one scsi.device resident in the ROM.");
            }

            var module = matches[0];
            var prefix = FindPrelude(rom.Bytes, module.Offset);
            if (prefix == 0)
            {
                throw new InvalidDataException("Unrecognized scsi.device ROM prelude.");
            }

            var start = module.Offset - prefix;
            var code = rom.Bytes.AsSpan(start, prefix + module.Size).ToArray();
            var relocations = RebaseRomCode(code, prefix, rom.BaseAddress + (uint)start, true);
            VerifyResident(code, prefix, code.Length);
            return KickstartRom.WriteCodeHunk(Apply(code, prefix), relocations);
        }

        internal static IReadOnlyList<uint> RebaseRomCode(byte[] code, int prefix, uint address, bool rejectExternal)
        {
            var relocations = ResidentRelocations(prefix).ToList();
            for (var i = prefix + ResidentHeaderSize; i <= code.Length - 6; i += 2)
            {
                if (!IsAbsoluteCall(code, i)) continue;

                var target = ReadInstruction(code, i + 2);
                if (target >= address && target - address < code.Length)
                {
                    relocations.Add((uint)(i + 2));
                }
                else if (rejectExternal && IsRomAddress(target))
                {
                    throw new InvalidDataException($"scsi.device calls ROM address 0x{target:X6} outside the module.");
                }
            }

            foreach (var offset in relocations)
            {
                var field = code.AsSpan((int)offset);
                var pointer = BinaryPrimitives.ReadUInt32BigEndian(field);
                if (pointer < address || pointer > address + (uint)code.Length)
                {
                    throw new InvalidDataException("scsi.device has a resident pointer outside its ROM module.");
                }

                BinaryPrimitives.WriteUInt32BigEndian(field, (uint)(pointer - address));
            }

            return relocations;
        }

        /// <summary>Applies GetSCSI's call-site changes to a supported, relocatable scsi.device Hunk.</summary>
        public static async Task<byte[]> Patch(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes, false);
            var hunks = (await HunkReader.Read(stream)).ToArray();
            if (stream.Position != stream.Length || hunks.Length != 4 || !(hunks[0] is Header header) ||
                header.TableSize != 1 || header.FirstHunkSlot != 0 || header.LastHunkSlot != 0 ||
                header.ResidentLibraryNames.Any() || !(hunks[1] is Code code) ||
                header.HunkSizes.Single() != (uint)code.Data.Length / 4 || !(hunks[3] is End))
            {
                throw new InvalidDataException("Expected a single-code-hunk scsi.device with relocations.");
            }

            var entries = hunks[2] switch
            {
                ReLoc32 reloc32 => reloc32.Entries.ToArray(),
                ReLoc32Short reloc32Short => reloc32Short.Entries.ToArray(),
                _ => Array.Empty<ReLoc32Entry>()
            };
            var relocations = entries.SelectMany(entry => entry.Offsets).OrderBy(offset => offset).ToArray();
            // The first resident relocation is rt_MatchTag, two bytes after the resident.
            if (relocations.Length < ResidentPointers.Length || entries.Any(entry => entry.HunkNumber != 0) ||
                relocations[0] < 2 || relocations.Distinct().Count() != relocations.Length)
            {
                throw new InvalidDataException("Invalid scsi.device relocations.");
            }

            if (relocations[0] > 0x42)
            {
                throw new InvalidDataException("Unrecognized scsi.device Hunk prelude.");
            }

            var prefix = (int)relocations[0] - 2;
            if (prefix != 0x30 && prefix != 0x40 || FindPrelude(code.Data, prefix) != prefix ||
                !ResidentRelocations(prefix).SequenceEqual(relocations.Take(ResidentPointers.Length)))
            {
                throw new InvalidDataException("Unrecognized scsi.device Hunk prelude or resident relocations.");
            }

            var originalSize = GetOriginalSize(code.Data, prefix);
            VerifyResident(code.Data, prefix, originalSize);
            foreach (var offset in relocations)
            {
                if ((offset & 1) != 0 || (ulong)offset + 4 > (ulong)originalSize ||
                    ReadInstruction(code.Data, (int)offset) > originalSize)
                {
                    throw new InvalidDataException($"Invalid scsi.device relocation at 0x{offset:X}.");
                }
            }

            for (var i = prefix + ResidentHeaderSize; i <= originalSize - 6; i += 2)
            {
                if (IsAbsoluteCall(code.Data, i) && IsRomAddress(ReadInstruction(code.Data, i + 2)))
                {
                    throw new InvalidDataException("scsi.device contains an unrelocated absolute ROM call.");
                }
            }

            var patched = Apply(code.Data, prefix);
            return ReferenceEquals(patched, code.Data) ? bytes : KickstartRom.WriteCodeHunk(patched, relocations);
        }

        private static IEnumerable<uint> ResidentRelocations(int prefix) =>
            ResidentPointers.Select(offset => (uint)(prefix + offset));

        internal static int FindPrelude(byte[] code, int resident)
        {
            if (resident < 0x30 || resident > code.Length) return 0;

            // Older layouts include a romboot.device string and put the DOS lookup helper
            // 50 bytes before the tag. Later layouts put the helper 34 bytes before it.
            if (resident >= 0x40 && ReadInstruction(code, resident - 0x40) == 0x90000000 &&
                HasDosLookup(code, resident, 50)) return 0x40;
            if (ReadInstruction(code, resident - 0x30) != 0x90000000 ||
                !HasDosLookup(code, resident, 34)) return 0;

            return resident >= 0x40 && ReadInstruction(code, resident - 0x40) == 0x6000200A &&
                   ReadInstruction(code, resident - 0x3C) == 0x4CDF4C00 ? 0x40 : 0x30;
        }

        private static bool HasDosLookup(byte[] code, int resident, int distance)
        {
            var helper = resident - distance;
            var target = helper + 2 + (short)ReadWord(code, helper + 2);
            return ReadWord(code, helper) == 0x43FA && ReadInstruction(code, helper + 4) == 0x4EAEFFA0 &&
                   target >= helper + 8 && target <= resident - DosName.Length &&
                   code.AsSpan(target, DosName.Length).SequenceEqual(DosName);
        }

        private static void VerifyResident(byte[] code, int prefix, int size)
        {
            if (size - prefix < ResidentHeaderSize || ReadWord(code, prefix) != 0x4AFC ||
                ReadInstruction(code, prefix + 2) != prefix || ReadInstruction(code, prefix + 6) != size ||
                ReadInstruction(code, prefix + 22) >= size ||
                ReadString(prefix + 14) != "scsi.device" ||
                ReadString(prefix + 18).IndexOf("scsidisk ", StringComparison.OrdinalIgnoreCase) < 0)
            {
                throw new InvalidDataException("Unsupported scsi.device resident layout.");
            }

            string ReadString(int field)
            {
                var offset = ReadInstruction(code, field);
                if (offset >= size) return string.Empty;
                var end = Array.IndexOf(code, (byte)0, (int)offset, size - (int)offset);
                return end < 0 ? string.Empty : Encoding.ASCII.GetString(code, (int)offset, end - (int)offset);
            }
        }

        private static byte[] Apply(byte[] code, int prefix)
        {
            var originalSize = GetOriginalSize(code, prefix);
            var openEntry = (originalSize + 3) & ~3;
            var bootEntry = openEntry + OpenExpansionLibrary.Length + ExpansionName.Length;
            var alreadyPatched = code.Length == bootEntry + GuardedAddBootNode.Length &&
                code.AsSpan(openEntry, OpenExpansionLibrary.Length).SequenceEqual(OpenExpansionLibrary) &&
                code.AsSpan(openEntry + OpenExpansionLibrary.Length, ExpansionName.Length).SequenceEqual(ExpansionName) &&
                code.AsSpan(bootEntry).SequenceEqual(GuardedAddBootNode);
            if (!alreadyPatched && code.Length != originalSize && code.Length != openEntry)
            {
                throw new InvalidDataException("Unexpected bytes after the scsi.device resident range.");
            }

            var opens = new List<int>();
            var boots = new List<int>();
            for (var i = prefix + ResidentHeaderSize + 12; i <= originalSize - 10; i += 2)
            {
                var instruction = ReadInstruction(code, i);
                if (instruction == (alreadyPatched ? BranchToSubroutine(i, openEntry) : JsrTaggedOpenLibrary))
                {
                    // MOVEQ #6,D0; call; TST.L D0. Also require the expected first-open or retry context.
                    if (ReadWord(code, i - 2) != MoveqExpansionTag || ReadWord(code, i + 4) != 0x4A80 ||
                        (!code.AsSpan(i - 8, 6).SequenceEqual(TaggedInitialization) &&
                         (ReadWord(code, i - 6) != 0x6600 || ReadWord(code, i + 6) != 0x6700)))
                    {
                        throw new InvalidDataException("Unrecognized scsi.device TaggedOpenLibrary call site.");
                    }

                    opens.Add(i);
                }

                if (instruction == (alreadyPatched ? BranchToSubroutine(i, bootEntry) : JsrAddBootNode))
                {
                    // MOVEA.L d(A5),A1; MOVEQ #1,D1; MOVE.L D3,D0; call; restore ExecBase.
                    if (ReadWord(code, i - 8) != 0x226D || ReadInstruction(code, i - 4) != 0x72012003 ||
                        (ReadWord(code, i + 4) != 0x2F0E && ReadWord(code, i + 4) != 0x224E) ||
                        ReadInstruction(code, i + 6) != 0x2C6D0024)
                    {
                        throw new InvalidDataException("Unrecognized scsi.device AddBootNode call site.");
                    }

                    boots.Add(i);
                }
            }

            var opensByName = code.AsSpan(prefix, originalSize - prefix)
                .IndexOf(ExpansionName.AsSpan(0, ExpansionName.Length - 2)) >= 0;
            if ((opens.Count != 2 || boots.Count != 1) &&
                !(opens.Count == 0 && opensByName && boots.Count <= 1 && (!alreadyPatched || boots.Count == 1)))
            {
                throw new InvalidDataException("Unrecognized scsi.device library call layout.");
            }

            if (alreadyPatched)
            {
                if (ContainsOriginalCall(code, prefix, originalSize))
                {
                    throw new InvalidDataException("scsi.device still contains unpatched library calls.");
                }

                return code;
            }

            if (boots.Count == 0) return code;

            var patched = new byte[bootEntry + GuardedAddBootNode.Length];
            code.CopyTo(patched, 0);
            OpenExpansionLibrary.CopyTo(patched, openEntry);
            ExpansionName.CopyTo(patched, openEntry + OpenExpansionLibrary.Length);
            GuardedAddBootNode.CopyTo(patched, bootEntry);
            foreach (var offset in opens)
            {
                BinaryPrimitives.WriteUInt32BigEndian(patched.AsSpan(offset), BranchToSubroutine(offset, openEntry));
            }

            var boot = boots.Single();
            BinaryPrimitives.WriteUInt32BigEndian(patched.AsSpan(boot), BranchToSubroutine(boot, bootEntry));
            return patched;
        }

        private static bool ContainsOriginalCall(byte[] code, int prefix, int size)
        {
            for (var i = prefix + ResidentHeaderSize; i <= size - 4; i += 2)
            {
                var call = ReadInstruction(code, i);
                if (call == JsrTaggedOpenLibrary || call == JsrAddBootNode) return true;
            }

            return false;
        }

        private static int GetOriginalSize(byte[] code, int prefix)
        {
            if (code.Length - prefix < ResidentHeaderSize)
            {
                throw new InvalidDataException("Truncated scsi.device resident.");
            }

            var size = ReadInstruction(code, prefix + 6);
            if (size < prefix + ResidentHeaderSize || size > code.Length || size > short.MaxValue - 32)
            {
                throw new InvalidDataException("scsi.device resident range cannot be patched with word branches.");
            }

            return (int)size;
        }

        private static bool IsAbsoluteCall(byte[] code, int offset) =>
            ReadWord(code, offset) == 0x4EB9 || ReadWord(code, offset) == 0x4EF9;

        private static bool IsRomAddress(uint address) => address >= 0xD00000 && address < 0x1000000;

        private static ushort ReadWord(byte[] code, int offset) =>
            BinaryPrimitives.ReadUInt16BigEndian(code.AsSpan(offset));

        private static uint ReadInstruction(byte[] code, int offset) =>
            BinaryPrimitives.ReadUInt32BigEndian(code.AsSpan(offset));

        private static uint BranchToSubroutine(int offset, int target)
        {
            var displacement = checked((short)(target - (offset + 2)));
            return 0x61000000U | (ushort)displacement;
        }
    }
}
