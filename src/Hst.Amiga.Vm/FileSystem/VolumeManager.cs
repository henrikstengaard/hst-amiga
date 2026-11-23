namespace Hst.Amiga.Vm.FileSystem;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Maps Amiga volumes to volume file systems, e.g. host directories or Amiga file systems in adf and disk image
/// files, and resolves Amiga paths with volumes, assigns, parent references and case insensitive names.
/// </summary>
public class VolumeManager
{
    private const int MaxAssignDepth = 16;

    private readonly Dictionary<string, IVolumeFileSystem> volumes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> assigns = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AssignType> assignTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> volumeNames = new(StringComparer.OrdinalIgnoreCase);

    public VolumeManager(HostMetadata metadata = null)
    {
        Metadata = metadata ?? new HostMetadata();
    }

    /// <summary>
    /// Metadata with Amiga names, protection bits and comments for files in host directories.
    /// </summary>
    public HostMetadata Metadata { get; }

    public IEnumerable<string> VolumeNames => volumeNames.Values;
    public IEnumerable<string> AssignNames => assigns.Keys;

    /// <summary>
    /// Add volume mapped to host directory.
    /// </summary>
    public void AddVolume(string name, string hostPath)
    {
        var fullPath = Path.GetFullPath(hostPath);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException(
                $"Volume {TrimColon(name)}: host directory '{fullPath}' doesn't exist");
        }

