namespace Hst.Amiga.VersionStrings
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Core.Extensions;
    using DataTypes.Hunks;
    using Extensions;

    // https://aminet.net/package/util/sys/Ver
    public static class VersionStringReader
    {
        // "$VER: <name> <version>.<revision> (dd.mm.yy)"
        private static readonly Regex
            VersionRegex = new Regex("\\$VER:\\s+(.+)\\s+(\\d+)\\.(\\d+)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // resident (romtag) id string without "$VER:", "<name> <version>.<revision> (dd.mm.yy)"
        private static readonly Regex
            IdStringRegex = new Regex("^\\s*(.+)\\s+(\\d+)\\.(\\d+)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex
            DateRegex = new Regex("\\((\\d+)\\.(\\d+)\\.(\\d+)\\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Read version string from data. If no "$VER:" version string is present, id string from
        /// resident structure (romtag) in hunk file is read instead.
        /// </summary>
        public static string Read(byte[] data)
        {
            var versionIndex = data.IndexOf(AmigaTextHelper.GetBytes("$VER"));

            return versionIndex == -1
                ? ReadRomTagIdString(new MemoryStream(data, false)).GetAwaiter().GetResult()
                : data.ReadStringWithNullTermination(versionIndex);
        }

        /// <summary>
        /// Read version string from stream. If no "$VER:" version string is present, id string from
        /// resident structure (romtag) in hunk file is read instead.
        /// </summary>
        public static async Task<string> Read(Stream stream)
        {
            var startPosition = stream.Position;
            var versionIndex = await stream.Find(AmigaTextHelper.GetBytes("$VER"));

            if (versionIndex == -1)
            {
                return stream.CanSeek && stream.Seek(startPosition, SeekOrigin.Begin) == startPosition
                    ? await ReadRomTagIdString(stream)
                    : null;
            }

            if (stream.Seek(versionIndex, SeekOrigin.Begin) != versionIndex)
            {
                return null;
            }

            return await stream.ReadNullTerminatedString();
        }

        private static async Task<string> ReadRomTagIdString(Stream stream)
        {
            IEnumerable<IHunk> hunks;
            try
            {
                hunks = await HunkReader.Read(stream);
            }
            catch (IOException)
            {
                // not a hunk file or unsupported hunk
                return null;
            }

            return RomTagReader.Read(hunks)?.IdString;
        }

        public static AmigaVersion Parse(string version)
        {
            var versionMatch = VersionRegex.Match(version);

            if (!versionMatch.Success)
            {
                versionMatch = IdStringRegex.Match(version);
            }

            if (!versionMatch.Success)
            {
                return null;
            }

            int.TryParse(versionMatch.Groups[2].Value, out var versionValue);
            int.TryParse(versionMatch.Groups[3].Value, out var revisionValue);

            var fileVersion = new AmigaVersion
            {
                Name = versionMatch.Groups[1].Value,
                Version = versionValue,
                Revision = revisionValue
            };

            var dateMatch = DateRegex.Match(version);

            if (!dateMatch.Success)
            {
                return fileVersion;
            }

            int.TryParse(dateMatch.Groups[1].Value, out var dateDayValue);
            int.TryParse(dateMatch.Groups[2].Value, out var dateMonthValue);
            int.TryParse(dateMatch.Groups[3].Value, out var dateYearValue);

            fileVersion.DateDay = dateDayValue;
            fileVersion.DateMonth = dateMonthValue;
            fileVersion.DateYear = dateYearValue;

            return fileVersion;
        }
    }
}