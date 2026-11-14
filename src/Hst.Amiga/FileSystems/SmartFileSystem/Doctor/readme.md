# Smart File System Doctor

Smart File System doctor checks, repairs and salvages Smart File System volumes with dos type SFS\0 (structure
version 3).

There is no open source Smart File System repair tool to port. The doctor is based on the Smart File System source
code and on what existing tools check:

- [AROS Smart File System source](https://github.com/aros-development-team/AROS/tree/master/rom/filesys/SFS)
  (LGPL): `SFScheck/SFScheck.c` is the read-only checker by John Hendrikx, the original author of Smart File
  System. It checks root blocks, admin space containers, node containers, object containers, extents and bitmap.
  `SFSfindobjects/SFSfindobjects.c` scans all blocks for object containers and is the basis for salvage.
  `FS/filesystemmain.c` (`readroots()`, `initdisk()`) and `FS/transactions.c` (`checkfortransaction()`) show which
  problems make Smart File System refuse to mount a volume.
- [SFSCheck 2.x](https://aminet.net/package/disk/misc/sfscheck) (closed source, MorphOS): rewritten checker, which
  also reports fragmentation and broken soft links.
- [SFSDoctor](https://aminet.net/package/disk/misc/sfsdoctor) (closed source, MorphOS): check, find lost files,
  check collisions, recover files and repair by rebuilding the directory tree.
- SFSsalv by Jörg Strohmayer (closed source): recovers files from damaged partitions.
- [Blocks](../Blocks) page for the on-disk structures.

## Usage

```c#
var volume = SmartFileSystemDoctor.OpenVolume(stream, partitionBlock);
volume.ShowMsg = message => Console.Write(message);
var doctor = await SmartFileSystemDoctor.Run(volume, Mode.Repair); // Check or Repair
// doctor.Issues, doctor.Stats, doctor.HasErrors, doctor.Aborted
```

Mode `Check` never writes to the stream. Mode `Repair` collects all changes in memory and writes the changed blocks
when the scan is done. Open a new volume for each run. Check and repair run the same scan, so check reports exactly
what repair fixes.

## What gets corrupted

Smart File System writes changes as transactions. A crash or reset during a write leaves an unfinished transaction,
which Smart File System replays when it mounts the volume. Most corruption comes from:

1. **Bad sectors or partially written blocks.** A checksum error in a metadata block. Smart File System requires
   both root blocks to be valid, so one bad root block makes the volume show up as not a DOS disk. A bad
   transaction block does the same.
2. **Stale objects after a crash.** Object containers and B-tree containers freed by Smart File System are not
   cleared. They keep valid checksums, so an object can exist in more than one block. SFSDoctor calls these
   "colliding" files: a real file and a dead file claiming the same blocks.
3. **Bitmap and admin space out of date.** Blocks used but marked free in the bitmap are overwritten by the next
   write. Leaked blocks only waste space. Smart File System silently fixes the cached number of free blocks in root
   info when it mounts a volume.
4. **Broken trees.** A bad container in the object node tree or extent B-tree makes objects or file data
   unreachable, even though the objects and extents themselves are intact.

## Checks and repairs

The scan runs in this order: root blocks, transaction, admin space containers, object node tree, extent B-tree,
directory tree, files, then the derived structures (object container chains, hash tables, soft links, object nodes,
extents, admin space bits and bitmap). The derived structures are rebuilt from the directory tree, so repairs like
removing an object or allocating a new object node update every structure that refers to it.

| Area | Check | Repair |
|------|-------|--------|
| Root blocks | Id, checksum, own block, version, block size, total blocks, partition size, structure pointers, both root blocks equal, reserved areas zero | Invalid root block is rebuilt from valid root block. If root blocks differ, the root block with most valid structure pointers is used. If both are invalid, the check is aborted |
| Transaction | Transaction block is a valid transaction ok or failure block. An unfinished transaction can be loaded | Unfinished transaction is replayed like Smart File System does when mounting. A transaction that can't be loaded is discarded. Transaction ok block is written |
| Admin space containers | Id, checksum, own block, previous, blocks per admin space, admin spaces inside partition and not overlapping | Chain is truncated at invalid container, invalid admin spaces are removed, previous and next are updated |
| Object node tree | Id, checksum, own block, node number, nodes and full bits of node containers. Object nodes point to the object container of the object | Object node tree is patched or, if its structure is invalid, rebuilt with reused and new node containers |
| Root object container | Id, own block and root directory object (checksum errors are rewritten) | Root object container is rebuilt: the first object container of the root directory is found by scanning admin spaces, volume name is set to "Recovered" |
| Object containers | Id, checksum, own block, parent, previous, objects readable, not empty, not used twice | Containers with only a checksum error are rewritten. Chain is truncated at invalid container, corrupt objects are removed, empty containers are removed, parent, previous and next are updated |
| Objects | Name (max 100 characters, no `:` or `/`), comment (max 79 characters), unique names, object node numbers valid and unique | Invalid names are fixed, comments truncated, duplicate names renamed, invalid or duplicate object nodes get a new object node |
| Hash tables | Id, checksum, own block, parent, all objects in the hash chain of their name, object node next and hash | Hash table is rebuilt in its block or a new block |
| Soft links | Id, checksum, own block and parent of soft link block | Soft link block with only a checksum error is rewritten, invalid soft link is removed |
| Extent B-tree | Id, checksum, own block, node size, keys sorted and inside range of parent | Extent B-tree is patched or rebuilt. Extents in unreachable leaf containers and leaf containers with checksum errors are used, if their previous matches the extent chain of a file |
| Files | Extents exist, have blocks, are inside partition and don't use metadata blocks. Chain doesn't loop. Size matches blocks in extents. Next and previous of extents | File is truncated at invalid extent, file size is set to size of extents, extents longer than file size are truncated, next and previous are updated |
| Cross linked files | Extents of files use the same blocks | The file owning the extents (previous matches its extent chain) is kept, otherwise the newest file is kept. Other files are removed |
| Admin space bits | Administration blocks used are marked used and inside an admin space, blocks marked used are used | Bits are updated, admin spaces are created for administration blocks outside admin spaces |
| Bitmap | Bitmap block id, checksum and own block, used blocks marked free, free blocks marked used, bits after last block | Bitmap is rebuilt from reserved blocks, root blocks, bitmap blocks, admin spaces and extents |
| Root info | Free blocks, deleted files and deleted blocks in recycled directory | Root info is updated |

Repairs follow these principles: a block with a wrong id or own block is not trusted, removing or truncating is
preferred over guessing, and the bitmap is always rebuilt last from the blocks that are in use. Administration
blocks needed by repair are allocated from free blocks in admin spaces. A new admin space is created, if all admin
spaces are full.

Run salvage before repairing a badly damaged volume to get as many files as possible out of it. Repair removes
objects in invalid object containers and truncates files with invalid extents, salvage reads what's left in those
blocks.

## Salvage

`SmartFileSystemDoctor.Salvage` scans a Smart File System volume and re-creates the directories and files it finds
in a target. Protection bits, comments and dates are preserved. The volume is never written to.

Salvage works like this:

1. A pending transaction is replayed in memory, so salvage sees the volume like Smart File System would after
   mounting it.
2. All blocks in the volume are scanned for valid object containers and extent B-tree leaf containers.
3. The directory tree is read from the root directory by following object container chains. If a chain is broken,
   all objects with the directory as parent are used. If root blocks or root object container are invalid, objects
   with the root directory as parent are used for the root directory. When the object node tree is valid, objects
   whose object node points to another object container are skipped, as they are stale objects in freed object
   containers. When the same name appears more than once, the newest object is kept.
4. Objects that can't be reached from the root directory are salvaged as orphans in `Lost+Found/Dir_<parent node>`,
   keeping the directory structure of orphaned directories. Set `SalvageOptions.SalvageOrphans` to false to skip
   them.
5. File data is read by following the extent chain. Extents from the extent B-tree are used first, then extents
   found by the scan with previous matching the extent chain of the file. A file whose data can't be fully read is
   written with the data that could be read, and is reported as partially salvaged.
6. Soft links are skipped and reported.

Salvage uses the same targets as the PFS3 and Fast File System doctors in `Hst.Amiga.FileSystems.Salvage`:

```c#
var volume = SmartFileSystemDoctor.OpenVolume(stream, partitionBlock);
var target = new DirectorySalvageTarget(@"c:\salvaged", UaeMetadata.UaeFsDb);
var result = await SmartFileSystemDoctor.Salvage(volume, target);
foreach (var message in result.Messages)
{
    Console.WriteLine(message);
}
```

Salvage to an existing, formatted PFS3, FFS or SFS partition in a rigid disk block image file. Don't use the same disk
as source and target.

```c#
await using var targetStream = File.Open(@"c:\target.hdf", FileMode.Open, FileAccess.ReadWrite);
using var target = await FileSystemVolumeSalvageTarget.MountPartition(targetStream, "DH0");
var result = await SmartFileSystemDoctor.Salvage(SmartFileSystemDoctor.OpenVolume(stream, partitionBlock), target);
```

## Source files

| File | Content |
|------|---------|
| SmartFileSystemDoctor.cs | `OpenVolume`, `Run`, block access, changed blocks and models |
| SmartFileSystemDoctor.Root.cs | Root blocks and transactions |
| SmartFileSystemDoctor.AdminSpaces.cs | Admin space containers, admin space bits and allocation of administration blocks |
| SmartFileSystemDoctor.Nodes.cs | Object node tree check, patch and rebuild |
| SmartFileSystemDoctor.Objects.cs | Directory tree, object containers, objects, hash tables and soft links |
| SmartFileSystemDoctor.Extents.cs | Extent B-tree check, patch and rebuild, files and cross linked files |
| SmartFileSystemDoctor.Bitmap.cs | Bitmap and root info |
| SmartFileSystemDoctor.Salvage.cs | Salvage |
| ExtentDataStream.cs | Stream reading file data from extents |
| DoctorVolume.cs | Volume geometry |
| Issue.cs, IssueType.cs, Mode.cs, Stats.cs | Results |

## Open decisions

### Cross linked files across doctors

Cross linked files are repaired differently by the PFS3, Fast File System and Smart File System doctors, and there is
no option to control it. Each doctor uses what its on-disk structures can tell about the owner of a block:

| Doctor | Repair of cross linked blocks | Owner of a block |
|--------|-------------------------------|------------------|
| Fast File System | The entry found first by the scan keeps the block. Later files are truncated, later entries truncate the hash chain | Usually unknown. Only OFS data blocks have a header key |
| PFS3 | Anode double allocation is reported and the bitmap is rebuilt. No files are removed | Anode chains belong to one file, so real cross links are rare |
| Smart File System | The file owning the extents (previous matches its extent chain) is kept, otherwise the newest file. Other files are removed | Previous of an extent points back to the owning file |

A common option like `CrossLinkPolicy` (keep owner, keep newest, keep first, truncate, report only) is not added
yet, as most values would have no effect for Fast File System (no owner) and PFS3 (no conflicting files). Salvage
never writes to the volume and salvages both cross linked files, so running salvage before repair keeps all data.

Choices to be made later:

- Should Smart File System truncate the other file at the first cross linked extent, like Fast File System, instead
  of removing it? This keeps the data of the other file up to the cross linked blocks.
- Should a report only option be added to all doctors, so repair fixes everything else and leaves cross linked files
  unchanged for manual inspection?

## Limitations

- SFS\2 is not supported, as its on-disk structures are not documented.
- Hard links are not supported by Smart File System. Objects with hard link bits are reported and left unchanged.
- The volume name can't be recovered, when the root object container is rebuilt.
- Root block first and last byte are not checked against the partition position, only the partition size, the same
  way as AROS Smart File System. This allows checking images of partitions.
- Soft links are counted, but their target path is not checked.
- Data blocks have no header, so file data can't be validated. Only extents are checked.
