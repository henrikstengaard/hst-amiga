namespace Hst.Amiga.Vm.FileSystem;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Maps Amiga volumes to host directories and resolves Amiga paths with volumes, assigns, parent
/// references and case insensitive names to host paths.
/// </summary>
public class VolumeManager
{
    private const int MaxAssignDepth = 16;

    private readonly Dictionary<string, string> volumes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> assigns = new(StringComparer.OrdinalIgnoreCase);
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
        name = TrimColon(name);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Volume name is empty", nameof(name));
        }

        var fullPath = Path.GetFullPath(hostPath);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Volume {name}: host directory '{fullPath}' doesn't exist");
        }

        volumes[name] = fullPath;
        volumeNames[name] = name;
        Metadata.AddRoot(fullPath);
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
        }

        paths.Add(amigaPath);
    }

    public void RemoveAssign(string name) => assigns.Remove(TrimColon(name));

    public bool HasVolume(string name) => volumes.ContainsKey(TrimColon(name));

    public bool HasAssign(string name) => assigns.ContainsKey(TrimColon(name));

    public string GetVolumeHostPath(string name) => volumes.TryGetValue(TrimColon(name), out var path) ? path : null;

    private static string TrimColon(string name) => (name ?? string.Empty).TrimEnd(':');

    /// <summary>
    /// Get root path of volume.
    /// </summary>
    public AmigaPath GetRoot(string volume)
    {
        volume = TrimColon(volume);
        return volumes.TryGetValue(volume, out var hostPath)
            ? new AmigaPath(volumeNames[volume], Array.Empty<string>(), hostPath)
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

            if (File.Exists(resolved.HostPath) || Directory.Exists(resolved.HostPath))
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

                if (!Metadata.IsValidName(part))
                {
                    return null;
                }

                components.Add(part);
            }
        }

        var volumeRoot = volumes[directory.Volume];
        var hostPath = volumeRoot;
        var resolvedComponents = new string[components.Count];
        for (var i = 0; i < components.Count; i++)
        {
            var entry = FindEntry(hostPath, components[i]);
            resolvedComponents[i] = entry.AmigaName;
            hostPath = Path.Combine(hostPath, entry.HostName);
        }

        return new AmigaPath(directory.Volume, resolvedComponents, hostPath);
    }

    /// <summary>
    /// Find entry in host directory by Amiga name ignoring case. Host name for a new entry is created, if entry
    /// doesn't exist.
    /// </summary>
    private HostEntry FindEntry(string hostDirectory, string amigaName) =>
        Metadata.Find(hostDirectory, amigaName) ??
        new HostEntry(amigaName, Metadata.CreateHostName(hostDirectory, amigaName));

    /// <summary>
    /// Get entry in directory by Amiga name.
    /// </summary>
    public AmigaPath GetChild(AmigaPath directory, string amigaName)
    {
        var entry = FindEntry(directory.HostPath, amigaName);
        return new AmigaPath(directory.Volume, directory.Components.Concat(new[] { entry.AmigaName }).ToArray(),
            Path.Combine(directory.HostPath, entry.HostName));
    }

    /// <summary>
    /// List Amiga names of entries in directory sorted by name.
    /// </summary>
    public string[] ListNames(AmigaPath directory) =>
        Metadata.List(directory.HostPath).Select(x => x.AmigaName)
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
        return new AmigaPath(path.Volume, components, Path.GetDirectoryName(path.HostPath));
    }
}
