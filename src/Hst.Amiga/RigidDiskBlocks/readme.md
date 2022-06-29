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