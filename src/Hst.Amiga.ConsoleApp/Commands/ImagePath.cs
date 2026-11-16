namespace Hst.Amiga.ConsoleApp.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Path to file in adf or disk image file, e.g. "workbench.adf\c\list" or "disk.vhd\rdb\1\c\list".
/// </summary>
public class ImagePath
{
    private static readonly char[] Separators = { '\\', '/' };

    public ImagePath(string imageFile, IReadOnlyList<string> components)
    {
        ImageFile = imageFile;
        Components = components;
    }

    /// <summary>
    /// Host path to adf or disk image file.
    /// </summary>
    public string ImageFile { get; }

    /// <summary>
    /// Path components after image file, e.g. "c", "list" or "rdb", "1", "c", "list".
    /// </summary>
    public IReadOnlyList<string> Components { get; }

    /// <summary>
    /// Parse path to file in image file by finding the first part of path, which is an existing host file.
    /// </summary>
    /// <param name="path">Path, e.g. "workbench.adf\c\list".</param>
    /// <param name="imagePath">Parsed image path.</param>
    /// <returns>True, if path is a path to a file in an image file.</returns>
    public static bool TryParse(string path, out ImagePath imagePath)
    {
        imagePath = null;
        if (string.IsNullOrWhiteSpace(path) || File.Exists(path))
        {
            return false;
        }

        var parts = path.Split(Separators);
        var prefix = string.Empty;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            prefix = i == 0
                ? parts[0].Length == 0 ? Path.DirectorySeparatorChar.ToString() : parts[0]
                : prefix.EndsWith(Path.DirectorySeparatorChar.ToString())
                    ? prefix + parts[i]
                    : prefix + Path.DirectorySeparatorChar + parts[i];

            // drive root, e.g. "c:" is a directory
            if (prefix.EndsWith(":"))
            {
                prefix += Path.DirectorySeparatorChar;
                continue;
            }

            if (!File.Exists(prefix))
            {
                continue;
            }

            var components = parts.Skip(i + 1).Where(x => x.Length > 0).ToList();
            if (components.Count == 0)
            {
                return false;
            }

            imagePath = new ImagePath(Path.GetFullPath(prefix), components);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Amiga path of components from index joined with slash, e.g. "c/list".
    /// </summary>
    public string GetAmigaPath(int index) => string.Join("/", Components.Skip(index));

    /// <summary>
    /// Is path to partition in rigid disk block, e.g. "rdb\1\c\list" or "rdb\dh0\c\list".
    /// </summary>
    public bool IsRigidDiskBlockPath =>
        Components.Count >= 2 && string.Equals(Components[0], "rdb", StringComparison.OrdinalIgnoreCase);
}
