namespace Hst.Amiga.Vm.Libraries.Dos;

using System;
using System.Collections.Generic;
using System.Linq;
using FileSystem;
using Structs;

/// <summary>
/// Assigns, dos list and device procs mapped to volumes and assigns in volume manager. The dos list in memory is
/// built from volumes and assigns and rebuilt, when assigns are changed.
/// </summary>
public partial class DosLibrary
{
    private const int DltDevice = 0;
    private const int DltDirectory = 1;
    private const int DltVolume = 2;
    private const int DltLate = 3;
    private const int DltNonBinding = 4;

    private const uint LdfDevices = 4;
    private const uint LdfVolumes = 8;
    private const uint LdfAssigns = 16;

    private const uint DvpfUnlock = 1;
    private const uint DvpfAssign = 2;

    private const uint IdDosDisk = 0x444F5300;
    private const int MaxDosListEntries = 10000;

    /// <summary>
    /// Address of DosInfo structure holding dos list.
    /// </summary>
    private uint dosInfo;

    /// <summary>
    /// Entries in dos list owned by dos.library by address.
    /// </summary>
    private readonly Dictionary<uint, DosListEntry> dosListEntries = new();

    /// <summary>
    /// Volume entries in dos list by volume name. Volume entries are kept, when dos list is rebuilt, as locks
    /// point to their volume entry.
    /// </summary>
    private readonly Dictionary<string, uint> volumeEntries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Dos list entries owned by program with address of their name, created with MakeDosEntry() or removed
    /// from dos list with RemDosEntry().
    /// </summary>
    private readonly Dictionary<uint, uint> programDosEntries = new();

    /// <summary>
    /// Assign targets of device procs by address.
    /// </summary>
    private readonly Dictionary<uint, DeviceProcState> deviceProcs = new();

    private sealed class DosListEntry
    {
        public uint NameAddress { get; set; }
        public List<uint> Allocations { get; } = new();
        public List<(uint Bptr, DosLock Lock)> Locks { get; } = new();
    }

    private sealed class DeviceProcState
    {
        public List<AmigaPath> Targets { get; set; }
        public int Index { get; set; }
    }

    private void RegisterAssignFunctions()
    {
        Register("AssignLock", () => D0 = DosBool(AssignLock(Ram.ReadCString(D1), D2)));
        Register("AssignLate",
            () => D0 = DosBool(AssignPath(Ram.ReadCString(D1), Ram.ReadCString(D2), AssignType.Late)));
        Register("AssignPath",
            () => D0 = DosBool(AssignPath(Ram.ReadCString(D1), Ram.ReadCString(D2), AssignType.NonBinding)));
        Register("AssignAdd", () => D0 = DosBool(AssignAdd(Ram.ReadCString(D1), D2)));
        Register("RemAssignList", () => D0 = DosBool(RemAssignList(Ram.ReadCString(D1), D2)));

        // dos list is only changed by the running program, so locking always succeeds
        Register("LockDosList", () => D0 = DosListHead);
        Register("AttemptLockDosList", () => D0 = DosListHead);
        Register("UnLockDosList", () => { });
        Register("NextDosEntry", () => D0 = NextDosEntry(D1, D2));
        Register("FindDosEntry", () => D0 = FindDosEntry(D1, Ram.ReadCString(D2), D3));
        Register("MakeDosEntry", () => D0 = MakeDosEntry(Ram.ReadCString(D1), (int)D2));
        Register("FreeDosEntry", () => FreeDosEntry(D1));
        Register("AddDosEntry", () => D0 = DosBool(AddDosEntry(D1)));
        Register("RemDosEntry", () => D0 = DosBool(RemDosEntry(D1)));

        Register("GetDeviceProc", () => D0 = GetDeviceProc(Ram.ReadCString(D1), D2));
        Register("FreeDeviceProc", () => FreeDeviceProc(D1));
    }

    private static uint DosBool(bool value) => value ? unchecked((uint)DosTrue) : 0;

    /// <summary>
    /// Pointer returned by LockDosList(), which is the dos info field holding the first entry. dol_Next is at
    /// offset zero, so it works as a list node pointing to first entry.
    /// </summary>
    private uint DosListHead => dosInfo + DosInfoStruct.DevInfo;

