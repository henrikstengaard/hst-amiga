namespace Hst.Amiga.Vm.FileSystem;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DataTypes.UaeFsDbs;
using DataTypes.UaeMetafiles;
using Hst.Amiga.FileSystems;

/// <summary>
/// Amiga names, protection bits, comments and dates for files in host directories stored as UAE metadata:
/// UAEFSDB files used by WinUAE or UAE metafiles used by FS-UAE. When writing is disabled, metadata files are
/// never created, changed or deleted and changes are kept in memory while the program runs.
/// </summary>
public class HostMetadata
{
    public const string UaeFsDbFileName = "_UAEFSDB.___";
    public const string MetafileExtension = ".uaem";

    // fib protection bits set, when write and delete is not allowed
    private const uint ProtectionWrite = 1 << 2;
    private const uint ProtectionDelete = 1 << 0;

    private static readonly char[] HostInvalidNameChars =
        Path.GetInvalidFileNameChars().Concat(new[] { '\\', '/', ':' }).Distinct().ToArray();

    private static readonly char[] AmigaInvalidNameChars = { '/', ':', '\0' };
    private static readonly Regex EncodedCharRegex = new("%[0-9a-fA-F]{2}", RegexOptions.Compiled);

    private static readonly (char Char, ProtectionBits Bit)[] ProtectionChars =
    {
        ('h', ProtectionBits.HeldResident), ('s', ProtectionBits.Script), ('p', ProtectionBits.Pure),
        ('a', ProtectionBits.Archive), ('r', ProtectionBits.Read), ('w', ProtectionBits.Write),
        ('e', ProtectionBits.Executable), ('d', ProtectionBits.Delete)
    };

    private enum Format
    {
        UaeFsDb,
        Metafile
    }

    private class UaeFsDb
    {
        public readonly List<UaeFsDbNode> Nodes = new();
        public DateTime LastWriteTime;
        public long Length = -1;

        /// <summary>
        /// Changed in memory only, because writing metadata is disabled.
        /// </summary>
        public bool Modified;

        public UaeFsDbNode Find(string hostName) =>
            Nodes.FirstOrDefault(x => string.Equals(x.NormalName, hostName, StringComparison.OrdinalIgnoreCase));
    }

    private readonly Dictionary<string, UaeFsDb> databases = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EntryMetadata> overlay = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Format> formats = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> roots = new();
    private readonly Action<string> log;

    public HostMetadata(UaeMetadataMode mode = UaeMetadataMode.None, bool write = false, Action<string> log = null)
    {
        Mode = mode;
        Write = write;
        this.log = log;
    }

    public UaeMetadataMode Mode { get; }

    /// <summary>
    /// Write changes to metadata files.
    /// </summary>
    public bool Write { get; }

    private bool Enabled => Mode != UaeMetadataMode.None;

    /// <summary>
    /// Add host directory of volume root. Directories without metadata use format of parent directories up to
    /// volume root.
    /// </summary>
    public void AddRoot(string hostDirectory) => roots.Add(Path.GetFullPath(hostDirectory));

