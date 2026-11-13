namespace Hst.Amiga.FileSystems.Pfs3.Doctor.Salvage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using DataTypes.UaeFsDbs;
    using DataTypes.UaeMetafiles;

    /// <summary>
    /// Salvage target re-creating directories and files in a directory on the host file system. Amiga names,
    /// protection bits and comments are stored as UAE metadata, either in UAEFSDB files or UAE metafiles, and
    /// dates are set as last write time of directories and files. Directories with UAE metadata can be added as
    /// a hard drive in WinUAE, FS-UAE or Amiberry.
    /// </summary>
    public class DirectorySalvageTarget : ISalvageTarget
    {
        private const string UaeFsDbFileName = "_UAEFSDB.___";
        private const string UaeMetafileExtension = ".uaem";

        private static readonly HashSet<string> ReservedWindowsNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
            };

        private readonly string path;
        private readonly UaeMetadata uaeMetadata;

        /// <summary>
        /// Host paths of created directories by Amiga path.
        /// </summary>
        private readonly Dictionary<string, string> directoryPaths;

        /// <summary>
        /// Create directory salvage target.
        /// </summary>
        /// <param name="path">Path to directory to re-create salvaged directories and files in. Directory is
        /// created, if it doesn't exist.</param>
        /// <param name="uaeMetadata">Type of UAE metadata to write.</param>
        public DirectorySalvageTarget(string path, UaeMetadata uaeMetadata)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path is required", nameof(path));

            this.path = Path.GetFullPath(path);
            this.uaeMetadata = uaeMetadata;
            directoryPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { string.Empty, this.path }
            };

            System.IO.Directory.CreateDirectory(this.path);
        }

        public async Task CreateDirectory(IReadOnlyList<string> parentPath, SalvageEntry entry)
        {
            var parentDirectoryPath = GetDirectoryPath(parentPath);
            var normalName = GetNormalName(parentDirectoryPath, entry.Name);
            var directoryPath = Path.Combine(parentDirectoryPath, normalName);

            System.IO.Directory.CreateDirectory(directoryPath);
            directoryPaths[GetKey(parentPath, entry.Name)] = directoryPath;

            await WriteMetadata(parentDirectoryPath, normalName, entry);
        }

        public Task SetDirectoryMetadata(IReadOnlyList<string> parentPath, SalvageEntry entry)
        {
            if (directoryPaths.TryGetValue(GetKey(parentPath, entry.Name), out var directoryPath))
            {
                System.IO.Directory.SetLastWriteTime(directoryPath, entry.Date);
            }

            return Task.CompletedTask;
        }

        public async Task WriteFile(IReadOnlyList<string> parentPath, SalvageEntry entry, Stream data)
        {
            var parentDirectoryPath = GetDirectoryPath(parentPath);
            var normalName = GetNormalName(parentDirectoryPath, entry.Name);
            var filePath = Path.Combine(parentDirectoryPath, normalName);

            using (var stream = System.IO.File.Create(filePath))
            {
                await data.CopyToAsync(stream);
            }

            await WriteMetadata(parentDirectoryPath, normalName, entry);

            System.IO.File.SetLastWriteTime(filePath, entry.Date);
        }

        public Task Flush() => Task.CompletedTask;

        private static string GetKey(IEnumerable<string> parentPath, string name) =>
            string.Join("/", parentPath.Concat(new[] { name }));

        private string GetDirectoryPath(IReadOnlyList<string> parentPath)
        {
            var key = string.Join("/", parentPath);
            if (!directoryPaths.TryGetValue(key, out var directoryPath))
            {
                throw new DirectoryNotFoundException($"Directory '{key}' is not created");
            }

            return directoryPath;
        }

        private static bool IsReservedWindowsName(string name)
        {
            var dotIndex = name.IndexOf('.');
            return ReservedWindowsNames.Contains(dotIndex >= 0 ? name.Substring(0, dotIndex) : name);
        }

        /// <summary>
        /// Get normal name on host file system for Amiga name.
        /// </summary>
        private string GetNormalName(string directoryPath, string amigaName)
        {
            switch (uaeMetadata)
            {
                case UaeMetadata.UaeFsDb:
                    return UaeFsDbNodeHelper.HasSpecialFilenameChars(amigaName) || IsReservedWindowsName(amigaName)
                        ? UaeFsDbNodeHelper.CreateUniqueNormalName(directoryPath,
                            UaeFsDbNodeHelper.MakeSafeFilename(amigaName))
                        : amigaName;
                default:
                    var normalName = UaeMetafileHelper.EncodeFilenameSpecialChars(amigaName);
                    if (IsReservedWindowsName(normalName))
                    {
                        normalName = string.Concat(UaeMetafileHelper.EncodeFilename(normalName.Substring(0, 1)),
                            normalName.Substring(1));
                    }

                    return normalName;
            }
        }

        private async Task WriteMetadata(string directoryPath, string normalName, SalvageEntry entry)
        {
            switch (uaeMetadata)
            {
                case UaeMetadata.UaeFsDb:
                    /* node is only needed, if name, protection bits or comment can't be represented by host */
                    var comment = entry.Comment ?? string.Empty;
                    if (normalName == entry.Name && entry.Protection == 0 && comment.Length == 0)
                    {
                        return;
                    }

                    var node = UaeFsDbNodeHelper.Create(entry.Name, normalName);
                    node.Mode = entry.Protection;
                    node.Comment = comment;
                    await UaeFsDbWriter.WriteToFile(Path.Combine(directoryPath, UaeFsDbFileName), node);
                    break;
                default:
                    var uaeMetafile = new UaeMetafile
                    {
                        ProtectionBits = EntryFormatter.FormatProtectionBits(entry.ProtectionBits)
                            .ToLowerInvariant(),
                        Date = entry.Date,
                        Comment = entry.Comment ?? string.Empty
                    };
                    var metafilePath = Path.Combine(directoryPath, string.Concat(normalName, UaeMetafileExtension));
                    var bytes = UaeMetafileWriter.Build(uaeMetafile);
                    using (var stream = System.IO.File.Create(metafilePath))
                    {
                        await stream.WriteAsync(bytes, 0, bytes.Length);
                    }

                    break;
            }
        }
    }
}
