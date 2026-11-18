using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Hst.Amiga.Roms
{
    /// <summary>
    /// Extracts relocatable code hunks using a matching Remus/ROMSplit split-data catalog.
    /// </summary>
    public static class RemusRomSplitter
    {
        private const uint End = uint.MaxValue;

        /// <summary>
        /// Uses the catalog's module ranges, relocations and extraction fixes. Optional patches are skipped.
        /// </summary>
        public static IReadOnlyList<(int Offset, string Name, byte[] Bytes)> Extract(byte[] romBytes,
            byte[] splitData, byte[] keyBytes = null)
        {
            var rom = KickstartRomReader.Decode(romBytes, keyBytes, out _);
            var (catalog, baseAddress, entries) = FindModules(rom, splitData);
            return entries.Select(entry =>
            {
                var code = rom.AsSpan(entry.Offset, entry.Size).ToArray();
                catalog.Seek(entry.Extra);
                return (entry.Offset, entry.Name,
                    ExtractModule(catalog, code, (ulong)baseAddress + (uint)entry.Offset, entry.Name));
            }).ToList();
        }

        /// <summary>Lists the module ranges from a matching catalog without decoding relocation records.</summary>
        public static IReadOnlyList<(int Offset, int Size, string Name)> List(byte[] romBytes,
            byte[] splitData, byte[] keyBytes = null)
        {
            var rom = KickstartRomReader.Decode(romBytes, keyBytes, out _);
            var (_, _, modules) = FindModules(rom, splitData);
            return modules.Select(module => (module.Offset, module.Size, module.Name)).ToList();
        }

        private static (CatalogReader Catalog, uint BaseAddress,
            List<(int Offset, int Size, string Name, uint Extra)> Entries) FindModules(byte[] rom, byte[] splitData)
        {
            var catalog = new CatalogReader(splitData);
            if (catalog.ReadUInt32() != 0x524D5346) // RMSF
            {
                throw new InvalidDataException("Not a Remus/ROMSplit split-data catalog (RMSF).");
            }

            var version = catalog.ReadUInt32();
            if (version != 0x20005 && version != 0x20007)
            {
                throw new InvalidDataException($"Unsupported Remus split-data version 0x{version:X}.");
            }

            uint count;
            while ((count = catalog.ReadUInt32()) != End)
            {
                var checksumOffset = catalog.ReadUInt32();
                var checksum = catalog.ReadUInt32();
                var size = catalog.ReadUInt32();
                catalog.ReadUInt32(); // ROM name offset
                var baseAddress = catalog.ReadUInt32();
                catalog.ReadUInt32(); // short ROM name offset
                if (version == 0x20007)
                {
                    catalog.ReadUInt32(); // ROM flags
                }

                if (count > catalog.Remaining / 16)
                {
                    throw new InvalidDataException("Invalid module count in Remus split data.");
                }

                if (size != rom.Length || !MatchesChecksum(rom, checksumOffset, checksum))
                {
                    catalog.Skip((int)count * 16);
                    ReadModuleTerminator(catalog);
                    continue;
                }

                var entries = new List<(uint Name, uint Offset, uint Size, uint Extra)>();
                for (var i = 0U; i < count; i++)
                {
                    entries.Add((catalog.ReadUInt32(), catalog.ReadUInt32(), catalog.ReadUInt32(),
                        catalog.ReadUInt32()));
                }

                ReadModuleTerminator(catalog);
                var modules = entries.OrderBy(entry => entry.Offset).Select(entry =>
                {
                    var name = catalog.ReadString(entry.Name);
                    if (entry.Size == 0 || (ulong)entry.Offset + entry.Size > (ulong)rom.Length)
                    {
                        throw new InvalidDataException($"Remus module '{name}' is outside the ROM image.");
                    }

                    return ((int)entry.Offset, (int)entry.Size, name, entry.Extra);
                }).ToList();
                return (catalog, baseAddress, modules);
            }

            throw new InvalidDataException("No matching ROM size and checksum in the Remus split-data catalog.");
        }

        private static bool MatchesChecksum(byte[] rom, uint offset, uint checksum)
        {
            if (offset != End && ((offset & 3) != 0 || (ulong)offset + 4 > (ulong)rom.Length ||
                                  BinaryPrimitives.ReadUInt32BigEndian(rom.AsSpan((int)offset)) != checksum))
            {
                return false;
            }

            uint sum = 0;
            for (var pos = 0; pos <= rom.Length - 4; pos += 4)
            {
                if (pos == offset)
                {
                    continue;
                }

                var next = (ulong)sum + BinaryPrimitives.ReadUInt32BigEndian(rom.AsSpan(pos));
                sum = unchecked((uint)next) + (uint)(next >> 32);
            }

            return ~sum == checksum;
        }

        private static void ReadModuleTerminator(CatalogReader catalog)
        {
            if (catalog.ReadUInt32() != End)
            {
                throw new InvalidDataException("Missing module list terminator in Remus split data.");
            }
        }

        private static byte[] ExtractModule(CatalogReader catalog, byte[] code, ulong address, string name)
        {
            var flags = catalog.ReadUInt16();
            if ((flags & ~0xDF) != 0)
            {
                throw new InvalidDataException($"Unsupported Remus flags 0x{flags:X} for '{name}'.");
            }

            if ((flags & 0x04) != 0) catalog.Skip(catalog.ReadUInt16() * 6); // optional short-offset patches
            if ((flags & 0x08) != 0) catalog.Skip(catalog.ReadUInt16() * 8); // optional long-offset patches

            var relocations = new List<uint>();
            if ((flags & 0x01) != 0) ReadRelocations(false);
            if ((flags & 0x02) != 0) ReadRelocations(true);
            if ((flags & 0x10) != 0 && catalog.ReadUInt16() != 0)
            {
                throw new InvalidDataException($"Remus BCPL relocations are not supported for '{name}'.");
            }

            if ((flags & 0x80) != 0)
            {
                var count = catalog.ReadUInt16();
                for (var i = 0; i < count; i++)
                {
                    var offset = catalog.ReadUInt32();
                    BinaryPrimitives.WriteUInt32BigEndian(AtOffset(offset), catalog.ReadUInt32());
                }
            }

            if ((flags & 0x40) != 0) catalog.ReadUInt32(); // optional module checksum
            return KickstartRom.WriteCodeHunk(code, relocations);

            void ReadRelocations(bool longOffsets)
            {
                var count = catalog.ReadUInt16();
                for (var i = 0; i < count; i++)
                {
                    uint offset = longOffsets ? catalog.ReadUInt32() : catalog.ReadUInt16();
                    var field = AtOffset(offset);
                    var pointer = BinaryPrimitives.ReadUInt32BigEndian(field);
                    if (pointer < address)
                    {
                        throw new InvalidDataException($"Invalid Remus relocation at 0x{offset:X} in '{name}'.");
                    }

                    BinaryPrimitives.WriteUInt32BigEndian(field, (uint)(pointer - address));
                    relocations.Add(offset);
                }
            }

            Span<byte> AtOffset(uint offset)
            {
                if ((ulong)offset + 4 > (ulong)code.Length)
                {
                    throw new InvalidDataException($"Remus edit at 0x{offset:X} is outside module '{name}'.");
                }

                return code.AsSpan((int)offset, 4);
            }
        }

        private sealed class CatalogReader
        {
            private readonly byte[] data;
            private int position;

            public CatalogReader(byte[] data) => this.data = data ?? throw new ArgumentNullException(nameof(data));

            public int Remaining => data.Length - position;

            public uint ReadUInt32() => BinaryPrimitives.ReadUInt32BigEndian(Take(4));
            public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16BigEndian(Take(2));
            public void Skip(int length) => Take(length);

            public void Seek(uint offset)
            {
                if (offset >= data.Length)
                {
                    throw new InvalidDataException("Offset outside the Remus split-data catalog.");
                }

                position = (int)offset;
            }

            public string ReadString(uint offset)
            {
                Seek(offset);
                var end = Array.IndexOf(data, (byte)0, position);
                if (end < 0)
                {
                    throw new InvalidDataException("Unterminated name in Remus split data.");
                }

                return AmigaTextHelper.Iso88591.GetString(data, position, end - position);
            }

            private ReadOnlySpan<byte> Take(int length)
            {
                if (length > Remaining)
                {
                    throw new InvalidDataException("Truncated Remus split-data catalog.");
                }

                var result = data.AsSpan(position, length);
                position += length;
                return result;
            }
        }
    }
}
