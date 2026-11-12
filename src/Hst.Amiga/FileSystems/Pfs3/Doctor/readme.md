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
