namespace Hst.Amiga.VersionStrings
{
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
        public static string Read(byte[] data) => ReadVersionString(data) ?? ReadRomTagIdString(data);

        /// <summary>
        /// Read "$VER:" version string from data.
        /// </summary>
        /// <returns>Version string or null, if not present</returns>
        public static string ReadVersionString(byte[] data)
        {
            var versionIndex = data.IndexOf(AmigaTextHelper.GetBytes("$VER"));

            return versionIndex == -1 ? null : data.ReadStringWithNullTermination(versionIndex);
        }

        /// <summary>
        /// Read id string from first resident structure (romtag) in hunk file data.
        /// </summary>
        /// <returns>Id string or null, if data is not a hunk file or has no resident structure with id string</returns>
        public static string ReadRomTagIdString(byte[] data) =>
            ReadRomTagIdString(new MemoryStream(data, false)).GetAwaiter().GetResult();

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

        /// <summary>
        /// Read id string from first resident structure (romtag) in hunk file stream.
        /// </summary>
        /// <returns>Id string or null, if stream is not a hunk file or has no resident structure with id string</returns>
        public static async Task<string> ReadRomTagIdString(Stream stream) =>
            (await RomTagReader.Read(stream))?.IdString;

        public static AmigaVersion Parse(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return null;
            }

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