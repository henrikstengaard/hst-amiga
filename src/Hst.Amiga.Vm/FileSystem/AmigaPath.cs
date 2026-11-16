namespace Hst.Amiga.Vm.FileSystem;

using System;
using System.Linq;

/// <summary>
/// Resolved Amiga path with volume, path components and host path.
/// </summary>
public class AmigaPath
{
    public AmigaPath(string volume, string[] components, string hostPath)
    {
        Volume = volume;
        Components = components;
        HostPath = hostPath;
    }

    /// <summary>
    /// Volume name without colon.
    /// </summary>
    public string Volume { get; }

    /// <summary>
    /// Path components from volume root.
    /// </summary>
    public string[] Components { get; }

    /// <summary>
    /// Path on host file system.
    /// </summary>
    public string HostPath { get; }

    public bool IsRoot => Components.Length == 0;

    /// <summary>
    /// Name of last component or volume name for root.
    /// </summary>
    public string Name => IsRoot ? Volume : Components[Components.Length - 1];

    /// <summary>
    /// Full Amiga path, e.g. "SYS:dir/file".
    /// </summary>
    public string FullPath => $"{Volume}:{string.Join("/", Components)}";

    public override string ToString() => FullPath;

    public bool IsSame(AmigaPath other) =>
        other != null && string.Equals(Volume, other.Volume, StringComparison.OrdinalIgnoreCase) &&
        Components.Length == other.Components.Length &&
        Components.Zip(other.Components, (a, b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
            .All(x => x);
}
