namespace Hst.Amiga.Vm.FileSystem;

using System;
using System.IO;
using System.Linq;

/// <summary>
/// Volume file system mapped to a host directory with Amiga names, protection bits and comments stored in
/// memory or as UAE metadata.
/// </summary>
public class HostVolumeFileSystem : IVolumeFileSystem
{
    private const long DefaultSize = 0x100000L * 512;
    private const long DefaultFree = 0x80000L * 512;

    public HostVolumeFileSystem(string hostRoot, HostMetadata metadata)
    {
        HostRoot = Path.GetFullPath(hostRoot);
        Metadata = metadata ?? new HostMetadata();
    }

    public string HostRoot { get; }

    /// <summary>
    /// Metadata with Amiga names, protection bits and comments for files in host directories.
    /// </summary>
    public HostMetadata Metadata { get; }

    public bool IsReadOnly => false;

    public bool IsValidName(string amigaName) => Metadata.IsValidName(amigaName);

    public AmigaPath GetRoot(string volume) => new(volume, Array.Empty<string>(), HostRoot);

    public AmigaPath GetChild(AmigaPath directory, string amigaName)
    {
        var entry = Metadata.Find(directory.HostPath, amigaName) ??
                    new HostEntry(amigaName, Metadata.CreateHostName(directory.HostPath, amigaName));
        return new AmigaPath(directory.Volume, directory.Components.Concat(new[] { entry.AmigaName }).ToArray(),
            Path.Combine(directory.HostPath, entry.HostName));
    }

    public string[] ListNames(AmigaPath directory) =>
        Metadata.List(directory.HostPath).Select(x => x.AmigaName).ToArray();

    public VolumeEntryInfo GetInfo(AmigaPath path)
    {
        var isDirectory = path.IsRoot || Directory.Exists(path.HostPath);
        if (!isDirectory && !File.Exists(path.HostPath))
        {
            return null;
        }

        var info = new VolumeEntryInfo { IsDirectory = isDirectory };
        try
        {
            if (isDirectory)
            {
                info.Date = Directory.GetLastWriteTime(path.HostPath);
            }
            else
            {
                var fileInfo = new FileInfo(path.HostPath);
                info.Size = fileInfo.Length;
                info.Date = fileInfo.LastWriteTime;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            info.Date = DateTime.Now;
        }

        if (path.IsRoot)
        {
            return info;
        }

        var metadata = Metadata.Get(path.HostPath);
        info.Protection = metadata.Protection;
        info.Comment = metadata.Comment ?? string.Empty;
        info.Date = metadata.Date ?? info.Date;
        return info;
    }

    public Stream OpenFile(AmigaPath path, VolumeFileMode mode)
    {
        var existed = File.Exists(path.HostPath);
        FileStream stream;
        switch (mode)
        {
            case VolumeFileMode.NewFile:
                stream = new FileStream(path.HostPath, FileMode.Create, FileAccess.ReadWrite,
                    FileShare.ReadWrite | FileShare.Delete);
                break;
            case VolumeFileMode.ReadWrite:
                stream = new FileStream(path.HostPath, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                    FileShare.ReadWrite | FileShare.Delete);
                break;
            default:
                try
                {
                    stream = new FileStream(path.HostPath, FileMode.Open, FileAccess.ReadWrite,
                        FileShare.ReadWrite | FileShare.Delete);
                }
                catch (UnauthorizedAccessException)
                {
                    // read only file
                    stream = new FileStream(path.HostPath, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete);
                }

                break;
        }

        if (!existed)
        {
            Metadata.Created(path.HostPath, path.Name);
        }

        return stream;
    }

    public void CreateDirectory(AmigaPath path)
    {
        Directory.CreateDirectory(path.HostPath);
        Metadata.Created(path.HostPath, path.Name);
    }

    public void Delete(AmigaPath path)
    {
        if (Directory.Exists(path.HostPath))
        {
            if (!Metadata.IsEmptyDirectory(path.HostPath))
            {
                throw new VolumeException(Libraries.Dos.DosErrors.DirectoryNotEmpty,
                    $"Directory '{path.FullPath}' is not empty");
            }

            Metadata.DeleteDirectory(path.HostPath);
        }
        else
        {
            File.Delete(path.HostPath);
        }

        Metadata.Deleted(path.HostPath);
    }

    public void Rename(AmigaPath oldPath, AmigaPath newPath)
    {
        var metadata = Metadata.Get(oldPath.HostPath);
        if (Directory.Exists(oldPath.HostPath))
        {
            Directory.Move(oldPath.HostPath, newPath.HostPath);
        }
        else
        {
            File.Move(oldPath.HostPath, newPath.HostPath);
        }

        Metadata.Renamed(oldPath.HostPath, newPath.HostPath, newPath.Name, metadata);
    }

    public void SetProtection(AmigaPath path, uint protection)
    {
        if (!path.IsRoot)
        {
            Metadata.Set(path.HostPath, path.Name, x => x.Protection = protection);
        }
    }

    public void SetComment(AmigaPath path, string comment)
    {
        if (!path.IsRoot)
        {
            Metadata.Set(path.HostPath, path.Name, x => x.Comment = comment);
        }
    }

    public void SetDate(AmigaPath path, DateTime date)
    {
        if (Directory.Exists(path.HostPath))
        {
            Directory.SetLastWriteTime(path.HostPath, date);
        }
        else
        {
            File.SetLastWriteTime(path.HostPath, date);
        }

        // date stored in metadata is used instead of host date, e.g. in uae metafiles
        if (!path.IsRoot && Metadata.Get(path.HostPath).Date != null)
        {
            Metadata.Set(path.HostPath, path.Name, x => x.Date = date);
        }
    }

    public (long Size, long Free) GetSpace(AmigaPath path)
    {
        try
        {
            var root = Path.GetPathRoot(path.HostPath);
            if (!string.IsNullOrEmpty(root))
            {
                var drive = new DriveInfo(root);
                return (drive.TotalSize, drive.AvailableFreeSpace);
            }
        }
        catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException)
        {
        }

        return (DefaultSize, DefaultFree);
    }
}