    /// <summary>
    /// Assign name to directory of lock, removing assign if lock is zero. Assign takes ownership of the lock.
    /// </summary>
    public bool AssignLock(string name, uint lockBptr)
    {
        if (!IsValidAssignName(ref name))
        {
            return false;
        }

        if (lockBptr == 0)
        {
            if (!volumes.HasAssign(name))
            {
                SetIoErr(DosErrors.ObjectNotFound);
                return false;
            }

            volumes.RemoveAssign(name);
        }
        else
        {
            if (!locks.TryGetValue(lockBptr, out var dosLock))
            {
                SetIoErr(DosErrors.InvalidLock);
                return false;
            }

            volumes.SetAssign(name, dosLock.Path.FullPath, AssignType.Lock);
            FreeLock(lockBptr);
        }

        RebuildDosList();
        return true;
    }

    /// <summary>
    /// Assign name to path as late binding or non-binding assign.
    /// </summary>
    public bool AssignPath(string name, string path, AssignType type)
    {
        if (!IsValidAssignName(ref name))
        {
            return false;
        }

        if (string.IsNullOrEmpty(path))
        {
            SetIoErr(DosErrors.ObjectNotFound);
            return false;
        }

        // paths without device are relative to current directory at the time of the assign
        if (path.IndexOf(':') <= 0)
        {
            var resolved = ResolveOrFail(path);
            if (resolved == null)
            {
                return false;
            }

            path = resolved.FullPath;
        }

        volumes.SetAssign(name, path, type);
        RebuildDosList();
        return true;
    }

    /// <summary>
    /// Add directory of lock to existing assign creating a multi assign. Assign takes ownership of the lock.
    /// </summary>
    public bool AssignAdd(string name, uint lockBptr)
    {
        if (!IsValidAssignName(ref name))
        {
            return false;
        }

        if (!volumes.HasAssign(name))
        {
            SetIoErr(DosErrors.ObjectNotFound);
            return false;
        }

        if (volumes.GetAssignType(name) != AssignType.Lock)
        {
            SetIoErr(DosErrors.ObjectWrongType);
            return false;
        }

        if (!locks.TryGetValue(lockBptr, out var dosLock))
        {
            SetIoErr(DosErrors.InvalidLock);
            return false;
        }

        volumes.AddAssign(name, dosLock.Path.FullPath);
        FreeLock(lockBptr);
        RebuildDosList();
        return true;
    }

    /// <summary>
    /// Remove directory of lock from multi assign. Lock is still owned by caller.
    /// </summary>
    public bool RemAssignList(string name, uint lockBptr)
    {
        if (!IsValidAssignName(ref name))
        {
            return false;
        }

        if (!locks.TryGetValue(lockBptr, out var dosLock))
        {
            SetIoErr(DosErrors.InvalidLock);
            return false;
        }

        if (volumes.GetAssignType(name) != AssignType.Lock || !volumes.RemoveAssignPath(name, dosLock.Path))
        {
            SetIoErr(DosErrors.ObjectNotFound);
            return false;
        }

        RebuildDosList();
        return true;
    }

    private bool IsValidAssignName(ref string name)
    {
        name = (name ?? string.Empty).TrimEnd(':');
        if (name.Length == 0 || name.IndexOf(':') >= 0 || name.IndexOf('/') >= 0)
        {
            SetIoErr(DosErrors.InvalidComponentName);
            return false;
        }

        if (volumes.HasVolume(name))
        {
            SetIoErr(DosErrors.ObjectExists);
            return false;
        }

        return true;
    }

    private static uint TypeFlag(int type) => type switch
    {
        DltDevice => LdfDevices,
        DltVolume => LdfVolumes,
        DltDirectory or DltLate or DltNonBinding => LdfAssigns,
        _ => 0
    };

    private uint NextDosEntry(uint dosList, uint flags)
    {
        if (dosList == 0)
        {
            return 0;
        }

        var entry = BAddr(Ram.ReadLong(dosList + DosListStruct.Next));
        for (var i = 0; entry != 0 && i < MaxDosListEntries; i++)
        {
            if ((TypeFlag((int)Ram.ReadLong(entry + DosListStruct.Type)) & flags) != 0)
            {
                return entry;
            }

            entry = BAddr(Ram.ReadLong(entry + DosListStruct.Next));
        }

        return 0;
    }