        AddVolume(name, new HostVolumeFileSystem(fullPath, Metadata));
        Metadata.AddRoot(fullPath);
    }

    /// <summary>
    /// Add volume mapped to volume file system, e.g. an Amiga file system in an adf or disk image file.
    /// </summary>
    public void AddVolume(string name, IVolumeFileSystem fileSystem)
    {
        name = TrimColon(name);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Volume name is empty", nameof(name));
        }

        volumes[name] = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        volumeNames[name] = name;
    }

    /// <summary>
    /// Add assign to Amiga path. Adding multiple paths to same assign creates a multi assign.
    /// </summary>
    public void AddAssign(string name, string amigaPath)
    {
        name = TrimColon(name);
        if (!assigns.TryGetValue(name, out var paths))
        {
            paths = new List<string>();
            assigns[name] = paths;
            assignTypes[name] = AssignType.Lock;
        }

        paths.Add(amigaPath);
    }

    /// <summary>
    /// Set assign to Amiga path replacing existing assign with same name.
    /// </summary>
    public void SetAssign(string name, string amigaPath, AssignType type)
    {
        name = TrimColon(name);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Assign name is empty", nameof(name));
        }

        assigns[name] = new List<string> { amigaPath };
        assignTypes[name] = type;
    }

    /// <summary>
    /// Remove path from multi assign, the assign is removed when its last path is removed.
    /// </summary>
    /// <returns>True, if a path of the assign resolves to the target and was removed.</returns>
    public bool RemoveAssignPath(string name, AmigaPath target)
    {
        name = TrimColon(name);
        if (target == null || !assigns.TryGetValue(name, out var paths))
        {
            return false;
        }

        var index = paths.FindIndex(x => target.IsSame(Resolve(x, null)));
        if (index < 0)
        {
            return false;
        }

        paths.RemoveAt(index);
        if (paths.Count == 0)
        {
            RemoveAssign(name);
        }

        return true;
    }

    public void RemoveAssign(string name)
    {
        assigns.Remove(TrimColon(name));
        assignTypes.Remove(TrimColon(name));
    }

    /// <summary>
    /// Get Amiga paths of assign in the order they are searched.
    /// </summary>
    /// <returns>Paths or empty list, if assign doesn't exist.</returns>
    public IReadOnlyList<string> GetAssignPaths(string name) =>
        assigns.TryGetValue(TrimColon(name), out var paths) ? paths.ToList() : new List<string>();

    /// <summary>
    /// Get type of assign.
    /// </summary>
    public AssignType GetAssignType(string name) =>
        assignTypes.TryGetValue(TrimColon(name), out var type) ? type : AssignType.Lock;

    public bool HasVolume(string name) => volumes.ContainsKey(TrimColon(name));

    public bool HasAssign(string name) => assigns.ContainsKey(TrimColon(name));

    /// <summary>
    /// Get host directory of volume.
    /// </summary>
    /// <returns>Host directory or null, if volume doesn't exist or isn't mapped to a host directory.</returns>
    public string GetVolumeHostPath(string name) =>
        volumes.TryGetValue(TrimColon(name), out var fileSystem) ? fileSystem.HostRoot : null;

    /// <summary>
    /// Get file system of volume.
    /// </summary>
    /// <returns>File system or null, if volume doesn't exist.</returns>
    public IVolumeFileSystem GetFileSystem(string volume) =>
        volumes.TryGetValue(TrimColon(volume), out var fileSystem) ? fileSystem : null;

    /// <summary>
    /// Get file system of path.
    /// </summary>
    public IVolumeFileSystem GetFileSystem(AmigaPath path) =>
        GetFileSystem(path.Volume) ?? throw new InvalidOperationException($"Volume '{path.Volume}' not found");

    /// <summary>
    /// Get information about file or directory.
    /// </summary>
    /// <returns>Information or null, if path doesn't exist.</returns>
    public VolumeEntryInfo GetInfo(AmigaPath path) => path == null ? null : GetFileSystem(path).GetInfo(path);

    /// <summary>
    /// Does file or directory exist.
    /// </summary>
    public bool Exists(AmigaPath path) => GetInfo(path) != null;

    /// <summary>
    /// Is path an existing directory.
    /// </summary>
    public bool IsDirectory(AmigaPath path) => GetInfo(path) is { IsDirectory: true };

    /// <summary>
    /// Is path an existing file.
    /// </summary>
    public bool IsFile(AmigaPath path) => GetInfo(path) is { IsDirectory: false };

    /// <summary>
    /// Read all bytes of file.
    /// </summary>
    public byte[] ReadAllBytes(AmigaPath path)
    {
        using var stream = GetFileSystem(path).OpenFile(path, VolumeFileMode.OldFile);
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    /// <summary>
    /// Write all bytes to file, creating parent directories if they don't exist.
    /// </summary>
    public void WriteAllBytes(AmigaPath path, byte[] data)
    {
        var fileSystem = GetFileSystem(path);
        CreateParentDirectories(path);
        using var stream = fileSystem.OpenFile(path, VolumeFileMode.NewFile);
        stream.Write(data, 0, data.Length);
    }

    private void CreateParentDirectories(AmigaPath path)
    {
        var parent = GetParent(path);
        if (parent == null || IsDirectory(parent))
        {
            return;
        }

        CreateParentDirectories(parent);
        GetFileSystem(parent).CreateDirectory(parent);
    }

    private static string TrimColon(string name) => (name ?? string.Empty).TrimEnd(':');

    /// <summary>
    /// Get root path of volume.
    /// </summary>
    public AmigaPath GetRoot(string volume)
    {
        volume = TrimColon(volume);
        return volumes.TryGetValue(volume, out var fileSystem)
            ? fileSystem.GetRoot(volumeNames[volume])
            : null;
    }

    /// <summary>
    /// Resolve Amiga path relative to current directory.
    /// </summary>
    /// <param name="path">Amiga path, e.g. "SYS:c/dir", ":c", "/file" or "file".</param>
    /// <param name="currentDirectory">Current directory used for relative paths.</param>
    /// <returns>Resolved path or null, if path doesn't resolve to a volume.</returns>
    public AmigaPath Resolve(string path, AmigaPath currentDirectory) => Resolve(path, currentDirectory, 0);

    private AmigaPath Resolve(string path, AmigaPath currentDirectory, int depth)
    {
        if (depth > MaxAssignDepth || path == null)
        {
            return null;
        }

        var colon = path.IndexOf(':');
        if (colon < 0)
        {
            return currentDirectory == null ? null : Append(currentDirectory, path);
        }

        var device = path.Substring(0, colon);
        var rest = path.Substring(colon + 1);

        if (device.Length == 0)
        {
            return currentDirectory == null ? null : Append(GetRoot(currentDirectory.Volume), rest);
        }

        if (volumes.ContainsKey(device))
        {
            return Append(GetRoot(device), rest);
        }

        if (!assigns.TryGetValue(device, out var targets))
        {
            return null;
        }

        // multi assign resolves to first target where path exists
        AmigaPath first = null;
        foreach (var target in targets)
        {
            var targetPath = Resolve(target, currentDirectory, depth + 1);
            if (targetPath == null)
            {
                continue;
            }

            var resolved = Append(targetPath, rest);
            if (resolved == null)
            {
                continue;
            }

            if (Exists(resolved))
            {
                return resolved;
            }

            first ??= resolved;
        }

        return first;
    }

    /// <summary>
    /// Append relative Amiga path to directory. Empty components are parent directory references.
    /// </summary>
    private AmigaPath Append(AmigaPath directory, string relativePath)
    {
        if (directory == null)
        {
            return null;
        }

        var fileSystem = GetFileSystem(directory);
        var components = directory.Components.ToList();
        if (!string.IsNullOrEmpty(relativePath))
        {
            var parts = relativePath.Split('/');
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part.Length == 0)
                {
                    // trailing slash is ignored, each slash before is a parent reference
                    if (i == parts.Length - 1 && i > 0)
                    {
                        continue;
                    }

                    if (components.Count == 0)
                    {
                        return null;
                    }

                    components.RemoveAt(components.Count - 1);
                    continue;
                }

                if (!fileSystem.IsValidName(part))
                {
                    return null;
                }

                components.Add(part);
            }
        }

        var path = fileSystem.GetRoot(directory.Volume);
        foreach (var component in components)
        {
            path = fileSystem.GetChild(path, component);
        }

        return path;
    }

    /// <summary>
    /// Get entry in directory by Amiga name.
    /// </summary>
    public AmigaPath GetChild(AmigaPath directory, string amigaName) =>
        GetFileSystem(directory).GetChild(directory, amigaName);

    /// <summary>
    /// List Amiga names of entries in directory sorted by name.
    /// </summary>
    public string[] ListNames(AmigaPath directory) =>
        GetFileSystem(directory).ListNames(directory)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();

    /// <summary>
    /// Get parent directory of path.
    /// </summary>
    /// <returns>Parent directory or null, if path is root.</returns>
    public AmigaPath GetParent(AmigaPath path)
    {
        if (path == null || path.IsRoot)
        {
            return null;
        }

        var components = path.Components.Take(path.Components.Length - 1).ToArray();
        return new AmigaPath(path.Volume, components,
            path.HostPath == null ? null : Path.GetDirectoryName(path.HostPath));
    }
}
