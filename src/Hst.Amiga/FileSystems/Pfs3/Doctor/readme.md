# PFS3 Doctor

C# port of PFSDoctor from https://github.com/tonioni/pfsdoctor.

## Usage

```csharp
var volume = Pfs3Doctor.OpenVolume(stream, partitionBlock);
volume.ShowMsg = message => Console.Write(message);
var doctor = await Pfs3Doctor.Run(volume, Mode.Repair); // Check, Info, Repair or Unformat
// doctor.Result, doctor.Stats
```

Mode `Check` never writes to the stream. Open a new volume for each run, because a scan can change volume geometry, e.g. when repartitioning.

## Source mapping

| Original       | C#                                                        |
|----------------|-----------------------------------------------------------|
| console.c      | Pfs3Doctor.cs (`OpenVolume`, `Run`, `CloseVolume`)          |
| access.c       | Pfs3Doctor.Access.cs                                      |
| device.c       | Pfs3Doctor.Device.cs                                      |
| fullscan.c     | Pfs3Doctor.FullScan.cs                                    |
| standardscan.c | Pfs3Doctor.StandardScan.cs                                |
| stats.c        | Pfs3Doctor.Stats.cs                                       |
| doctor.h       | Volume, Cache, CacheLine, CachedBlock, CAnode, Bitmap, ScanElement, Stats, Error, Mode, StandardScanFlags, DoctorConstants |
| pfs3.h         | RootBlock, ExtensionBlock, BootBlock, BitmapBlock, IndexBlock, AnodeBlock, DirBlock, DirEntry, DelDirBlock, ExtraFields |

## Restore displaced root block

`Pfs3Doctor.RestoreRootBlock` is an addition that isn't part of the original PFSDoctor. It restores the boot and root
blocks of a volume when they have been displaced: a copy was written at the wrong offset, while the original blocks
at the volume start were destroyed. It searches the sectors around the volume start for a valid root block that has
a boot block before it, a disk size matching the partition, and a root block extension at the expected position
relative to the volume start. It then copies the boot block and the root block cluster back to the volume start.
With `write: false` the displaced blocks are only searched for and reported.

```c#
var result = await Pfs3Doctor.RestoreRootBlock(stream, partitionBlock);
foreach (var message in result.Messages)
{
    Console.WriteLine(message);
}
```

## Salvage

`Pfs3Doctor.Salvage` is an addition that isn't part of the original PFSDoctor. It scans a PFS3 volume and re-creates
the directories and files it finds in a target. Protection bits, comments and dates are preserved. The volume is
never written to.

Salvage works like this:

1. The root block is read to get the reserved block size and the reserved area. If the root block is not valid,
   the reserved block size is calculated from the volume size, and the end of the reserved area is searched for.
2. The reserved area is scanned for anode and directory blocks. For each anode block sequence number, the newest
   anode block is used. Anode blocks newer than the root block are only used when no other block has the same
   sequence number, so blocks left over from a previous format are not preferred.
3. The directory tree is read from the root directory by following anode chains. If a directory's anode chain is
   broken, all directory blocks found for that directory are used. When the same name appears more than once, the
   entry from the newest block is kept.
4. Directory blocks that can't be reached from the root directory are salvaged as orphaned directories in
   `Lost+Found/Dir_<anode>`. They can be old or deleted directories. Set `SalvageOptions.SalvageOrphans` to false
   to skip them.
5. Directories and files are written to the target. Hard links to files are written as copies of the linked file.
   Soft links and hard links to directories are skipped and reported. Rollover files are written as regular files.
   A file whose data can't be fully read is written with the data that could be read, and is reported as
   partially salvaged.

Salvage to a directory with UAE metadata, either UAEFSDB (`UaeMetadata.UaeFsDb`) or UAE metafiles
(`UaeMetadata.UaeMetafile`). Amiga names, protection bits and comments are stored in UAE metadata. Dates are set as
the last write time of directories and files.

```c#
var volume = Pfs3Doctor.OpenVolume(stream, partitionBlock);
var target = new DirectorySalvageTarget(@"c:\salvaged", UaeMetadata.UaeFsDb);
var result = await Pfs3Doctor.Salvage(volume, target);
foreach (var message in result.Messages)
{
    Console.WriteLine(message);
}
```

Salvage to an existing, formatted PFS3 or FFS partition in a rigid disk block image file. Don't use the same disk
as source and target.

```c#
await using var targetStream = File.Open(@"c:\target.hdf", FileMode.Open, FileAccess.ReadWrite);
using var target = await FileSystemVolumeSalvageTarget.MountPartition(targetStream, "DH0");
var result = await Pfs3Doctor.Salvage(Pfs3Doctor.OpenVolume(stream, partitionBlock), target);
```

Salvage targets are in namespace `Hst.Amiga.FileSystems.Salvage` and are shared with Fast File System doctor salvage. To salvage to another target, implement `ISalvageTarget`.

## Naming

- Function names keep the original names in PascalCase with underscores removed, e.g. `c_GetBlock` -> `CGetBlock`, `vol_GetBlock` -> `VolGetBlock`, `dd_CheckBlock` -> `DdCheckBlock`, `bg_InitBitmap` -> `BgInitBitmap`.
- `volume.getblock` and `volume.writeblock` are `VolGetBlock` and `VolWriteBlock`.
- Local variable names are kept, e.g. `rbl`, `rext`, `bloknr`, `blk`.
- Globals are fields of `Pfs3Doctor`.
- Error and mode values drop their prefix and use PascalCase, e.g. `e_block_outside_partition` -> `Error.BlockOutsidePartition`, `check` -> `Mode.Check`.
- Struct fields are PascalCase properties, e.g. `rbl->reserved_blksize` -> `rbl.ReservedBlksize`.

## Blocks

Blocks are kept as raw `byte[]`, the same as in the original. Block classes are views over the buffer that read and write big-endian fields in place, so corrupt blocks never throw and repairs edit the block directly. Arrays inside structs are methods that return a `UInt32Pointer` (replacing `uint32 *`), e.g. `rext.data->superindex[i]` -> `rext.ExtensionBlock.SuperIndex(i).Value`.

## Deviations from original

- I/O is async and goes to a `Stream`. TD64, NSD, direct SCSI and access mode detection are not ported.
- `askuser`, `showmsg`, `status`, `progress` and `updatestats` are delegates on `Volume`. `AskUser` returns true by default (non-interactive).
- ASL file requesters are replaced with `Volume.OpenMountFile`. If it returns null, no mountlist is written and the scan continues instead of retrying forever.
- CTRL-C is replaced with a `CancellationToken`.
- Added guards against null bitmaps, out-of-range reads, division by zero in `RepairAnodeBitmap`, a zero `rescluster` in `SearchFileSystem`, and a missing lower bound in `InitAnodeBitmap`.
- `RepairMainBitmap` uses a new buffer for each bitmap block. The original reused one buffer and overwrote the data of built bitmap blocks.
- `SetExtraFields` uses a 512 byte entry buffer, because `MAX_ENTRYSIZE` is smaller than a long filename entry.
- `BuildBootBlock` passes `bloknr` and `bytes` in the right order (they are swapped in the original, which never calls it).
- `UpdateCache` clears the dirty flag after a successful write.
- When the maximum number of passes is exceeded, `ExitStandardScan` still runs, so dirty cache lines get written.
- Info mode exits with `RblNotFound` when no valid rootblock is found.
