# File systems

## Mounting a partition only once

FFS, PFS3 and SFS volumes cache blocks and write some changes later, when the volume is flushed or disposed.
Two volumes mounted on the same partition in the same stream would read stale blocks and overwrite each others
changes, which corrupts the file system.

Mounting a partition, which overlaps a partition already mounted by another volume on the same stream, throws a
`PartitionAlreadyMountedException`. A mounted partition is released when its volume is disposed, so volumes must be
disposed before the partition is mounted again:
```
await using (var volume = await FileSystemVolumeFactory.MountPartition(stream, partitionBlock))
{
    await volume.CreateDirectory("New Dir");
}

// partition can be mounted again, as volume is disposed
await using var volume2 = await FileSystemVolumeFactory.MountPartition(stream, partitionBlock);
```

The check can be skipped with the `force` parameter available on `FileSystemVolumeFactory.MountPartition`,
`FastFileSystemVolume.Mount`, `FastFileSystemVolume.MountPartition`, `FastFileSystemVolume.MountAdf`,
`Pfs3Volume.Mount` and `SmartFileSystemVolume.Mount`. Only use force when the other volume isn't writing,
e.g. to read a partition while another volume is mounted on it:
```
var volume = await FileSystemVolumeFactory.MountPartition(stream, partitionBlock, force: true);
```

The check has following limitations:
- Only volumes mounted on the same stream instance are checked. Two streams opened on the same image file, e.g.
  two `FileStream` instances, are not detected.
- Volumes mounted from read only streams (`CanWrite` is false) are not checked, as they can't write blocks.
- A volume, which is not disposed, keeps the partition mounted until the stream is garbage collected.

## Protection bits

Files in Amiga file systems have a series of protection bits which controls their attributes. 

The protection bits are represented by letters:
- `H` The file should be held resident in memory after it has been used.
- `S` The file is a script.
- `P` The file is a pure command and can be made resident.
- `A` The file has been archived.
- `R` The file can be read.
- `W` The file can be written to (altered).
- `E` The file is executable (a program).
- `D` The file can be deleted.

Protection bits are represented by following bits:

| Bit | Protection    |
|-----|---------------|
| 128 | Held Resident |
| 64  | Script        |
| 32  | Pure          |
| 16  | Archive       |
| 8   | Read          |
| 4   | Write         |
| 2   | Execute       |
| 1   | Delete        |

By default a file has Read, Write, Execute and Delete protection bits when set to 0.

Protection bit examples:

| Value | Bits       | Attributes |
|-------|------------|------------|
| 0     | `00000000` | `----RWED` |
| 1     | `00000001` | `----RWE-` |
| 4     | `00000100` | `----R-ED` |
| 6     | `00000110` | `----R--D` |
| 64    | `01000000` | `-S--RWED` |


