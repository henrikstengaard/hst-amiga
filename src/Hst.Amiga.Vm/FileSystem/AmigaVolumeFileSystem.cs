namespace Hst.Amiga.Vm.FileSystem;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.FileSystems;
using Hst.Amiga.FileSystems.Exceptions;
using Libraries.Dos;
using AmigaFileMode = Hst.Amiga.FileSystems.FileMode;

/// <summary>
/// Volume file system mapped to an Amiga file system volume, e.g. a fast file system in an adf file or a
/// partition in a disk image file. Files opened are read into memory and written back to the volume, when
/// flushed or closed after they have been changed.
/// </summary>
public class AmigaVolumeFileSystem : IImageFileSystem
{
    private static readonly char[] InvalidNameChars = { '/', ':', '\0' };

    private readonly IFileSystemVolume volume;
    private readonly Action<string> log;
    private readonly DateTime rootDate = DateTime.Now;

    /// <summary>
    /// Create volume file system for mounted Amiga file system volume.
    /// </summary>
    /// <param name="volume">Mounted Amiga file system volume.</param>
    /// <param name="readOnly">Volume is write protected and changes are rejected.</param>
    /// <param name="log">Action receiving warnings, if any.</param>
    public AmigaVolumeFileSystem(IFileSystemVolume volume, bool readOnly, Action<string> log = null)
    {
        this.volume = volume ?? throw new ArgumentNullException(nameof(volume));
        this.log = log;
        IsReadOnly = readOnly;
    }

    /// <summary>
    /// Mounted Amiga file system volume.
    /// </summary>
    public IFileSystemVolume Volume => volume;

    /// <summary>
    /// Name of Amiga file system volume.
    /// </summary>
    public string Name => volume.Name;

    public string HostRoot => null;

    public bool IsReadOnly { get; }

    public bool IsValidName(string amigaName) =>
        !string.IsNullOrEmpty(amigaName) && amigaName.IndexOfAny(InvalidNameChars) < 0;

    public AmigaPath GetRoot(string volumeName) => new(volumeName, Array.Empty<string>(), null);

    public AmigaPath GetChild(AmigaPath directory, string amigaName)
    {
        var entry = FindEntry(directory.Components, amigaName);
        return new AmigaPath(directory.Volume,
            directory.Components.Concat(new[] { entry?.Name ?? amigaName }).ToArray(), null);
    }

    public string[] ListNames(AmigaPath directory)
    {
        try
        {
            ChangeDirectory(directory.Components);
            return RunSync(() => volume.ListEntries()).Select(x => x.Name).ToArray();
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

        var entry = FindEntry(Parent(path), path.Name);
        if (entry == null)
        {
            return null;
        }

        var isDirectory = entry.Type is EntryType.Dir or EntryType.DirLink ||
                          (entry.Type == EntryType.SoftLink && entry.LinkEntry?.Type == EntryType.Dir);
        return new VolumeEntryInfo
        {
            IsDirectory = isDirectory,
            Size = isDirectory ? 0 : entry.Size,
            Date = entry.Date,
            Protection = ProtectionBitsConverter.ToProtectionValue(entry.ProtectionBits),
            Comment = entry.Comment ?? string.Empty
        };
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
            ThrowIfReadOnly();
            Run(() =>
            {
                ChangeDirectory(Parent(path));
                RunSync(() => volume.CreateFile(path.Name, true, true));
            });
            return new BufferedFileStream(this, path, Array.Empty<byte>());
        }

        var data = ReadFile(path);
        return IsReadOnly
            ? new MemoryStream(data, false)
            : new BufferedFileStream(this, path, data);
    }

    public void CreateDirectory(AmigaPath path)
    {
        ThrowIfReadOnly();
        Run(() =>
        {
            ChangeDirectory(Parent(path));
            RunSync(() => volume.CreateDirectory(path.Name));
        });
    }

    public void Delete(AmigaPath path)
    {
        ThrowIfReadOnly();
        Run(() =>
        {
            ChangeDirectory(Parent(path));
            RunSync(() => volume.Delete(path.Name, true));
        });
    }

