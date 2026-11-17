# Rigid disk blocks

Rigid disk blocks directory contains classes to read and write Amiga hard disks Rigid Disk Block (RDB). A RDB contains definitions of hard disk geometry, partitions, file systems and list of bad blocks. It similar to Master Boot Record (MBR)
 used by MS-DOS and older versions of Windows.

RDB uses following blocks:
- Rigid disk block: Root block with hard disk geometry and links to other blocks.
- Partition block: Block per partition with partition geometry, name, dos type, max transfer, etc.
- File system header block: Block per file system with dos type, version and link to load seg blocks. 
- Load seg block: Blocks per 492 bytes containing binary for file system.
- Bad block: Blocks with list of blocks with bad sectors to be ignored. None, if no bad blocks are present.

Each block has a reader and a writer to read and write blocks to/from a stream. 

## References

The Rigid disk block classes are written with inspiration from following references:
- http://lclevy.free.fr/adflib/adf_info.html by Laurent Clévy.
- http://amigadev.elowar.com/read/ADCD_2.1/Devices_Manual_guide/node0079.html from Amiga Developer Docs.

## Examples

Example of reading Rigid Disk Block from a file:

```c#

await using var hdfStream = File.OpenRead("4gb.hdf");
var rigidDiskBlock = RigidDiskBlockReader.Read(stream);

```

Example of creating a 4 GB .hdf file with PFS3AIO file system and 2 partitions using fluent extensions:

```c#
await 4.GB()
    .ToUniversalSize()
    .CreateRigidDiskBlock()
    .AddFileSystem("PDS3", await File.ReadAllBytesAsync(@"pfs3aio"))
    .AddPartition("DH0", 300.MB(), bootable: true)
    .AddPartition("DH1")
    .WriteToFile(@"4gb.hdf");

```
## Move and resize partitions

Partitions with Fast File System, PFS3 and Smart File System can be moved and resized with the mover and
resizer classes next to the doctor of each file system:

| File system        | Mover                   | Resizer                   |
|--------------------|-------------------------|---------------------------|
| Fast File System   | `FastFileSystemMover`   | `FastFileSystemResizer`   |
| PFS3               | `Pfs3Mover`             | `Pfs3Resizer`             |
| Smart File System  | `SmartFileSystemMover`  | `SmartFileSystemResizer`  |

Partitions are selected by partition number starting from 1. Cylinders are cylinders of the partition, the same
as low and high cylinder in the partition block.

**Mover** moves a partition to a new low cylinder, keeping the number of cylinders. The new location can overlap
the current location, e.g. a few cylinders back or forth, or be free cylinders anywhere within the rigid disk
block before or after the partition. Data is copied in an order that handles overlapping locations.

**Resizer** expands or shrinks the file system within the cylinders available before and after the partition:
from the end of the previous partition (or the first partitionable cylinder) to the start of the next partition
(or the last partitionable cylinder). The low cylinder can be changed as well to expand or shrink the partition
at the start. When shrinking, blocks used in the area cut off are relocated to free blocks. The resize fails, if
the used blocks don't fit in the new size.

Both mover and resizer:
- check the file system with the doctor before the operation and refuse to change a file system with errors.
- optionally expand or shrink the rigid disk block with `RigidDiskBlockCylinders`. The rigid disk block can be
  expanded up to the number of cylinders the disk (stream length) holds and shrunk down to the last cylinder used
  by partitions.
- write the rigid disk block and partition blocks back to the blocks they were read from, after the file system
  is changed.
- check the file system with the doctor after the operation. Errors found are returned in the result.
- support dry run, which validates and plans the operation without writing any changes.

Moving and resizing changes the disk in place and is not crash safe. Make a backup of the disk first and run
with `DryRun` to see if the operation is possible.

Example of moving second partition to cylinder 1200:

```c#
await using var stream = File.Open("4gb.hdf", FileMode.Open, FileAccess.ReadWrite);
var result = await FastFileSystemMover.Move(stream, 2, new PartitionMoveOptions { NewLowCyl = 1200 });
if (!result.Success)
{
    Console.WriteLine(string.Join(Environment.NewLine, result.Errors));
}
```

Example of expanding rigid disk block to the size of the disk and expanding last partition to the end of disk,
e.g. after copying a disk image to a larger CF card:

```c#
await using var stream = File.Open(@"\\.\PhysicalDrive2", FileMode.Open, FileAccess.ReadWrite);
var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
var cylinders = RigidDiskBlockResizer.GetMaxCylinders(rigidDiskBlock, stream.Length);
var partitionNumber = rigidDiskBlock.PartitionBlocks.Count();

var result = await Pfs3Resizer.Resize(stream, partitionNumber, new PartitionResizeOptions
{
    NewHighCyl = cylinders - 1,
    RigidDiskBlockCylinders = cylinders
});
```

Example of getting cylinders available for resizing a partition:

```c#
var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);
var partitionBlock = rigidDiskBlock.PartitionBlocks.ElementAt(1);
var available = PartitionGeometry.GetAvailableCylinders(rigidDiskBlock, partitionBlock);
Console.WriteLine($"Cylinders {available.LowCyl} to {available.HighCyl} are available");
```

The rigid disk block can also be resized without changing partitions with `RigidDiskBlockResizer.Resize`.

## Recovery

`RigidDiskBlockRecovery` recovers a broken Rigid Disk Block. It scans the disk for boot and root blocks of
known file systems (FFS/OFS, PFS3 and SFS) to find where each partition starts and how big it is, and it reads
the Rigid Disk Block leniently: checksums are ignored and the Rigid Disk Block area is scanned for blocks that
aren't linked.

