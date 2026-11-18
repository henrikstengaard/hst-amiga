using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hst.Amiga;

namespace Hst.Amiga.Roms
{
    /// <summary>
    /// Reads Kickstart ROM images and discovers their resident modules.
    /// </summary>
    public static class KickstartRomReader
    {
        private const int CloantoHeaderSize = 11;
        private const string CloantoHeader = "AMIROMTYPE1";
        private const int KickImageHeaderSize = 512;
        private const string KickImageHeader = "KICK";
        private const int ResidentSize = 26;
        private const uint ResidentMatchWord = 0x4AFC;
        private const uint MinimumRomBase = 0xD00000;
        private const uint MaximumRomBase = 0x1000000;

        /// <summary>
        /// Read a raw or Amiga Forever encrypted Kickstart ROM image.
        /// Encrypted images require the companion ROM key bytes.
        /// </summary>
        /// <param name="romBytes">ROM file bytes.</param>
        /// <param name="keyBytes">Amiga Forever rom.key bytes, if the image is encrypted.</param>
        /// <returns>Decoded ROM image and discovered resident modules.</returns>
        /// <exception cref="InvalidDataException">The image is invalid, unsupported, or has no credible resident modules.</exception>
        public static KickstartRom Read(byte[] romBytes, byte[] keyBytes = null)
        {
            var decodedBytes = Decode(romBytes, keyBytes, out var isEncrypted);
            var modules = FindModules(decodedBytes, out var baseAddress);
            return new KickstartRom(decodedBytes, isEncrypted, baseAddress, modules);
        }

        internal static byte[] Decode(byte[] romBytes, byte[] keyBytes, out bool isEncrypted)
        {
            if (romBytes == null)
            {
                throw new ArgumentNullException(nameof(romBytes));
            }

            isEncrypted = romBytes.Length >= CloantoHeaderSize &&
                              string.Equals(AmigaTextHelper.Iso88591.GetString(romBytes, 0, CloantoHeaderSize),
                                  CloantoHeader, StringComparison.Ordinal);
            var hasKickImageHeader = !isEncrypted && romBytes.Length > KickImageHeaderSize &&
                                     string.Equals(AmigaTextHelper.Iso88591.GetString(romBytes, 0, KickImageHeader.Length),
                                         KickImageHeader, StringComparison.Ordinal);

            byte[] decodedBytes;
            if (isEncrypted)
            {
                if (keyBytes == null || keyBytes.Length == 0)
                {
                    throw new InvalidDataException("Encrypted Amiga Forever ROM requires a non-empty rom.key file.");
                }

                decodedBytes = new byte[romBytes.Length - CloantoHeaderSize];
                for (var i = 0; i < decodedBytes.Length; i++)
                {
                    decodedBytes[i] = (byte)(romBytes[i + CloantoHeaderSize] ^ keyBytes[i % keyBytes.Length]);
                }
            }
            else
            {
                var headerSize = hasKickImageHeader ? KickImageHeaderSize : 0;
                decodedBytes = new byte[romBytes.Length - headerSize];
                Array.Copy(romBytes, headerSize, decodedBytes, 0, decodedBytes.Length);
            }

            return decodedBytes;
        }

        private static IReadOnlyList<KickstartRomModule> FindModules(byte[] bytes, out uint baseAddress)
        {
            var candidates = new Dictionary<uint, List<KickstartRomModule>>();

            for (var offset = 0; offset <= bytes.Length - ResidentSize; offset += 2)
            {
                if (ReadUInt16(bytes, offset) != ResidentMatchWord)
                {
                    continue;
                }

                var matchAddress = ReadUInt32(bytes, offset + 2);
                var endAddress = ReadUInt32(bytes, offset + 6);
                if (matchAddress < (uint)offset)
                {
                    continue;
                }

                var candidateBase = matchAddress - (uint)offset;
                if (candidateBase < MinimumRomBase || candidateBase >= MaximumRomBase ||
                    (candidateBase & 1) != 0 || endAddress <= matchAddress)
                {
                    continue;
                }

                var endOffset = (ulong)endAddress - candidateBase;
                if (endOffset > (ulong)bytes.Length || endOffset < (ulong)offset + ResidentSize)
                {
                    continue;
                }

                if (!candidates.TryGetValue(candidateBase, out var modules))
                {
                    modules = new List<KickstartRomModule>();
                    candidates[candidateBase] = modules;
                }

                var nameAddress = ReadUInt32(bytes, offset + 14);
                var idAddress = ReadUInt32(bytes, offset + 18);

                modules.Add(new KickstartRomModule
                {
                    Offset = offset,
                    Size = (int)endOffset - offset,
                    Version = bytes[offset + 11],
                    Revision = ReadRevision(ReadResidentString(bytes, candidateBase, idAddress)),
                    Name = ReadResidentString(bytes, candidateBase, nameAddress),
                    IdString = ReadResidentString(bytes, candidateBase, idAddress)?.TrimEnd('\r', '\n')
                });
            }

            var best = candidates
                .OrderByDescending(x => x.Value.Count)
                .ThenBy(x => x.Key)
                .FirstOrDefault();

            if (best.Value == null || best.Value.Count < 2)
            {
                throw new InvalidDataException("No Kickstart resident modules were found. The ROM format may be unsupported, or the encryption key may be incorrect.");
            }

            baseAddress = best.Key;
            return best.Value
                .GroupBy(x => x.Offset)
                .Select(x => x.First())
                .OrderBy(x => x.Offset)
                .ToList();
        }

        private static string ReadResidentString(byte[] bytes, uint baseAddress, uint address)
        {
            if (address < baseAddress)
            {
                return null;
            }

            var offset = (ulong)address - baseAddress;
            if (offset >= (ulong)bytes.Length)
            {
                return null;
            }

            var start = (int)offset;
            var end = start;
            while (end < bytes.Length && bytes[end] != 0)
            {
                end++;
            }

            return AmigaTextHelper.GetString(bytes, start, end - start);
        }

        private static byte ReadRevision(string idString)
        {
            if (string.IsNullOrWhiteSpace(idString))
            {
                return 0;
            }

            var versionStart = 0;
            while (versionStart < idString.Length && !char.IsDigit(idString[versionStart]))
            {
                versionStart++;
            }

            var dot = idString.IndexOf('.', versionStart);
            if (versionStart >= idString.Length || dot < 0)
            {
                return 0;
            }

            var revisionEnd = dot + 1;
            while (revisionEnd < idString.Length && char.IsDigit(idString[revisionEnd]))
            {
                revisionEnd++;
            }

            return byte.TryParse(idString.Substring(dot + 1, revisionEnd - dot - 1), out var revision)
                ? revision
                : (byte)0;
        }

        private static ushort ReadUInt16(byte[] bytes, int offset) =>
            (ushort)(bytes[offset] << 8 | bytes[offset + 1]);

        private static uint ReadUInt32(byte[] bytes, int offset) =>
            (uint)(bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3]);
    }
}
