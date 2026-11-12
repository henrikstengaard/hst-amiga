namespace Hst.Amiga.DataTypes.Hunks
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Reads resident structures (romtags) from hunks read by <see cref="HunkReader"/>.
    /// File system handlers and libraries contain a resident structure, where the id string
    /// holds the version, e.g. SmartFilesystem, which doesn't have a "$VER:" version string.
    /// </summary>
    public static class RomTagReader
    {
        // offsets in resident structure (exec/resident.h)
        private const int RtMatchTagOffset = 2;
        private const int RtFlagsOffset = 10;
        private const int RtVersionOffset = 11;
        private const int RtTypeOffset = 12;
        private const int RtPriOffset = 13;
        private const int RtNameOffset = 14;
        private const int RtIdStringOffset = 18;
        private const int RtSize = 26;

        /// <summary>
        /// Read hunk file from stream and return first resident structure found.
        /// </summary>
        /// <param name="stream">Stream with hunk file</param>
        /// <returns>Rom tag or null, if stream is not a hunk file, has unsupported hunks or no resident structure is found</returns>
        public static async Task<RomTag> Read(Stream stream)
        {
            IEnumerable<IHunk> hunks;
            try
            {
                hunks = await HunkReader.Read(stream);
            }
            catch (IOException)
            {
                return null;
            }

            return Read(hunks);
        }

        /// <summary>
        /// Read first resident structure found in hunks.
        /// </summary>
        /// <param name="hunks">Hunks read by hunk reader</param>
        /// <returns>Rom tag or null, if no resident structure is found</returns>
        public static RomTag Read(IEnumerable<IHunk> hunks) => ReadAll(hunks).FirstOrDefault();

        /// <summary>
        /// Read all resident structures found in hunks.
        /// </summary>
        /// <param name="hunks">Hunks read by hunk reader</param>
        /// <returns>Rom tags found</returns>
        public static IEnumerable<RomTag> ReadAll(IEnumerable<IHunk> hunks)
        {
            var hunkDatas = new List<byte[]>();
            var relocations = new List<Dictionary<uint, uint>>();

            // hunk numbers are assigned in order to code, data and bss hunks and
            // reloc32 hunks apply to the code, data or bss hunk preceding it
            foreach (var hunk in hunks)
            {
                switch (hunk)
                {
                    case Code code:
                        hunkDatas.Add(code.Data);
                        relocations.Add(new Dictionary<uint, uint>());
                        break;
                    case Data data:
                        hunkDatas.Add(data.Bytes);
                        relocations.Add(new Dictionary<uint, uint>());
                        break;
                    case Bss _:
                        hunkDatas.Add(null);
                        relocations.Add(new Dictionary<uint, uint>());
                        break;
                    case ReLoc32 reLoc32 when relocations.Count > 0 && reLoc32.Entries != null:
                        var hunkRelocations = relocations[relocations.Count - 1];
                        foreach (var entry in reLoc32.Entries)
                        {
                            foreach (var offset in entry.Offsets)
                            {
                                hunkRelocations[offset] = entry.HunkNumber;
                            }
                        }

                        break;
                }
            }

            for (var hunkNumber = 0; hunkNumber < hunkDatas.Count; hunkNumber++)
            {
                var data = hunkDatas[hunkNumber];
                if (data == null)
                {
                    continue;
                }

                for (var offset = 0; offset + RtSize <= data.Length; offset += 2)
                {
                    if (ReadUInt16(data, offset) != RomTag.MatchWord)
                    {
                        continue;
                    }

                    // rt_MatchTag must point to the resident structure itself
                    var matchTagOffset = (uint)(offset + RtMatchTagOffset);
                    if (ReadUInt32(data, (int)matchTagOffset) != (uint)offset ||
                        !relocations[hunkNumber].TryGetValue(matchTagOffset, out var matchTagHunkNumber) ||
                        matchTagHunkNumber != hunkNumber)
                    {
                        continue;
                    }

                    yield return new RomTag
                    {
                        HunkNumber = hunkNumber,
                        Offset = (uint)offset,
                        Flags = data[offset + RtFlagsOffset],
                        Version = data[offset + RtVersionOffset],
                        Type = data[offset + RtTypeOffset],
                        Priority = (sbyte)data[offset + RtPriOffset],
                        Name = ReadString(hunkDatas, relocations[hunkNumber], data, (uint)(offset + RtNameOffset)),
                        IdString = ReadString(hunkDatas, relocations[hunkNumber], data,
                            (uint)(offset + RtIdStringOffset))?.TrimEnd('\r', '\n')
                    };
                }
            }
        }

        private static string ReadString(IList<byte[]> hunkDatas, IDictionary<uint, uint> relocations,
            byte[] data, uint pointerOffset)
        {
            // pointers without relocation are null or absolute addresses, which can't be resolved
            if (!relocations.TryGetValue(pointerOffset, out var targetHunkNumber) ||
                targetHunkNumber >= hunkDatas.Count)
            {
                return null;
            }

            var targetData = hunkDatas[(int)targetHunkNumber];
            var stringOffset = ReadUInt32(data, (int)pointerOffset);
            if (targetData == null || stringOffset >= targetData.Length)
            {
                return null;
            }

            var end = (int)stringOffset;
            while (end < targetData.Length && targetData[end] != 0)
            {
                end++;
            }

            return AmigaTextHelper.GetString(targetData, (int)stringOffset, end - (int)stringOffset);
        }

        private static uint ReadUInt32(byte[] data, int offset) =>
            (uint)(data[offset] << 24 | data[offset + 1] << 16 | data[offset + 2] << 8 | data[offset + 3]);

        private static ushort ReadUInt16(byte[] data, int offset) =>
            (ushort)(data[offset] << 8 | data[offset + 1]);
    }
}
