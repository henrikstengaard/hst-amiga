# Professional File System 3

PFS3 directory contains classes to read and write partitions with Professional File System 3 (PFS3).

The code is based on pfs3aio (https://github.com/tonioni/pfs3aio) by Toni Wilen and is almost identical to it's C code with exceptions of structs, unions and moving pointers.

# Usage

The usage section describes how to use classes for reading and writing PFS3 partitions.

## Mounting

First the PFS3 volume has to be mounted using Pfs3Volume class. Mounted Pfs3 volume has current directory set to root directory.

**Due to a limitation in the current implementation, pfs3 volume has to be unmounted and mounted between each write operation.**

Example of mounting a PFS3 volume from a partition block in a stream:
```
var pfs3Volume = await Pfs3Volume.Mount(stream, partitionBlock);
```

## List entries

Example of listing entries from current directory using a mounted PFS3 volume:
```
var entries = await pfs3Volume.GetEntries();
```

## Creating a directory

Example of creating a directory in current directory using a mounted PFS3 volume:
```
await pfs3Volume.CreateDirectory("New Dir");
```

## Creating a file

Example of creating a file in current directory using a mounted PFS3 volume:
```
await pfs3Volume.CreateFile("New File");
```