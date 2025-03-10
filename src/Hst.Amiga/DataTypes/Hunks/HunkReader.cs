﻿namespace Hst.Amiga.DataTypes.Hunks
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Hst.Core.Extensions;

    public static class HunkReader
    {
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

            IHunk hunk;
            do
            {
                hunk = await ReadHunk(stream);
                hunks.Add(hunk);
            } while (hunk != null && hunk.Identifier != HunkIdentifiers.End);

            return hunks;
        }

        public static async Task<IHunk> ReadHunk(Stream stream)
        {
            var identifier = await stream.ReadBigEndianUInt32();

            switch (identifier)
            {
                case HunkIdentifiers.Code:
                    return await ReadCode(stream);
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

            string residentLibraryName;
            do
            {
                residentLibraryName = await ReadString(stream);
                if (string.IsNullOrEmpty(residentLibraryName))
                {
                    break;
                }
                residentLibraryNames.Add(residentLibraryName);
            } while (!string.IsNullOrEmpty(residentLibraryName));
            
            var tableSize = await stream.ReadBigEndianUInt32();
            var firstHunk = await stream.ReadBigEndianUInt32();
            var lastHunk = await stream.ReadBigEndianUInt32();

            var hunkSizes = new List<uint>();
            for (var i = 0; i < lastHunk - firstHunk + 1; i++)
            {
                hunkSizes.Add(await stream.ReadBigEndianUInt32());
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
            var size = await stream.ReadBigEndianUInt32();
            var data = await stream.ReadBytes((int)size * 4);

            return new Code
            {
                Data = data
            };
        }
        
        public static async Task<ReLoc32> ReadReLoc32(Stream stream)
        {
            var hunkOffsets = new List<uint>();

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

                for (var i = 0; i < numOffsets; i++)
                {
                    var hunkOffset = await stream.ReadBigEndianUInt32();
                    hunkOffsets.Add(hunkOffset);
                }
            } while (stream.Position < stream.Length);

            return new ReLoc32
            {
                Offsets = hunkOffsets
            };
        }

        public static async Task<string> ReadString(Stream stream)
        {
            var numLongs = await stream.ReadBigEndianUInt32();
            if (numLongs < 1)
                return null;

            var stringBytes = await stream.ReadBytes((int)numLongs * 4);

            var endOffset = stringBytes.Length - 1;
            for (var i = 0; i < stringBytes.Length; i++)
            {
                if (stringBytes[i] == '\0')
                {
                    endOffset = i;
                }
            }
            
            return Encoding.ASCII.GetString(stringBytes, 0, endOffset);
        }
    }
}