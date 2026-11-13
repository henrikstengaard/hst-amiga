# Fast File System Doctor

Fast File System doctor checks, repairs and salvages Fast File System volumes with dos types DOS\0 to DOS\7
(OFS, FFS, international, dir cache and long file names).

There is no single open source Fast File System repair tool to port, so the doctor is based on the documented
Fast File System structure and on what the AmigaDOS disk validator and tools like DiskSalv do:

- [The .ADF (Amiga Disk File) format FAQ](http://lclevy.free.fr/adflib/adf_info.html) by Laurent Clévy: block
  layouts, checksums, hash function and bitmap.
- [ADFlib](https://github.com/lclevy/ADFlib) by Laurent Clévy: `adf_salv.c` for checking file blocks before
  recovering a file.
- [amitools](https://github.com/cnvogelg/amitools) `xdftool validate` (`amitools/fs/validate`): check categories
  for boot block, directory tree, files and bitmap.
- Linux kernel `fs/affs`: sanity checks done when mounting.
- [DCFS and LNFS low level data structures](https://wiki.amigaos.net/wiki/DCFS_and_LNFS_Low_Level_Data_Structures):
  dir cache and long file name blocks.

## Usage

```c#
var volume = FastFileSystemDoctor.OpenVolume(stream, partitionBlock); // or FastFileSystemDoctor.OpenAdf(stream)
volume.ShowMsg = message => Console.Write(message);
var doctor = await FastFileSystemDoctor.Run(volume, Mode.Repair); // Check or Repair
// doctor.Issues, doctor.Stats, doctor.HasErrors, doctor.Aborted
```

Mode `Check` never writes to the stream. Mode `Repair` collects all changes in memory and writes the changed
blocks when the scan is done. Open a new volume for each run.

The dos type is taken from the partition block. For adf files, or when the partition block has no dos type, the dos
type is read from the boot block. Set `DoctorVolume.DosType` to override it.

## What gets corrupted

Most common corruptions, in order:

1. **Volume not validated.** A write was interrupted by a reset, crash or power loss. The bitmap flag in the root
   block is not valid and the bitmap is out of date. AmigaDOS shows the volume as not validated. The fix is to walk
   the directory tree, rebuild the bitmap and set the bitmap flag. This alone fixes most broken FFS volumes.
2. **Checksum errors** on any block, from bad sectors or partially written blocks. AmigaDOS shows
   "Checksum error on disk block N".
3. **"Key already set".** A block is used by two entries (cross-linked), or the bitmap marks a used block as free.
4. **"Disk structure corrupt", "Key out of range" or "Not a DOS disk".** Pointers outside the volume, blocks with
   wrong type, or a destroyed root or boot block.

## Checks and repairs

The scan runs in this order: boot block, root block, directory tree (hash chains, entries, files and links),
hard links, dir caches, bitmap pointers and bitmap content. Blocks used by the directory tree are collected while
scanning, and the bitmap is compared with them at the end.

| Area | Check | Repair |
|------|-------|--------|
| Boot block | Dos type is a Fast File System dos type and matches the partition dos type | Dos type is set to partition dos type, checksum is updated if boot block was bootable |
| Root block | Type, header key, high seq, hash table size, secondary type and checksum | Checksum is updated if structure is valid. If structure is invalid, root block is rebuilt from entry blocks with root block as parent |
| Hash chains | Pointers are inside volume, point to valid entry blocks, don't loop and don't point to entries found elsewhere | Hash chain is truncated at the invalid pointer |
| Entry blocks | Type, header key (own block number), secondary type and checksum | Checksum is updated, if the entry is in the right directory and hash chain, otherwise hash chain is truncated |
| Entries | Parent points to directory, name is valid (not empty, not too long, no `:` or `/`), name is unique, name hash matches hash chain, date is valid | Parent is updated, invalid or duplicate names are renamed, entry is moved to right hash chain, invalid date parts are set to 0 |
| Files | High seq, data block pointers inside volume and not used by other entries, file extension blocks (type, header key, parent, secondary type, checksum) | File is truncated at first invalid data block pointer or file extension block. Data block pointers are rewritten |
| OFS data blocks | Type, header key, sequence number, data size, checksum and next data chain | File is truncated at first invalid data block, next data pointers are updated |
| File size | File size matches number of data blocks (OFS: sum of data sizes) | File size is updated, data blocks are never dropped |
| Hard links | Real entry is a valid file or directory found in directory tree, link is in link chain of real entry | Dangling hard link is removed, missing link is added to link chain |
| LNFS comment blocks | Type, own key, header key and checksum | Comment block pointer is cleared |
| Dir cache (DOS\4, DOS\5) | Dir cache blocks exist, are valid and records match entries in directory | Dir cache is rebuilt, valid dir cache blocks are reused |
| Bitmap | Bitmap flag, bitmap block and bitmap extension block pointers, bitmap block checksums, used blocks marked free, free blocks marked used | Bitmap is rebuilt from blocks used, invalid bitmap blocks are relocated to free blocks |

Repairs follow the same principles as the disk validator: a block with a wrong checksum or a wrong own block number
is not trusted, removing or truncating is preferred over guessing, and the bitmap is always rebuilt last from the
blocks that are in use.

Blocks of entries removed by repair, e.g. the rest of a truncated hash chain, are freed when the bitmap is rebuilt.
Run salvage before repairing a badly damaged volume to get as many files as possible out of it.

Rebuilding a destroyed root block uses all entry blocks with the root block as parent. Entry blocks of deleted
entries are still on disk, so deleted entries in the root directory can reappear after the root block is rebuilt.

## Salvage

`FastFileSystemDoctor.Salvage` scans a Fast File System volume and re-creates the directories and files it finds in a
target. Protection bits, comments and dates are preserved. The volume is never written to.

Salvage works like this:

1. All blocks in the volume are scanned for valid entry blocks (type, own block number, secondary type and
   checksum). For OFS, valid data blocks are also collected by file header and sequence number.
2. The directory tree is read from the root directory by following hash chains. If a hash chain is broken, all
   entry blocks with the directory as parent are used. If the root block is invalid, entry blocks with the root
   block as parent are used for the root directory. When the same name appears more than once, the newest entry is
   kept.
3. Entry blocks that can't be reached from the root directory are salvaged as orphans in
   `Lost+Found/Dir_<parent block>`, keeping the directory structure of orphaned directories. They can be old or
   deleted entries. Set `SalvageOptions.SalvageOrphans` to false to skip them.
4. File data is read by following data block pointers in the file header and file extension blocks. For OFS, data
   blocks found by the scan are used when data block pointers are broken, so OFS files can often be fully salvaged.
   A file whose data can't be fully read is written with the data that could be read, and is reported as
   partially salvaged.
5. Hard links to files are written as copies of the linked file. Soft links and hard links to directories are
   skipped and reported.

Salvage uses the same targets as PFS3 doctor salvage in `Hst.Amiga.FileSystems.Salvage`:

```c#
var volume = FastFileSystemDoctor.OpenVolume(stream, partitionBlock);
var target = new DirectorySalvageTarget(@"c:\salvaged", UaeMetadata.UaeFsDb);
var result = await FastFileSystemDoctor.Salvage(volume, target);
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
var result = await FastFileSystemDoctor.Salvage(FastFileSystemDoctor.OpenVolume(stream, partitionBlock), target);
```

## Source files

| File | Content |
|------|---------|
| FastFileSystemDoctor.cs | `OpenVolume`, `OpenAdf`, `Run`, block access, changed blocks and field helpers |
| FastFileSystemDoctor.Scan.cs | Boot block, root block (incl. rebuild) and directory tree |
| FastFileSystemDoctor.File.cs | Files, file extension blocks, OFS data blocks and hard links |
| FastFileSystemDoctor.DirCache.cs | Dir cache check and rebuild |
| FastFileSystemDoctor.Bitmap.cs | Bitmap check, rebuild and block allocation |
| FastFileSystemDoctor.Salvage.cs | Salvage |
| DataBlockStream.cs | Stream reading file data from data blocks |
| DoctorVolume.cs | Volume geometry |
| Issue.cs, IssueType.cs, Mode.cs, Stats.cs | Results |

## Limitations

- Bitmap blocks are counted from the number of blocks in the volume, the same way as the formatter in this library.
- Soft links are counted, but their target path is not checked.
- Data in FFS data blocks can't be validated, since FFS data blocks have no header. Only pointers are checked.
