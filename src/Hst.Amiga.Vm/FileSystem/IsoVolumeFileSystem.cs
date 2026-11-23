namespace Hst.Amiga.Vm.FileSystem;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiscUtils.Iso9660;
using Libraries.Dos;

/// <summary>
/// Volume file system mapped to an ISO 9660 file system in a CD image. Joliet and Rock Ridge names are used, when
/// present, version suffixes of names, e.g. ";1", and trailing dots of names without extension are hidden. Volume
/// is always write protected.
/// </summary>
public class IsoVolumeFileSystem : IImageFileSystem
{
    private static readonly char[] InvalidNameChars = { '/', ':', '\0' };
    private static readonly char[] PathSeparators = { '\\', '/' };

    private readonly CDReader reader;
    private readonly Dictionary<string, IList<IsoEntry>> directories = new(StringComparer.OrdinalIgnoreCase);
    private readonly long size;
    private readonly DateTime rootDate = DateTime.Now;

    /// <summary>
    /// Create volume file system for CD image.
    /// </summary>
    /// <param name="stream">Stream with ISO 9660 CD image, which must be seekable. Stream isn't disposed.</param>
    /// <exception cref="IOException">Stream doesn't contain a valid ISO 9660 file system.</exception>
    public IsoVolumeFileSystem(Stream stream)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        size = stream.Length;
        stream.Position = 0;
        try
        {
            reader = new CDReader(stream, true, true);
        }
        catch (Exception e) when (e is not IOException)
        {
            throw new IOException($"Invalid ISO 9660 file system: {e.Message}", e);
        }

