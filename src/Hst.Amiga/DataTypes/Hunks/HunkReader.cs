namespace Hst.Amiga.DataTypes.Hunks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Hst.Core.Extensions;

    public static class HunkReader
    {
        // upper 2 bits of hunk identifiers and sizes are memory flags
        private const uint MemoryFlagsMask = 0xC0000000;

        // http://amiga-dev.wikidot.com/file-format:hunk#toc15
        // https://www.markwrobel.dk/post/amiga-machine-code-detour-reverse-engineering/
        // http://amiga.rules.no/abfs/abfs.pdf
        public static async Task<IEnumerable<IHunk>> Read(Stream stream)
        {
            var header = await ReadHeader(stream);

            var hunks = new List<IHunk>
            {
                header
            };

            // each code, data or bss hunk is terminated by an end hunk
            var hunkCount = header.LastHunkSlot - header.FirstHunkSlot + 1;
            var endCount = 0;
            do
            {
                var hunk = await ReadHunk(stream);
                hunks.Add(hunk);

                if (hunk.Identifier == HunkIdentifiers.End)
                {
                    endCount++;
                }
            } while (endCount < hunkCount && (!stream.CanSeek || stream.Position < stream.Length));

            return hunks;
        }

        public static async Task<IHunk> ReadHunk(Stream stream)
        {
            var identifier = await stream.ReadBigEndianUInt32();

            switch (identifier & ~MemoryFlagsMask)
            {
                case HunkIdentifiers.Code:
                    return await ReadCode(stream);
                case HunkIdentifiers.Data:
                    return await ReadData(stream);
                case HunkIdentifiers.Bss:
                    return await ReadBss(stream);
                case HunkIdentifiers.ReLoc32:
                    return await ReadReLoc32(stream);
                case HunkIdentifiers.End:
                    return new End();
                default:
                    throw new IOException($"Unsupported hunk identifier '{identifier.FormatHex()}'");
            }
        }

        public static async Task<Header> ReadHeader(Stream stream)
        {
            var identifier = await stream.ReadBigEndianUInt32();
            if (identifier != HunkIdentifiers.Header)
            {
                throw new IOException("Invalid hunk header identifier");
            }

            var residentLibraryNames = new List<string>();

            // list of resident library names is terminated by zero number of long words
            string residentLibraryName;
            while ((residentLibraryName = await ReadString(stream)) != null)
            {
                residentLibraryNames.Add(residentLibraryName);
            }
            
            var tableSize = await stream.ReadBigEndianUInt32();
            var firstHunk = await stream.ReadBigEndianUInt32();
            var lastHunk = await stream.ReadBigEndianUInt32();

            if (lastHunk < firstHunk)
            {
                throw new IOException("Invalid hunk header first and last hunk slots");
            }

            var hunkCount = (long)lastHunk - firstHunk + 1;
            EnsureAvailable(stream, hunkCount * 4);

            var hunkSizes = new List<uint>();
            for (var i = 0; i < hunkCount; i++)
            {
                var hunkSize = await stream.ReadBigEndianUInt32();
                hunkSizes.Add(hunkSize);

                // both memory flags set, extended memory attributes follow
                if ((hunkSize & MemoryFlagsMask) == MemoryFlagsMask)
                {
                    await stream.ReadBigEndianUInt32();
                }
            }

            return new Header
            {
                ResidentLibraryNames = residentLibraryNames,
                TableSize = tableSize,
                FirstHunkSlot = firstHunk,
                LastHunkSlot = lastHunk,
                HunkSizes = hunkSizes
            };
        }

        public static async Task<Code> ReadCode(Stream stream)
        {
            return new Code
            {
                Data = await ReadHunkData(stream)
            };
        }

        public static async Task<Data> ReadData(Stream stream)
        {
            return new Data
            {
                Bytes = await ReadHunkData(stream)
            };
        }

        public static async Task<Bss> ReadBss(Stream stream)
        {
            var size = await stream.ReadBigEndianUInt32() & ~MemoryFlagsMask;

            return new Bss
            {
                Size = size * 4
            };
        }

        private static async Task<byte[]> ReadHunkData(Stream stream)
        {
            var size = (long)(await stream.ReadBigEndianUInt32() & ~MemoryFlagsMask) * 4;
            EnsureAvailable(stream, size);

            return await stream.ReadBytes((int)size);
        }

        private static void EnsureAvailable(Stream stream, long size)
        {
            if (size > int.MaxValue || (stream.CanSeek && stream.Position + size > stream.Length))
            {
                throw new IOException("Hunk size exceeds end of stream");
            }
        }
        
        public static async Task<ReLoc32> ReadReLoc32(Stream stream)
        {
            var hunkOffsets = new List<uint>();
            var entries = new List<ReLoc32Entry>();

            do
            {
                // // the number of offsets for a given hunk. If this value is zero, then it indicates the immediate end of this block.
                var numOffsets = await stream.ReadBigEndianUInt32();
                if (numOffsets == 0)
                {
                    break;
                }

                // // The number of the hunk the offsets are to point into.
                var hunkNumber = await stream.ReadBigEndianUInt32();
                hunkOffsets.Add(hunkNumber);

                EnsureAvailable(stream, (long)numOffsets * 4);

                var entryOffsets = new List<uint>();
                for (var i = 0; i < numOffsets; i++)
                {
                    var hunkOffset = await stream.ReadBigEndianUInt32();
                    hunkOffsets.Add(hunkOffset);
                    entryOffsets.Add(hunkOffset);
                }

                entries.Add(new ReLoc32Entry
                {
                    HunkNumber = hunkNumber,
                    Offsets = entryOffsets
                });
            } while (stream.Position < stream.Length);

            return new ReLoc32
            {
                Offsets = hunkOffsets,
                Entries = entries
            };
        }

        /// <summary>
        /// Read string stored as number of long words followed by string bytes padded with zeros to long word size.
        /// </summary>
        /// <returns>String up to first zero byte or null, if number of long words is zero</returns>
        public static async Task<string> ReadString(Stream stream)
        {
            var numLongs = await stream.ReadBigEndianUInt32();
            if (numLongs < 1)
                return null;

            EnsureAvailable(stream, (long)numLongs * 4);

            var stringBytes = await stream.ReadBytes((int)numLongs * 4);

            // string ends at first zero byte or fills all long words without zero termination
            var length = Array.IndexOf(stringBytes, (byte)0);
            if (length < 0)
            {
                length = stringBytes.Length;
            }

            return Encoding.ASCII.GetString(stringBytes, 0, length);
        }
    }
}