    public void Rename(AmigaPath oldPath, AmigaPath newPath)
    {
        ThrowIfReadOnly();
        // file systems rename entries in current directory to path, which can be absolute
        var oldName = oldPath.Name;
        var newName = "/" + string.Join("/", newPath.Components);
        Run(() =>
        {
            ChangeDirectory(Parent(oldPath));
            if (oldPath.IsSame(newPath))
            {
                if (string.Equals(oldName, newPath.Name, StringComparison.Ordinal))
                {
                    return;
                }

                // names are case insensitive, so changing case renames via temporary name
                var temporaryName = $"{newPath.Name}.{Guid.NewGuid():N}".Substring(0, 30);
                RunSync(() => volume.Rename(oldName, temporaryName));
                oldName = temporaryName;
            }

            RunSync(() => volume.Rename(oldName, newName));
        });
    }

    public void SetProtection(AmigaPath path, uint protection)
    {
        if (path.IsRoot)
        {
            return;
        }

        ThrowIfReadOnly();
        Run(() =>
        {
            ChangeDirectory(Parent(path));
            RunSync(() => volume.SetProtectionBits(path.Name,
                ProtectionBitsConverter.ToProtectionBits((int)(protection & 0xFF))));
        });
    }

    public void SetComment(AmigaPath path, string comment)
    {
        if (path.IsRoot)
        {
            return;
        }

        ThrowIfReadOnly();
        Run(() =>
        {
            ChangeDirectory(Parent(path));
            RunSync(() => volume.SetComment(path.Name, comment ?? string.Empty));
        });
    }

    public void SetDate(AmigaPath path, DateTime date)
    {
        if (path.IsRoot)
        {
            return;
        }

        ThrowIfReadOnly();
        Run(() =>
        {
            ChangeDirectory(Parent(path));
            RunSync(() => volume.SetDate(path.Name, date));
        });
    }

    public (long Size, long Free) GetSpace(AmigaPath path)
    {
        try
        {
            return (volume.Size, volume.Free);
        }
        catch (Exception e) when (IsFileSystemError(e))
        {
            return (0, 0);
        }
    }

    /// <summary>
    /// Flush changes to volume.
    /// </summary>
    public void Flush()
    {
        if (!IsReadOnly)
        {
            Run(() => RunSync(() => volume.Flush()));
        }
    }

    public void Dispose()
    {
        volume.Dispose();
    }

    private static string[] Parent(AmigaPath path) =>
        path.Components.Take(path.Components.Length - 1).ToArray();

    private Entry FindEntry(string[] directory, string name)
    {
        try
        {
            ChangeDirectory(directory);
            var result = RunSync(() => volume.FindEntry(name));
            return result?.Entry != null && (result.PartsNotFound == null || !result.PartsNotFound.Any())
                ? result.Entry
                : null;
        }
        catch (Exception e) when (IsFileSystemError(e))
        {
            return null;
        }
    }

    /// <summary>
    /// Change current directory of volume to absolute path of directory from root.
    /// </summary>
    private void ChangeDirectory(IEnumerable<string> directory) =>
        RunSync(() => volume.ChangeDirectory("/" + string.Join("/", directory)));

    private byte[] ReadFile(AmigaPath path)
    {
        return Run(() =>
        {
            ChangeDirectory(Parent(path));
            using var stream = RunSync(() => volume.OpenFile(path.Name, AmigaFileMode.Read, false, true));
            using var memoryStream = new MemoryStream();
            RunSync(() => stream.CopyToAsync(memoryStream));
            return memoryStream.ToArray();
        });
    }

    private void WriteFile(AmigaPath path, byte[] data, int length)
    {
        Run(() =>
        {
            // protection bits and comment are kept, when file is overwritten
            var info = GetInfo(path);
            ChangeDirectory(Parent(path));
            using (var stream = RunSync(() =>
                       volume.OpenFile(path.Name, AmigaFileMode.Write, true, true)))
            {
                RunSync(() => stream.WriteAsync(data, 0, length));
            }

            if (info == null)
            {
                return;
            }

            ChangeDirectory(Parent(path));
            var after = GetInfo(path);
            if (after != null && after.Protection != info.Protection)
            {
                SetProtection(path, info.Protection);
            }

            if (after != null && after.Comment != info.Comment && info.Comment.Length > 0)
            {
                SetComment(path, info.Comment);
            }
        });
    }