    /// <summary>
    /// Is name a metadata file, which is hidden from directory listings.
    /// </summary>
    public bool IsMetadataFile(string hostName) =>
        Enabled && (string.Equals(hostName, UaeFsDbFileName, StringComparison.OrdinalIgnoreCase) ||
                    hostName.EndsWith(MetafileExtension, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Is name a valid Amiga name for a file or directory.
    /// </summary>
    public bool IsValidName(string amigaName)
    {
        if (string.IsNullOrEmpty(amigaName) || amigaName == "." || amigaName == "..")
        {
            return false;
        }

        return amigaName.IndexOfAny(Enabled ? AmigaInvalidNameChars : HostInvalidNameChars) < 0;
    }

    /// <summary>
    /// List entries in host directory with Amiga names. Metadata files are excluded.
    /// </summary>
    public IList<HostEntry> List(string hostDirectory)
    {
        var entries = new List<HostEntry>();
        IEnumerable<string> hostEntries;
        try
        {
            hostEntries = Directory.EnumerateFileSystemEntries(hostDirectory).Select(Path.GetFileName).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return entries;
        }

        if (!Enabled)
        {
            return hostEntries.Select(x => new HostEntry(x, x)).ToList();
        }

        var format = GetFormat(hostDirectory);
        var db = Mode == UaeMetadataMode.UaeMetafile ? null : GetDb(hostDirectory);
        foreach (var hostName in hostEntries)
        {
            if (IsMetadataFile(hostName))
            {
                continue;
            }

            var node = db?.Find(hostName);
            var amigaName = node != null && !string.IsNullOrEmpty(node.AmigaName)
                ? node.AmigaName
                : format == Format.Metafile
                    ? DecodeName(hostName)
                    : hostName;
            entries.Add(new HostEntry(amigaName, hostName));
        }

        return entries;
    }

    /// <summary>
    /// Find entry in host directory by Amiga name, preferring exact match over match ignoring case.
    /// </summary>
    /// <returns>Entry or null, if not found.</returns>
    public HostEntry Find(string hostDirectory, string amigaName)
    {
        if (!Directory.Exists(hostDirectory))
        {
            return null;
        }

        HostEntry match = null;
        foreach (var entry in List(hostDirectory))
        {
            if (string.Equals(entry.AmigaName, amigaName, StringComparison.Ordinal))
            {
                return entry;
            }

            if (match == null && string.Equals(entry.AmigaName, amigaName, StringComparison.OrdinalIgnoreCase))
            {
                match = entry;
            }
        }

        return match;
    }

    /// <summary>
    /// Create host name for new entry with Amiga name. Names with characters, which are invalid on host, are
    /// stored as UAEFSDB names prefixed with __uae___ or as UAE metafile names with encoded characters.
    /// </summary>
    public string CreateHostName(string hostDirectory, string amigaName)
    {
        if (!Enabled)
        {
            return amigaName;
        }

        if (GetFormat(hostDirectory) == Format.Metafile)
        {
            return UaeMetafileHelper.EncodeFilenameSpecialChars(amigaName);
        }

        return UaeFsDbNodeHelper.HasSpecialFilenameChars(amigaName)
            ? UaeFsDbNodeHelper.CreateUniqueNormalName(hostDirectory,
                UaeFsDbNodeHelper.MakeSafeFilename(amigaName))
            : amigaName;
    }

    /// <summary>
    /// Get metadata of file or directory.
    /// </summary>
    public EntryMetadata Get(string hostPath)
    {
        if (overlay.TryGetValue(hostPath, out var metadata))
        {
            return metadata.Clone();
        }

        var result = new EntryMetadata { Protection = GetDefaultProtection(hostPath) };
        if (!Enabled)
        {
            return result;
        }

        var (directory, hostName) = Split(hostPath);
        var node = Mode == UaeMetadataMode.UaeMetafile ? null : GetDb(directory).Find(hostName);
        if (node != null)
        {
            result.Protection = node.Mode;
            result.Comment = node.Comment ?? string.Empty;
            return result;
        }

        var metafile = Mode == UaeMetadataMode.UaeFsDb ? null : ReadMetafile(hostPath);
        if (metafile != null)
        {
            result.Protection = FromProtectionText(metafile.ProtectionBits, result.Protection);
            result.Comment = metafile.Comment ?? string.Empty;
            result.Date = metafile.Date;
        }

        return result;
    }

    /// <summary>
    /// Update metadata of file or directory.
    /// </summary>
    public void Set(string hostPath, string amigaName, Action<EntryMetadata> update)
    {
        var metadata = Get(hostPath);
        update(metadata);

        if (!Enabled || !Write)
        {
            overlay[hostPath] = metadata;
            return;
        }

        overlay.Remove(hostPath);
        var (directory, hostName) = Split(hostPath);
        var defaultProtection = GetDefaultProtection(hostPath);
        var hasAttributes = metadata.Protection != defaultProtection || metadata.Comment.Length > 0;

        if (GetFormat(directory) == Format.UaeFsDb)
        {
            var db = GetDb(directory);
            var node = db.Find(hostName);
            if (node == null)
            {
                if (!hasAttributes)
                {
                    return;
                }

                node = UaeFsDbNodeHelper.Create(amigaName, hostName);
                db.Nodes.Add(node);
            }

            node.Mode = metadata.Protection;
            node.Comment = metadata.Comment;
            SaveDb(directory, db);
            return;
        }

        var metafilePath = hostPath + MetafileExtension;
        if (!hasAttributes && metadata.Date == null && !File.Exists(metafilePath))
        {
            return;
        }

        WriteMetafile(metafilePath, metadata, hostPath);
    }

    /// <summary>
    /// Register new file or directory created with host name for Amiga name.
    /// </summary>
    public void Created(string hostPath, string amigaName)
    {
        overlay.Remove(hostPath);
        var (directory, hostName) = Split(hostPath);
        if (!Enabled || string.Equals(hostName, amigaName, StringComparison.Ordinal) ||
            GetFormat(directory) != Format.UaeFsDb)
        {
            return;
        }

        var db = GetDb(directory);
        var node = db.Find(hostName);
        if (node == null)
        {
            node = UaeFsDbNodeHelper.Create(amigaName, hostName);
            node.Mode = GetDefaultProtection(hostPath);
            db.Nodes.Add(node);
        }

        node.AmigaName = amigaName;
        SaveDb(directory, db);
    }

    /// <summary>
    /// Remove metadata of deleted file or directory.
    /// </summary>
    public void Deleted(string hostPath)
    {
        overlay.Remove(hostPath);
        if (!Enabled)
        {
            return;
        }

        var (directory, hostName) = Split(hostPath);
        if (Mode != UaeMetadataMode.UaeMetafile)
        {
            var db = GetDb(directory);
            var node = db.Find(hostName);
            if (node != null)
            {
                db.Nodes.Remove(node);
                SaveDb(directory, db);
            }
        }

        var metafilePath = hostPath + MetafileExtension;
        if (Write && Mode != UaeMetadataMode.UaeFsDb && File.Exists(metafilePath))
        {
            TryIo(() => File.Delete(metafilePath));
        }
    }

    /// <summary>
    /// Move metadata of renamed file or directory.
    /// </summary>
    /// <param name="oldHostPath">Host path before rename.</param>
    /// <param name="newHostPath">Host path after rename.</param>
    /// <param name="newAmigaName">Amiga name after rename.</param>
    /// <param name="metadata">Metadata read before rename.</param>
    public void Renamed(string oldHostPath, string newHostPath, string newAmigaName, EntryMetadata metadata)
    {
        // detect format of new directory before metadata of old path is deleted
        if (Enabled)
        {
            GetFormat(Split(newHostPath).Directory);
        }

        Deleted(oldHostPath);
        Created(newHostPath, newAmigaName);
        Set(newHostPath, newAmigaName, x =>
        {
            x.Protection = metadata.Protection;
            x.Comment = metadata.Comment;
            x.Date = metadata.Date;
        });
    }

    /// <summary>
    /// Is host directory empty ignoring metadata files.
    /// </summary>
    public bool IsEmptyDirectory(string hostDirectory) =>
        !Directory.EnumerateFileSystemEntries(hostDirectory).Select(Path.GetFileName).Any(x => !IsMetadataFile(x));

    /// <summary>
    /// Delete empty host directory including metadata files in it.
    /// </summary>
    public void DeleteDirectory(string hostDirectory)
    {
        foreach (var file in Directory.EnumerateFiles(hostDirectory).Where(x => IsMetadataFile(Path.GetFileName(x))))
        {
            File.Delete(file);
        }

        databases.Remove(hostDirectory);
        Directory.Delete(hostDirectory);
    }

    private static (string Directory, string HostName) Split(string hostPath) =>
        (Path.GetDirectoryName(hostPath) ?? string.Empty, Path.GetFileName(hostPath));

    private static uint GetDefaultProtection(string hostPath)
    {
        try
        {
            if (File.Exists(hostPath) &&
                (File.GetAttributes(hostPath) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                return ProtectionWrite | ProtectionDelete;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }

        return 0;
    }

    private Format GetFormat(string hostDirectory)
    {
        switch (Mode)
        {
            case UaeMetadataMode.UaeFsDb:
                return Format.UaeFsDb;
            case UaeMetadataMode.UaeMetafile:
                return Format.Metafile;
        }

        // format detected is kept, so it doesn't change when last metadata file in directory is deleted
        if (formats.TryGetValue(hostDirectory, out var format))
        {
            return format;
        }

        // directory without metadata uses format of parent directories within volume
        for (var directory = hostDirectory; directory != null; directory = Path.GetDirectoryName(directory))
        {
            var detected = DetectFormat(directory);
            if (detected.HasValue)
            {
                formats[hostDirectory] = detected.Value;
                return detected.Value;
            }

            if (roots.Any(x => string.Equals(x, directory, StringComparison.OrdinalIgnoreCase)) ||
                !roots.Any(x => IsWithin(directory, x)))
            {
                break;
            }
        }

        return Format.UaeFsDb;
    }

    private Format? DetectFormat(string hostDirectory)
    {
        if (File.Exists(Path.Combine(hostDirectory, UaeFsDbFileName)) ||
            (databases.TryGetValue(hostDirectory, out var db) && db.Modified))
        {
            return Format.UaeFsDb;
        }

        try
        {
            return Directory.Exists(hostDirectory) &&
                   Directory.EnumerateFiles(hostDirectory, "*" + MetafileExtension).Any()
                ? Format.Metafile
                : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsWithin(string path, string root) =>
        path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    private UaeFsDb GetDb(string hostDirectory)
    {
        databases.TryGetValue(hostDirectory, out var db);
        if (db is { Modified: true })
        {
            return db;
        }

        var fileInfo = new FileInfo(Path.Combine(hostDirectory, UaeFsDbFileName));
        if (!fileInfo.Exists)
        {
            if (db == null || db.Length != -1)
            {
                db = new UaeFsDb();
                databases[hostDirectory] = db;
            }

            return db;
        }

        if (db != null && db.Length == fileInfo.Length && db.LastWriteTime == fileInfo.LastWriteTimeUtc)
        {
            return db;
        }

        db = new UaeFsDb { Length = fileInfo.Length, LastWriteTime = fileInfo.LastWriteTimeUtc };
        databases[hostDirectory] = db;
        try
        {
            // read into memory stream, so reading completes synchronously
            using var stream = new MemoryStream(File.ReadAllBytes(fileInfo.FullName));
            var nodes = UaeFsDbReader.ReadFromStream(stream).GetAwaiter().GetResult();
            db.Nodes.AddRange(nodes.Where(x => x.Valid != 0 && !string.IsNullOrEmpty(x.NormalName)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException
                                      or InvalidOperationException)
        {
            log?.Invoke($"Warning: Unable to read '{fileInfo.FullName}': {e.Message}");
        }

        return db;
    }

    private void SaveDb(string hostDirectory, UaeFsDb db)
    {
        if (!Write)
        {
            db.Modified = true;
            return;
        }

        var path = Path.Combine(hostDirectory, UaeFsDbFileName);
        TryIo(() =>
        {
            var data = db.Nodes.SelectMany(x => UaeFsDbWriter.Build(x)).ToArray();
            File.WriteAllBytes(path, data);
            var fileInfo = new FileInfo(path);
            db.Length = fileInfo.Length;
            db.LastWriteTime = fileInfo.LastWriteTimeUtc;
        });
    }

    private UaeMetafile ReadMetafile(string hostPath)
    {
        var metafilePath = hostPath + MetafileExtension;
        if (!File.Exists(metafilePath))
        {
            return null;
        }

        try
        {
            return UaeMetafileReader.Read(File.ReadAllBytes(metafilePath));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            log?.Invoke($"Warning: Unable to read '{metafilePath}': {e.Message}");
            return null;
        }
    }

    private void WriteMetafile(string metafilePath, EntryMetadata metadata, string hostPath)
    {
        TryIo(() =>
        {
            var date = metadata.Date ?? (Directory.Exists(hostPath)
                ? Directory.GetLastWriteTime(hostPath)
                : File.GetLastWriteTime(hostPath));
            File.WriteAllBytes(metafilePath, UaeMetafileWriter.Build(new UaeMetafile
            {
                ProtectionBits = ToProtectionText(metadata.Protection),
                Date = date,
                Comment = metadata.Comment
            }));
        });
    }

    private void TryIo(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            log?.Invoke($"Warning: Unable to write UAE metadata: {e.Message}");
        }
    }

    private static string DecodeName(string hostName)
    {
        if (!EncodedCharRegex.IsMatch(hostName))
        {
            return hostName;
        }

        return EncodedCharRegex.Replace(hostName, m => ((char)Convert.ToInt32(m.Value.Substring(1), 16)).ToString());
    }

    /// <summary>
    /// Convert fib protection bits to text, e.g. "----rwed".
    /// </summary>
    public static string ToProtectionText(uint protection)
    {
        var bits = ProtectionBitsConverter.ToProtectionBits((int)(protection & 0xFF));
        return new string(ProtectionChars.Select(x => (bits & x.Bit) != 0 ? x.Char : '-').ToArray());
    }

    /// <summary>
    /// Convert text, e.g. "----rwed" to fib protection bits keeping bits above lowest 8 bits.
    /// </summary>
    public static uint FromProtectionText(string text, uint protection = 0)
    {
        try
        {
            var value = ProtectionBitsConverter.ToProtectionValue(ProtectionBitsConverter.ParseProtectionBits(text));
            return (protection & ~0xFFu) | value;
        }
        catch (ArgumentException)
        {
            return protection;
        }
    }
}
