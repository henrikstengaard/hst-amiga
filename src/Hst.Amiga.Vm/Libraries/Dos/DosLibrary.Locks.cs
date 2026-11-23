namespace Hst.Amiga.Vm.Libraries.Dos;

using System;
using System.IO;
using System.Linq;
using FileSystem;
using Structs;

public partial class DosLibrary
{
    private const uint FibFlagsWrite = 1 << 2;
    private const uint FibFlagsDelete = 1 << 0;

    private void RegisterLockFunctions()
    {
        Register("Lock", () => D0 = Lock(Ram.ReadCString(D1), (int)D2));
        Register("UnLock", () => FreeLock(D1));
        Register("DupLock", () =>
        {
            if (D1 == 0)
            {
                D0 = 0;
                return;
            }

            D0 = TryGetLock(D1, out var dosLock) ? CreateLock(dosLock.Path, SharedLock) : 0;
        });
        Register("Examine", () =>
        {
            if (!TryGetLock(D1, out var dosLock) || !Exists(dosLock.Path))
            {
                SetIoErr(DosErrors.ObjectNotFound);
                D0 = 0;
                return;
            }

            dosLock.Entries = null;
            FillFileInfoBlock(D2, dosLock.Path);
            D0 = unchecked((uint)DosTrue);
        });
        Register("ExNext", ExNext);
        Register("ExamineFH", () =>
        {
            var file = GetFile(D1);
            if (file?.Path == null)
            {
                SetIoErr(DosErrors.ObjectWrongType);
                D0 = 0;
                return;
            }

            file.Output?.Flush();
            FillFileInfoBlock(D2, file.Path);
            D0 = unchecked((uint)DosTrue);
        });
        Register("Info", () =>
        {
            if (!TryGetLock(D1, out var dosLock))
            {
                D0 = 0;
                return;
            }

            FillInfoData(D2, dosLock.Path);
            D0 = unchecked((uint)DosTrue);
        });
        Register("CreateDir", CreateDir);
        Register("CurrentDir", () =>
        {
            var old = Ram.ReadLong(Process + ProcessStruct.CurrentDir);
            Ram.WriteLong(Process + ProcessStruct.CurrentDir, D1);
            D0 = old;
        });
        Register("ParentDir", () =>
        {
            if (!TryGetLock(D1, out var dosLock))
            {
                D0 = 0;
                return;
            }

            D0 = ParentLock(dosLock.Path);
        });
        Register("DupLockFromFH", () =>
        {
            var file = GetFile(D1);
            D0 = file?.Path == null ? 0 : CreateLock(file.Path, SharedLock);
        });
        Register("ParentOfFH", () =>
        {
            var file = GetFile(D1);
            D0 = file?.Path == null ? 0 : ParentLock(file.Path);
        });
        Register("OpenFromLock", () =>
        {
            if (D1 == 0 || !TryGetLock(D1, out var dosLock))
            {
                D0 = 0;
                return;
            }

            var fileHandle = Open(dosLock.Path.FullPath, ModeOldFile);
            if (fileHandle != 0)
            {
                FreeLock(D1);
            }

            D0 = fileHandle;
        });
        Register("NameFromLock", () =>
        {
            if (!TryGetLock(D1, out var dosLock))
            {
                D0 = 0;
                return;
            }

            D0 = NameToBuffer(dosLock.Path, D2, (int)D3);
        });
        Register("NameFromFH", () =>
        {
            var file = GetFile(D1);
            if (file?.Path == null)
            {
                SetIoErr(DosErrors.ObjectWrongType);
                D0 = 0;
                return;
            }

            D0 = NameToBuffer(file.Path, D2, (int)D3);
        });
        Register("SameLock", () =>
        {
            if (!TryGetLock(D1, out var lock1) || !TryGetLock(D2, out var lock2))
            {
                D0 = 0xFFFFFFFF;
                return;
            }

            D0 = lock1.Path.IsSame(lock2.Path)
                ? 0u // LOCK_SAME
                : string.Equals(lock1.Path.Volume, lock2.Path.Volume, StringComparison.OrdinalIgnoreCase)
                    ? 1u // LOCK_SAME_VOLUME
                    : 0xFFFFFFFF; // LOCK_DIFFERENT
        });
        Register("SameDevice", () =>
        {
            D0 = TryGetLock(D1, out var lock1) && TryGetLock(D2, out var lock2) &&
                 string.Equals(lock1.Path.Volume, lock2.Path.Volume, StringComparison.OrdinalIgnoreCase)
                ? unchecked((uint)DosTrue)
                : 0;
        });
        Register("DeleteFile", DeleteFile);
        Register("Rename", Rename);
        Register("SetProtection", () =>
        {
            var path = ResolveOrFail(Ram.ReadCString(D1));
            if (path == null || !Exists(path))
            {
                SetIoErr(DosErrors.ObjectNotFound);
                D0 = 0;
                return;
            }

            var protection = D2;
            try
            {
                FileSystemOf(path).SetProtection(path, protection);
                D0 = unchecked((uint)DosTrue);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                SetIoErr(MapException(e));
                D0 = 0;
            }
        });
        Register("SetComment", () =>
        {
            var path = ResolveOrFail(Ram.ReadCString(D1));
            var comment = Ram.ReadCString(D2) ?? string.Empty;
            if (path == null || !Exists(path))
            {
                SetIoErr(DosErrors.ObjectNotFound);
                D0 = 0;
                return;
            }

            if (comment.Length > 79)
            {
                SetIoErr(DosErrors.CommentTooBig);
                D0 = 0;
                return;
            }

            try
            {
                FileSystemOf(path).SetComment(path, comment);
                D0 = unchecked((uint)DosTrue);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                SetIoErr(MapException(e));
                D0 = 0;
            }
        });
        Register("SetFileDate", () =>
        {
            var path = ResolveOrFail(Ram.ReadCString(D1));
            if (path == null || !Exists(path))
            {
                SetIoErr(DosErrors.ObjectNotFound);
                D0 = 0;
                return;
            }

            try
            {
                FileSystemOf(path).SetDate(path, ReadDateStamp(D2));
                D0 = unchecked((uint)DosTrue);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            {
                SetIoErr(MapException(e));
                D0 = 0;
            }
        });
        Register("LoadSeg", LoadSeg);
        Register("UnLoadSeg", () =>
        {
            if (D1 != 0)
            {
                loader.Unload(D1);
            }

            D0 = unchecked((uint)DosTrue);
        });
        Register("IsFileSystem", () =>
        {
            // names without device are relative to current directory, which is always on a file system
            var name = Ram.ReadCString(D1) ?? string.Empty;
            var colon = name.IndexOf(':');
            if (colon < 0)
            {
                D0 = unchecked((uint)DosTrue);
                return;
            }

            var device = name.Substring(0, colon);
            D0 = device.Length == 0 || volumes.HasVolume(device) || volumes.HasAssign(device)
                ? unchecked((uint)DosTrue)
                : 0;
        });
        Register("DeviceProc", () =>
        {
            var path = ResolveOrFail(Ram.ReadCString(D1));
            D0 = path == null ? 0 : dummyPort;
        });
        Register("ExAll", ExAll);
        Register("ExAllEnd", () => D0 = unchecked((uint)DosTrue));
    }

    /// <summary>
    /// Lock file or directory.
    /// </summary>
    /// <returns>BCPL pointer to lock or zero, if object doesn't exist.</returns>
    public uint Lock(string name, int access)
    {
        var path = ResolveOrFail(name);
        if (path == null)
        {
            return 0;
        }

        if (!Exists(path))
        {
            SetIoErr(DosErrors.ObjectNotFound);
            return 0;
        }

        return CreateLock(path, access == ExclusiveLock ? ExclusiveLock : SharedLock);
    }

    private uint ParentLock(AmigaPath path)
    {
        var parent = volumes.GetParent(path);
        if (parent == null)
        {
            SetIoErr(0);
            return 0;
        }

        return CreateLock(parent, SharedLock);
    }

    private uint NameToBuffer(AmigaPath path, uint buffer, int length)
    {
        if (WriteString(buffer, length, path.FullPath))
        {
            return unchecked((uint)DosTrue);
        }

        SetIoErr(DosErrors.LineTooLong);
        return 0;
    }

    private uint GetProtection(AmigaPath path) => volumes.GetInfo(path)?.Protection ?? 0;

    /// <summary>
    /// Fill file info block with information about file or directory.
    /// </summary>
    private void FillFileInfoBlock(uint fib, AmigaPath path, int diskKey = 0)
    {
        Ram.Fill(fib, FileInfoBlockStruct.Size);

        var info = volumes.GetInfo(path) ?? new VolumeEntryInfo { Date = DateTime.Now };
        var isDirectory = path.IsRoot || info.IsDirectory;
        var entryType = path.IsRoot ? StRoot : isDirectory ? StUserDir : StFile;
        var size = isDirectory ? 0 : info.Size;
        var date = info.Date;

        Ram.WriteLong(fib + FileInfoBlockStruct.DiskKey, (uint)diskKey);
        Ram.WriteLong(fib + FileInfoBlockStruct.DirEntryType, (uint)entryType);
        WriteString(fib + FileInfoBlockStruct.FileName, 108, path.Name);
        Ram.WriteLong(fib + FileInfoBlockStruct.Protection, info.Protection);
        Ram.WriteLong(fib + FileInfoBlockStruct.EntryType, (uint)entryType);
        Ram.WriteLong(fib + FileInfoBlockStruct.FileSize, (uint)Math.Min(size, uint.MaxValue));
        Ram.WriteLong(fib + FileInfoBlockStruct.NumBlocks, (uint)Math.Min((size + 511) / 512, uint.MaxValue));
        WriteDateStamp(fib + FileInfoBlockStruct.Date, date);
        WriteString(fib + FileInfoBlockStruct.Comment, 80, info.Comment ?? string.Empty);
    }

    private void FillInfoData(uint infoData, AmigaPath path)
    {
        Ram.Fill(infoData, InfoDataStruct.Size);
        const uint blockSize = 512;
        var fileSystem = FileSystemOf(path);
        var (size, free) = fileSystem.GetSpace(path);
        var totalBlocks = Math.Min(size / blockSize, int.MaxValue);
        var freeBlocks = Math.Min(free / blockSize, totalBlocks);

        // ID_WRITE_PROTECTED or ID_VALIDATED
        Ram.WriteLong(infoData + InfoDataStruct.DiskState, fileSystem.IsReadOnly ? 80u : 82u);
        Ram.WriteLong(infoData + InfoDataStruct.NumBlocks, (uint)totalBlocks);
        Ram.WriteLong(infoData + InfoDataStruct.NumBlocksUsed, (uint)(totalBlocks - freeBlocks));
        Ram.WriteLong(infoData + InfoDataStruct.BytesPerBlock, blockSize);
        Ram.WriteLong(infoData + InfoDataStruct.DiskType, 0x444F5300); // ID_DOS_DISK
        Ram.WriteLong(infoData + InfoDataStruct.InUse, unchecked((uint)DosTrue));
    }

    /// <summary>
    /// Get Amiga names of entries in directory excluding metadata files.
    /// </summary>
    private string[] GetDirectoryEntries(AmigaPath path) => volumes.ListNames(path);

    private AmigaPath Child(AmigaPath directory, string name) => volumes.GetChild(directory, name);

    private void ExNext()
    {
        var fib = D2;
        if (!TryGetLock(D1, out var dosLock))
        {
            D0 = 0;
            return;
        }

        if (!IsDirectory(dosLock.Path))
        {
            SetIoErr(DosErrors.ObjectWrongType);
            D0 = 0;
            return;
        }

        // zero lock is created per call, entries are cached by fib disk key starting at zero
        var entries = dosLock.Entries ??= GetDirectoryEntries(dosLock.Path);
        var index = (int)Ram.ReadLong(fib + FileInfoBlockStruct.DiskKey);
        while (index >= 0 && index < entries.Count)
        {
            var entryPath = Child(dosLock.Path, entries[index]);
            index++;
            if (!Exists(entryPath))
            {
                continue;
            }

            FillFileInfoBlock(fib, entryPath, index);
            D0 = unchecked((uint)DosTrue);
            return;
        }

        if (dosLock.Address == 0)
        {
            dosLock.Entries = null;
        }

        SetIoErr(DosErrors.NoMoreEntries);
        D0 = 0;
    }

    private void CreateDir()
    {
        var path = ResolveOrFail(Ram.ReadCString(D1));
        if (path == null)
        {
            D0 = 0;
            return;
        }

        if (Exists(path))
        {
            SetIoErr(DosErrors.ObjectExists);
            D0 = 0;
            return;
        }

        var parent = volumes.GetParent(path);
        if (parent == null || !IsDirectory(parent))
        {
            SetIoErr(DosErrors.DirNotFound);
            D0 = 0;
            return;
        }

        try
        {
            FileSystemOf(path).CreateDirectory(path);
            D0 = CreateLock(path, ExclusiveLock);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            SetIoErr(MapException(e));
            D0 = 0;
        }
    }

    private void DeleteFile()
    {
        var path = ResolveOrFail(Ram.ReadCString(D1));
        if (path == null)
        {
            D0 = 0;
            return;
        }

        if (path.IsRoot || !Exists(path))
        {
            SetIoErr(path.IsRoot ? DosErrors.ObjectWrongType : DosErrors.ObjectNotFound);
            D0 = 0;
            return;
        }

        if ((GetProtection(path) & FibFlagsDelete) != 0)
        {
            SetIoErr(DosErrors.DeleteProtected);
            D0 = 0;
            return;
        }

        try
        {
            FileSystemOf(path).Delete(path);
            D0 = unchecked((uint)DosTrue);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            SetIoErr(MapException(e));
            D0 = 0;
        }
    }

    private void Rename()
    {
        var oldPath = ResolveOrFail(Ram.ReadCString(D1));
        var newPath = oldPath == null ? null : ResolveOrFail(Ram.ReadCString(D2));
        if (oldPath == null || newPath == null)
        {
            D0 = 0;
            return;
        }

        if (!Exists(oldPath))
        {
            SetIoErr(DosErrors.ObjectNotFound);
            D0 = 0;
            return;
        }

        if (!string.Equals(oldPath.Volume, newPath.Volume, StringComparison.OrdinalIgnoreCase))
        {
            SetIoErr(DosErrors.RenameAcrossDevices);
            D0 = 0;
            return;
        }

        var caseOnly = oldPath.IsSame(newPath);
        if (Exists(newPath) && !caseOnly)
        {
            SetIoErr(DosErrors.ObjectExists);
            D0 = 0;
            return;
        }

        try
        {
            FileSystemOf(oldPath).Rename(oldPath, newPath);
            D0 = unchecked((uint)DosTrue);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            SetIoErr(MapException(e));
            D0 = 0;
        }
    }

    private void LoadSeg()
    {
        var path = ResolveOrFail(Ram.ReadCString(D1));
        if (path == null)
        {
            D0 = 0;
            return;
        }

        if (!IsFile(path))
        {
            SetIoErr(DosErrors.ObjectNotFound);
            D0 = 0;
            return;
        }

        try
        {
            D0 = LoadSegment(path).Bptr;
        }
        catch (IOException)
        {
            SetIoErr(DosErrors.FileNotObject);
            D0 = 0;
        }
        catch (VmException)
        {
            SetIoErr(DosErrors.NoFreeStore);
            D0 = 0;
        }
    }

    private void ExAll()
    {
        const int edName = 1;
        const int edType = 2;
        const int edSize = 3;
        const int edProtection = 4;
        const int edDate = 5;
        const int edComment = 6;

        var buffer = D2;
        var size = (int)D3;
        var type = (int)D4;
        var control = D5;

        if (!TryGetLock(D1, out var dosLock) || type < edName || type > 7)
        {
            SetIoErr(type < edName || type > 7 ? DosErrors.BadNumber : DosErrors.InvalidLock);
            D0 = 0;
            return;
        }

        if (!IsDirectory(dosLock.Path))
        {
            SetIoErr(DosErrors.ObjectWrongType);
            D0 = 0;
            return;
        }

        var entries = GetDirectoryEntries(dosLock.Path);
        var index = (int)Ram.ReadLong(control + ExAllControlStruct.LastKey);
        var matchString = Ram.ReadLong(control + ExAllControlStruct.MatchString);
        var pattern = matchString == 0 ? null : new AmigaPattern(Ram.ReadCString(matchString), true);

        var structSize = type switch
        {
            edName => 8,
            edType => 12,
            edSize => 16,
            edProtection => 20,
            edDate => 32,
            edComment => 36,
            _ => 40
        };

        var count = 0;
        var position = buffer;
        var end = buffer + (uint)size;
        uint previous = 0;
        while (index < entries.Length)
        {
            var name = entries[index];
            if (pattern != null && !pattern.IsMatch(name))
            {
                index++;
                continue;
            }

            var entryPath = Child(dosLock.Path, name);
            var nameBytes = Ram.GetBytes(name);
            var info = volumes.GetInfo(entryPath) ?? new VolumeEntryInfo { Date = DateTime.Now };
            var comment = info.Comment ?? string.Empty;
            var commentBytes = type >= edComment ? Ram.GetBytes(comment) : Array.Empty<byte>();
            var required = (uint)(structSize + nameBytes.Length + 1 + (type >= edComment ? commentBytes.Length + 1 : 0));
            required = (required + 3) & ~3u;
            if (position + required > end)
            {
                break;
            }

            Ram.Fill(position, required);
            var stringPosition = position + (uint)structSize;
            Ram.WriteBytes(stringPosition, nameBytes);
            Ram.WriteLong(position + 4, stringPosition);
            stringPosition += (uint)nameBytes.Length + 1;

            var isDirectory = info.IsDirectory;
            var fileSize = isDirectory ? 0 : info.Size;
            var date = info.Date;

            if (type >= edType)
            {
                Ram.WriteLong(position + 8, (uint)(isDirectory ? StUserDir : StFile));
            }

            if (type >= edSize)
            {
                Ram.WriteLong(position + 12, (uint)Math.Min(fileSize, uint.MaxValue));
            }

            if (type >= edProtection)
            {
                Ram.WriteLong(position + 16, info.Protection);
            }

            if (type >= edDate)
            {
                WriteDateStamp(position + 20, date);
            }

            if (type >= edComment)
            {
                Ram.WriteBytes(stringPosition, commentBytes);
                Ram.WriteLong(position + 32, stringPosition);
            }

            if (previous != 0)
            {
                Ram.WriteLong(previous, position);
            }

            previous = position;
            position += required;
            count++;
            index++;
        }

        Ram.WriteLong(control + ExAllControlStruct.Entries, (uint)count);
        Ram.WriteLong(control + ExAllControlStruct.LastKey, (uint)index);

        if (index >= entries.Length)
        {
            SetIoErr(DosErrors.NoMoreEntries);
            D0 = 0;
            return;
        }

        if (count == 0)
        {
            // buffer too small for a single entry
            SetIoErr(DosErrors.BufferOverflow);
            D0 = 0;
            return;
        }

        D0 = unchecked((uint)DosTrue);
    }
}