    /// <summary>
    /// Find entry in dos list starting with entry passed, e.g. an entry returned by NextDosEntry(), or first entry,
    /// if pointer returned by LockDosList() is passed.
    /// </summary>
    private uint FindDosEntry(uint dosList, string name, uint flags)
    {
        name = (name ?? string.Empty).TrimEnd(':');
        var first = dosList != 0 && dosList != DosListHead &&
                    (TypeFlag((int)Ram.ReadLong(dosList + DosListStruct.Type)) & flags) != 0
            ? dosList
            : NextDosEntry(dosList, flags);
        for (var entry = first; entry != 0; entry = NextDosEntry(entry, flags))
        {
            if (string.Equals(DosEntryName(entry), name, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return 0;
    }

    private string DosEntryName(uint entry) =>
        (Ram.ReadBString(BAddr(Ram.ReadLong(entry + DosListStruct.Name))) ?? string.Empty).TrimEnd(':');

    private uint MakeDosEntry(string name, int type)
    {
        var entry = AllocateDosEntry(name, type);
        if (entry.Address == 0)
        {
            SetIoErr(DosErrors.NoFreeStore);
            return 0;
        }

        programDosEntries[entry.Address] = entry.NameAddress;
        return entry.Address;
    }

    private void FreeDosEntry(uint entry)
    {
        // entries in dos list are owned by dos.library and freed when the dos list is rebuilt
        if (entry == 0 || !programDosEntries.TryGetValue(entry, out var nameAddress))
        {
            return;
        }

        programDosEntries.Remove(entry);
        Context.Allocator.Free(nameAddress);
        Context.Allocator.Free(entry);
    }

    /// <summary>
    /// Add assign entry to dos list. Devices and volumes can't be added.
    /// </summary>
    private bool AddDosEntry(uint entry)
    {
        if (entry == 0)
        {
            SetIoErr(DosErrors.ObjectNotFound);
            return false;
        }

        var type = (int)Ram.ReadLong(entry + DosListStruct.Type);
        var name = DosEntryName(entry);
        if (name.Length == 0 || name.IndexOf('/') >= 0)
        {
            SetIoErr(DosErrors.InvalidComponentName);
            return false;
        }

        if (volumes.HasVolume(name) || volumes.HasAssign(name))
        {
            SetIoErr(DosErrors.ObjectExists);
            return false;
        }

        switch (type)
        {
            case DltDirectory:
                var paths = new List<string>();
                if (locks.TryGetValue(Ram.ReadLong(entry + DosListStruct.Lock), out var dosLock))
                {
                    paths.Add(dosLock.Path.FullPath);
                }

                var assignList = Ram.ReadLong(entry + DosListStruct.List);
                for (var i = 0; assignList != 0 && i < MaxDosListEntries; i++)
                {
                    if (locks.TryGetValue(Ram.ReadLong(assignList + AssignListStruct.Lock), out var listLock))
                    {
                        paths.Add(listLock.Path.FullPath);
                    }

                    assignList = Ram.ReadLong(assignList + AssignListStruct.Next);
                }

                if (paths.Count == 0)
                {
                    SetIoErr(DosErrors.InvalidLock);
                    return false;
                }

                foreach (var path in paths)
                {
                    volumes.AddAssign(name, path);
                }

                break;
            case DltLate:
            case DltNonBinding:
                var assignName = Ram.ReadCString(Ram.ReadLong(entry + DosListStruct.AssignName));
                if (string.IsNullOrEmpty(assignName))
                {
                    SetIoErr(DosErrors.ObjectNotFound);
                    return false;
                }

                volumes.SetAssign(name, assignName, type == DltLate ? AssignType.Late : AssignType.NonBinding);
                break;
            default:
                SetIoErr(DosErrors.ActionNotKnown);
                return false;
        }

        // entry is owned by dos list now and the dos list shows an entry built from the assign
        programDosEntries.Remove(entry);
        RebuildDosList();
        return true;
    }

    /// <summary>
    /// Remove assign entry from dos list. Entry and its locks are owned by caller after removal.
    /// </summary>
    private bool RemDosEntry(uint entry)
    {
        if (!dosListEntries.TryGetValue(entry, out var dosListEntry))
        {
            SetIoErr(DosErrors.ObjectNotFound);
            return false;
        }

        if (TypeFlag((int)Ram.ReadLong(entry + DosListStruct.Type)) != LdfAssigns)
        {
            SetIoErr(DosErrors.ObjectInUse);
            return false;
        }

        volumes.RemoveAssign(DosEntryName(entry));
        dosListEntries.Remove(entry);
        programDosEntries[entry] = dosListEntry.NameAddress;
        Ram.WriteLong(entry + DosListStruct.Next, 0);
        RebuildDosList();
        return true;
    }

    /// <summary>
    /// Allocate dos list entry with name as zero terminated BCPL string like MakeDosEntry().
    /// </summary>
    private (uint Address, uint NameAddress) AllocateDosEntry(string name, int type)
    {
        var bytes = Ram.GetBytes(name ?? string.Empty);
        var length = Math.Min(bytes.Length, 255);
        var address = Context.Allocator.Allocate(DosListStruct.Size);
        var nameAddress = address == 0 ? 0 : Context.Allocator.Allocate((uint)length + 2);
        if (nameAddress == 0)
        {
            if (address != 0)
            {
                Context.Allocator.Free(address);
            }

            return (0, 0);
        }

        Ram.Fill(address, DosListStruct.Size);
        Ram.WriteByte(nameAddress, (byte)length);
        Ram.WriteBytes(nameAddress + 1, bytes, 0, length);
        Ram.WriteByte(nameAddress + 1 + (uint)length, 0);
        Ram.WriteLong(address + DosListStruct.Type, (uint)type);
        Ram.WriteLong(address + DosListStruct.Name, MkBAddr(nameAddress));
        return (address, nameAddress);
    }

    /// <summary>
    /// Rebuild dos list in memory from volumes and assigns, freeing assign entries and locks of previous dos list.
    /// </summary>
    private void RebuildDosList()
    {
        if (dosInfo == 0)
        {
            return;
        }

        // volumes don't change, so volume entries are created once
        foreach (var name in volumes.VolumeNames.Where(x => !volumeEntries.ContainsKey(x)))
        {
            var volumeEntry = CreateVolumeEntry(name);
            if (volumeEntry != 0)
            {
                volumeEntries[name] = volumeEntry;
            }
        }

        foreach (var pair in dosListEntries.Where(x => !volumeEntries.ContainsValue(x.Key)).ToList())
        {
            // locks unlocked by program may have been reused for another lock
            foreach (var (bptr, dosLock) in pair.Value.Locks)
            {
                if (locks.TryGetValue(bptr, out var current) && ReferenceEquals(current, dosLock))
                {
                    FreeLock(bptr);
                }
            }

            foreach (var allocation in pair.Value.Allocations)
            {
                Context.Allocator.Free(allocation);
            }

            Context.Allocator.Free(pair.Value.NameAddress);
            Context.Allocator.Free(pair.Key);
            dosListEntries.Remove(pair.Key);
        }

        var link = DosListHead;
        Ram.WriteLong(link, 0);
        var entries = volumeEntries.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Select(x => x.Value)
            .Concat(volumes.AssignNames.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Select(CreateAssignEntry))
            .ToList();
        foreach (var entry in entries.Where(x => x != 0))
        {
            Ram.WriteLong(link, MkBAddr(entry));
            link = entry + DosListStruct.Next;
        }
    }

    private uint AddDosListEntry(string name, int type, out DosListEntry dosListEntry)
    {
        var (address, nameAddress) = AllocateDosEntry(name, type);
        if (address == 0)
        {
            dosListEntry = null;
            return 0;
        }

        dosListEntry = new DosListEntry { NameAddress = nameAddress };
        dosListEntries[address] = dosListEntry;
        return address;
    }

    private uint CreateVolumeEntry(string name)
    {
        var address = AddDosListEntry(name, DltVolume, out _);
        if (address == 0)
        {
            return 0;
        }

        var root = volumes.GetRoot(name);
        Ram.WriteLong(address + DosListStruct.Task, dummyPort);
        WriteDateStamp(address + DosListStruct.VolumeDate, volumes.GetInfo(root)?.Date ?? DateTime.Now);
        Ram.WriteLong(address + DosListStruct.DiskType, IdDosDisk);
        return address;
    }

    private uint CreateAssignEntry(string name)
    {
        var type = volumes.GetAssignType(name);
        var paths = volumes.GetAssignPaths(name);

        if (type == AssignType.Lock)
        {
            var targets = paths.Select(x => volumes.Resolve(x, null)).Where(x => x != null && Exists(x)).ToList();
            if (targets.Count > 0)
            {
                return CreateDirectoryAssignEntry(name, targets);
            }

            // assign to directories, which don't exist yet, are shown as late binding assign
            type = AssignType.Late;
        }

        var address = AddDosListEntry(name, type == AssignType.Late ? DltLate : DltNonBinding, out var entry);
        if (address == 0)
        {
            return 0;
        }

        var assignName = AllocateString(paths.FirstOrDefault() ?? string.Empty);
        entry.Allocations.Add(assignName);
        Ram.WriteLong(address + DosListStruct.AssignName, assignName);
        return address;
    }

    private uint CreateDirectoryAssignEntry(string name, IReadOnlyList<AmigaPath> targets)
    {
        var address = AddDosListEntry(name, DltDirectory, out var entry);
        if (address == 0)
        {
            return 0;
        }

        Ram.WriteLong(address + DosListStruct.Task, dummyPort);
        Ram.WriteLong(address + DosListStruct.Lock, CreateDosListLock(entry, targets[0]));

        // multi assign directories after first are in assign list
        var link = address + DosListStruct.List;
        foreach (var target in targets.Skip(1))
        {
            var node = Context.Allocator.Allocate(AssignListStruct.Size);
            if (node == 0)
            {
                break;
            }

            Ram.Fill(node, AssignListStruct.Size);
            entry.Allocations.Add(node);
            Ram.WriteLong(node + AssignListStruct.Lock, CreateDosListLock(entry, target));
            Ram.WriteLong(link, node);
            link = node + AssignListStruct.Next;
        }

        return address;
    }

    /// <summary>
    /// Get BCPL pointer to volume entry in dos list used for fl_Volume of locks.
    /// </summary>
    private uint VolumeEntryBptr(string volume) =>
        volume != null && volumeEntries.TryGetValue(volume, out var entry) ? MkBAddr(entry) : 0;

    private uint CreateDosListLock(DosListEntry entry, AmigaPath path)
    {
        var bptr = CreateLock(path, SharedLock);
        if (bptr != 0)
        {
            entry.Locks.Add((bptr, locks[bptr]));
        }

        return bptr;
    }

    /// <summary>
    /// Get device proc for name. Passing previous device proc gets next directory of a multi assign and frees
    /// previous device proc, when there are no more directories.
    /// </summary>
    private uint GetDeviceProc(string name, uint devProc)
    {
        if (devProc != 0)
        {
            if (!deviceProcs.TryGetValue(devProc, out var previous) ||
                previous.Index + 1 >= previous.Targets.Count)
            {
                FreeDeviceProc(devProc);
                SetIoErr(DosErrors.NoMoreEntries);
                return 0;
            }

            previous.Index++;
            SetDeviceProcLock(devProc, previous);
            return devProc;
        }

        name ??= string.Empty;
        var path = ResolveOrFail(name);
        if (path == null)
        {
            return 0;
        }

        devProc = Context.Allocator.Allocate(DevProcStruct.Size);
        if (devProc == 0)
        {
            SetIoErr(DosErrors.NoFreeStore);
            return 0;
        }

        Ram.Fill(devProc, DevProcStruct.Size);
        Ram.WriteLong(devProc + DevProcStruct.Port, dummyPort);

        var colon = name.IndexOf(':');
        var device = colon > 0 ? name.Substring(0, colon) : null;
        if (device != null && volumes.HasAssign(device))
        {
            var targets = volumes.GetAssignPaths(device).Select(x => volumes.Resolve(x, CurrentDirectory))
                .Where(x => x != null).ToList();
            var directories = targets.Where(IsDirectory).ToList();
            var state = new DeviceProcState { Targets = directories.Count > 0 ? directories : targets };
            if (state.Targets.Count > 0)
            {
                deviceProcs[devProc] = state;
                SetDeviceProcLock(devProc, state);
            }
        }
        else if (colon < 0)
        {
            // relative names use current directory lock, which isn't unlocked by FreeDeviceProc()
            Ram.WriteLong(devProc + DevProcStruct.Lock, Ram.ReadLong(Process + ProcessStruct.CurrentDir));
        }

        Ram.WriteLong(devProc + DevProcStruct.DevNode,
            FindDosEntry(DosListHead, device ?? path.Volume, LdfVolumes | LdfAssigns));
        return devProc;
    }

    private void SetDeviceProcLock(uint devProc, DeviceProcState state)
    {
        UnlockDeviceProc(devProc);
        var target = state.Targets[state.Index];
        var lockBptr = Exists(target) ? CreateLock(target, SharedLock) : 0;
        Ram.WriteLong(devProc + DevProcStruct.Lock, lockBptr);
        Ram.WriteLong(devProc + DevProcStruct.Flags, DvpfAssign | (lockBptr != 0 ? DvpfUnlock : 0));
    }

    private void UnlockDeviceProc(uint devProc)
    {
        if ((Ram.ReadLong(devProc + DevProcStruct.Flags) & DvpfUnlock) != 0)
        {
            FreeLock(Ram.ReadLong(devProc + DevProcStruct.Lock));
        }

        Ram.WriteLong(devProc + DevProcStruct.Lock, 0);
        Ram.WriteLong(devProc + DevProcStruct.Flags, 0);
    }

    private void FreeDeviceProc(uint devProc)
    {
        if (devProc == 0)
        {
            return;
        }

        UnlockDeviceProc(devProc);
        deviceProcs.Remove(devProc);
        Context.Allocator.Free(devProc);
    }
}