Recovery modes:
- Fix: repairs an existing Rigid Disk Block by recalculating checksums, relinking blocks and correcting
  partitions (low and high cylinder, dos type, block size) from the file systems found.
  Partitions found on disk that have no partition block are added.
- Rebuild: builds a new Rigid Disk Block from the disk size and the file systems found. Partition blocks
  and file systems (FSHD/LSEG blocks) still intact in the Rigid Disk Block area are reused.
- Auto (default): fix if a Rigid Disk Block is found, otherwise rebuild.

`SkipFloppyImages` (enabled by default) skips file systems the size of a double density (1760 sectors) or high density
(3520 sectors) floppy disk. These are usually .adf floppy images stored as files inside a partition, and a newer
floppy image would otherwise win over the partition it is stored in. An FFS file system is also treated as
a floppy image when its root block points to bitmap block 881 or 1761, which catches .adf files stored
fragmented.

A PFS3 volume whose boot and root blocks have been displaced is detected by checking that the root block extension
sits at the expected position relative to the volume start. Displaced means a copy was written at the wrong offset
while the original blocks at the volume start were destroyed. The partition is then placed at the real volume start
and a warning is reported. The volume can't be mounted until its boot and root blocks are restored with
`Pfs3Doctor.RestoreRootBlock`.

`Analyze` is a dry run and never writes to the disk. `Repair` writes the changed sectors of the Rigid Disk Block
area, but only when recovery finished without errors.

Example of repairing the Rigid Disk Block in a hard file:

```c#
await using var stream = File.Open("4gb.hdf", FileMode.Open, FileAccess.ReadWrite);
var result = await RigidDiskBlockRecovery.Repair(stream, new RecoveryOptions
{
    Mode = RecoveryMode.Auto,
    Pfs3DosType = "PDS3"
});

foreach (var message in result.Messages)
{
    Console.WriteLine(message);
}
```

### Recovery order

To recover a disk with a broken Rigid Disk Block and damaged file systems, run these steps in order. Each
step depends on the one before it:

1. **Backup**: work on a copy of the disk or image. Steps 3, 5 and 7 write to it.
2. **Analyze**: run `RigidDiskBlockRecovery.Analyze`, a dry run. Check the partitions and messages:
   partition offsets, displaced PFS3 boot and root blocks, and file systems dropped from the Rigid Disk Block.
3. **Repair**: run `RigidDiskBlockRecovery.Repair`. Then read the Rigid Disk Block with
   `RigidDiskBlockReader.Read` to confirm checksums and links are valid.
4. **Re-add file systems**: file systems (FSHD/LSEG blocks) with broken load segments are dropped. Add the
   file system drivers again, e.g. pfs3aio. Otherwise the partitions can't be mounted from the Rigid Disk Block.
5. **Restore PFS3 boot and root blocks**: run `Pfs3Doctor.RestoreRootBlock` for each PFS3 partition, with
   `write: false` first, then `write: true`. It needs the partition block placed at the real volume start, so
   the Rigid Disk Block must be repaired first. It does nothing if the root block at the volume start is valid.
6. **Check PFS3 volumes**: run `Pfs3Doctor.Run` with `Mode.Check`. The doctor reads the root block at the
   volume start, so boot and root blocks must be restored first. This matters most for restored volumes: the
   displaced copy overwrote the blocks where it was written. Restoring rewrites only the boot block and root
   block cluster, not the blocks the copy overwrote.
7. **Repair PFS3 volumes**: run `Pfs3Doctor.Run` with `Mode.Repair` on volumes where the check found errors.
8. **Verify**: mount the volumes, list directories and read files.

Example of steps 2, 3 and 5 to 7:

```c#
await using var stream = File.Open("broken.img", FileMode.Open, FileAccess.ReadWrite);

// 2. analyze
var options = new RecoveryOptions { Mode = RecoveryMode.Auto };
var analyzeResult = await RigidDiskBlockRecovery.Analyze(stream, options);
foreach (var message in analyzeResult.Messages)
{
    Console.WriteLine(message);
}

// 3. repair and read rigid disk block strictly
var repairResult = await RigidDiskBlockRecovery.Repair(stream, options);
if (!repairResult.IsValid)
{
    return;
}

var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);

// 4. re-add file systems, e.g. pfs3aio, to rigid disk block here

foreach (var partitionBlock in rigidDiskBlock.PartitionBlocks.Where(x =>
             x.DosTypeFormatted.StartsWith("PDS") || x.DosTypeFormatted.StartsWith("PFS")))
{
    // 5. restore displaced boot and root blocks
    var restoreResult = await Pfs3Doctor.RestoreRootBlock(stream, partitionBlock, write: false);
    if (restoreResult.Found)
    {
        restoreResult = await Pfs3Doctor.RestoreRootBlock(stream, partitionBlock, write: true);
    }

    foreach (var message in restoreResult.Messages)
    {
        Console.WriteLine(message);
    }

    // 6. check volume, 7. repair volume if check found errors
    await Pfs3Doctor.Run(Pfs3Doctor.OpenVolume(stream, partitionBlock), Mode.Check, verbose: true);
    // await Pfs3Doctor.Run(Pfs3Doctor.OpenVolume(stream, partitionBlock), Mode.Repair, verbose: true);
}
```

.adf floppy images stored as files inside a volume are not recovered by these steps. Check them separately
after their volume checks clean.