        // volume labels are padded with spaces, which are read as U+2020 from Joliet labels padded with
        // single byte spaces
        var label = reader.VolumeLabel?.TrimEnd(' ', '\0', '†');
        Name = string.IsNullOrEmpty(label) ? "CD" : label;
    }

    /// <summary>
    /// Name of CD volume from volume label.
    /// </summary>
    public string Name { get; }

    public string HostRoot => null;

    public bool IsReadOnly => true;

    public bool IsValidName(string amigaName) =>
        !string.IsNullOrEmpty(amigaName) && amigaName.IndexOfAny(InvalidNameChars) < 0;

    public AmigaPath GetRoot(string volumeName) => new(volumeName, Array.Empty<string>(), null);

    public AmigaPath GetChild(AmigaPath directory, string amigaName)
    {
        var name = ListNames(directory)
            .FirstOrDefault(x => string.Equals(x, amigaName, StringComparison.OrdinalIgnoreCase));
        return new AmigaPath(directory.Volume, directory.Components.Concat(new[] { name ?? amigaName }).ToArray(),
            null);
    }

    public string[] ListNames(AmigaPath directory)
    {
        try
        {
            return ListEntries(ToIsoPath(directory)).Select(x => x.Name).ToArray();
        }
        catch (Exception e) when (IsFileSystemError(e))
        {
            return Array.Empty<string>();
        }
    }

    public VolumeEntryInfo GetInfo(AmigaPath path)
    {
        if (path.IsRoot)
        {
            return new VolumeEntryInfo { IsDirectory = true, Date = rootDate };
        }

        try
        {
            var isoPath = ToIsoPath(path);
            if (reader.DirectoryExists(isoPath))
            {
                return new VolumeEntryInfo { IsDirectory = true, Date = GetDate(isoPath) };
            }

            if (reader.FileExists(isoPath))
            {
                return new VolumeEntryInfo
                {
                    Size = reader.GetFileLength(isoPath),
                    Date = GetDate(isoPath)
                };
            }

            return null;
        }
        catch (Exception e) when (IsFileSystemError(e))
        {
            return null;
        }
    }

    public Stream OpenFile(AmigaPath path, VolumeFileMode mode)
    {
        var info = GetInfo(path);
        if (info is { IsDirectory: true })
        {
            throw new VolumeException(DosErrors.ObjectWrongType, $"'{path.FullPath}' is a directory");
        }

        if (info == null && mode == VolumeFileMode.OldFile)
        {
            throw new VolumeException(DosErrors.ObjectNotFound, $"'{path.FullPath}' not found");
        }

        if (info == null || mode == VolumeFileMode.NewFile)
        {
            throw WriteProtected();
        }

        try
        {
            return reader.OpenFile(ToIsoPath(path), FileMode.Open, FileAccess.Read);
        }
        catch (Exception e) when (IsFileSystemError(e))
        {
            throw new VolumeException(DosErrors.ObjectNotFound, e.Message, e);
        }
    }

    public void CreateDirectory(AmigaPath path) => throw WriteProtected();

    public void Delete(AmigaPath path) => throw WriteProtected();

    public void Rename(AmigaPath oldPath, AmigaPath newPath) => throw WriteProtected();

    public void SetProtection(AmigaPath path, uint protection)
    {
        if (!path.IsRoot)
        {
            throw WriteProtected();
        }
    }

    public void SetComment(AmigaPath path, string comment)
    {
        if (!path.IsRoot)
        {
            throw WriteProtected();
        }
    }

    public void SetDate(AmigaPath path, DateTime date)
    {
        if (!path.IsRoot)
        {
            throw WriteProtected();
        }
    }

    public (long Size, long Free) GetSpace(AmigaPath path) => (size, 0);

    /// <summary>
    /// CD volumes are write protected, so there are no changes to flush.
    /// </summary>
    public void Flush()
    {
    }

    public void Dispose() => reader.Dispose();

    private VolumeException WriteProtected() =>
        new(DosErrors.DiskWriteProtected, $"Volume '{Name}' is write protected");

    private DateTime GetDate(string isoPath)
    {
        var date = reader.GetLastWriteTime(isoPath);
        return date == DateTime.MinValue ? rootDate : date;
    }

    /// <summary>
    /// Get ISO path of Amiga path by finding each component in its directory ignoring case, e.g. "C/Hello" is
    /// "C\Hello." in ISO 9660 file system. Components are joined with directory separator of host, as used by
    /// DiscUtils. Components not found are kept as is.
    /// </summary>
    private string ToIsoPath(AmigaPath path)
    {
        var isoPath = string.Empty;
        foreach (var component in path.Components)
        {
            var entries = ListEntries(isoPath);
            var entry = entries.FirstOrDefault(x => string.Equals(x.Name, component, StringComparison.OrdinalIgnoreCase))
                        ?? entries.FirstOrDefault(x =>
                            string.Equals(x.IsoName, component, StringComparison.OrdinalIgnoreCase));
            var name = entry?.IsoName ?? component;
            isoPath = isoPath.Length == 0 ? name : $"{isoPath}{Path.DirectorySeparatorChar}{name}";
        }

        return isoPath;
    }

    /// <summary>
    /// List entries in directory by ISO path. Listings are cached, as volume is read only.
    /// </summary>
    private IList<IsoEntry> ListEntries(string isoPath)
    {
        if (directories.TryGetValue(isoPath, out var entries))
        {
            return entries;
        }

        entries = isoPath.Length == 0 || reader.DirectoryExists(isoPath)
            ? reader.GetFileSystemEntries(isoPath).Select(GetIsoName).Where(x => x.Length > 0)
                .Select(x => new IsoEntry(ToAmigaName(x), x)).ToList()
            : new List<IsoEntry>();
        directories[isoPath] = entries;
        return entries;
    }

    /// <summary>
    /// Get name of entry from ISO path, which has directory separator of host, e.g. "\C\Hello." on Windows and
    /// "/C/Hello." on Linux and macOS.
    /// </summary>
    private static string GetIsoName(string isoPath)
    {
        var name = isoPath.TrimEnd(PathSeparators);
        var separator = name.LastIndexOfAny(PathSeparators);
        return separator >= 0 ? name.Substring(separator + 1) : name;
    }

    /// <summary>
    /// Amiga name of ISO name without trailing dot of names without extension, e.g. "Hello." is "Hello".
    /// </summary>
    private static string ToAmigaName(string isoName)
    {
        var name = isoName.TrimEnd('.');
        return name.Length > 0 ? name : isoName;
    }

    private sealed class IsoEntry
    {
        public IsoEntry(string name, string isoName)
        {
            Name = name;
            IsoName = isoName;
        }

        public string Name { get; }

        public string IsoName { get; }
    }

    private static bool IsFileSystemError(Exception e) =>
        e is IOException or ArgumentException or NotSupportedException or InvalidOperationException;
}