    private void ThrowIfReadOnly()
    {
        if (IsReadOnly)
        {
            throw new VolumeException(DosErrors.DiskWriteProtected, $"Volume '{volume.Name}' is write protected");
        }
    }

    private static bool IsFileSystemError(Exception e) =>
        e is FileSystemException or IOException or ArgumentException or NotSupportedException;

    private static void Run(Action action) => Run(() =>
    {
        action();
        return true;
    });

    /// <summary>
    /// Run file system operation mapping file system errors to volume exceptions with dos error codes.
    /// </summary>
    private static T Run<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (VolumeException)
        {
            throw;
        }
        catch (PathNotFoundException e)
        {
            throw new VolumeException(DosErrors.ObjectNotFound, e.Message, e);
        }
        catch (PathAlreadyExistsException e)
        {
            throw new VolumeException(DosErrors.ObjectExists, e.Message, e);
        }
        catch (DirectoryNotEmptyException e)
        {
            throw new VolumeException(DosErrors.DirectoryNotEmpty, e.Message, e);
        }
        catch (DiskFullException e)
        {
            throw new VolumeException(DosErrors.DiskFull, e.Message, e);
        }
        catch (NotAFileException e)
        {
            throw new VolumeException(DosErrors.ObjectWrongType, e.Message, e);
        }
        catch (FileSystemException e)
        {
            throw new VolumeException(DosErrors.ObjectInUse, e.Message, e);
        }
        catch (NotSupportedException e)
        {
            // writing to read only stream
            throw new VolumeException(DosErrors.DiskWriteProtected, e.Message, e);
        }
        catch (ArgumentException e)
        {
            throw new VolumeException(DosErrors.InvalidComponentName, e.Message, e);
        }
    }

    /// <summary>
    /// Run async file system operation synchronously, as dos library functions are synchronous.
    /// </summary>
    private static void RunSync(Func<Task> operation)
    {
        if (SynchronizationContext.Current != null || TaskScheduler.Current != TaskScheduler.Default)
        {
            Task.Run(operation).GetAwaiter().GetResult();
            return;
        }

        operation().GetAwaiter().GetResult();
    }

    private static T RunSync<T>(Func<Task<T>> operation) =>
        SynchronizationContext.Current != null || TaskScheduler.Current != TaskScheduler.Default
            ? Task.Run(operation).GetAwaiter().GetResult()
            : operation().GetAwaiter().GetResult();

    /// <summary>
    /// File data in memory written back to volume, when flushed or closed after it has been changed.
    /// </summary>
    private class BufferedFileStream : MemoryStream
    {
        private readonly AmigaVolumeFileSystem fileSystem;
        private readonly AmigaPath path;
        private bool modified;
        private bool disposed;

        public BufferedFileStream(AmigaVolumeFileSystem fileSystem, AmigaPath path, byte[] data)
        {
            this.fileSystem = fileSystem;
            this.path = path;
            base.Write(data, 0, data.Length);
            Position = 0;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            base.Write(buffer, offset, count);
            modified = true;
        }

        public override void WriteByte(byte value)
        {
            base.WriteByte(value);
            modified = true;
        }

        public override void SetLength(long value)
        {
            base.SetLength(value);
            modified = true;
        }

        public override void Flush()
        {
            if (!modified || disposed)
            {
                return;
            }

            fileSystem.WriteFile(path, GetBuffer(), (int)Length);
            modified = false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                try
                {
                    Flush();
                }
                catch (IOException e)
                {
                    fileSystem.log?.Invoke($"Warning: Unable to write '{path.FullPath}': {e.Message}");
                }

                disposed = true;
            }

            base.Dispose(disposing);
        }
    }
}
