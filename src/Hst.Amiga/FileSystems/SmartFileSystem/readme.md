# Smart File System

Smart File System directory contains classes to read, write and format partitions with Smart File System (SFS). Smart File System is a journaling file system for Amiga computers originally written by John Hendrikx and later maintained by Ralph Schmidt, Joerg Strohmayer and the AROS development team.

The code is based on Smart File System source code from following links:
- https://github.com/henrikstengaard/smartfilesystem-src Smart File System 1.84 source code converted from SourceForge CVS repository.
- https://github.com/aros-development-team/AROS/tree/master/rom/filesys/SFS AROS Smart File System source code.
- https://aminet.net/package/disk/misc/SFS Smart File System 1.279 for AmigaOS 3.x and AmigaOS 4 by Joerg Strohmayer, includes headers for on-disk structures.

Newer versions of Smart File System 1.84, AROS Smart File System and Smart File System 1.279 use the same on-disk structures for DOS type SFS\0 (structure version 3).

## Blocks

See [Blocks](Blocks) page for details about blocks used by Smart File System.

## DOS types

| Dostype | Dostype hex | Supported | Description                                                                                                                                                       |
|---------|-------------|-----------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| SFS\0   | 0x53465300  | Yes       | Smart File System. Max file size is 4 GB - 2 bytes and max partition size is 128 GB.                                                                              |
| SFS\2   | 0x53465302  | No        | Smart File System with large file support introduced in Smart File System 1.274. Closed source and on-disk structures are not documented. Not compatible with SFS\0. |

## Limitations

- Changes are written directly to blocks without using transactions. Pending transactions from an interrupted write by Smart File System are reapplied when mounting a volume. If the stream is read only, reapplied blocks are kept in memory.
- Deleted files are deleted permanently and not moved to recycled directory.
- Hard links are not supported by Smart File System. Create link creates a soft link.
- Names can be max 100 characters, comments max 79 characters and volume name max 30 characters.

## Usage

The usage section describes how to use classes for reading and writing Smart File System partitions.

### Formatting a partition with Smart File System

Example of formatting a partition block in a stream with Smart File System and volume name "Workbench":
```
await SmartFileSystemFormatter.FormatPartition(stream, partitionBlock, "Workbench");
```

Example of formatting a partition block in a stream with Smart File System with case sensitive names and without recycled directory:
```
await SmartFileSystemFormatter.FormatPartition(stream, partitionBlock, "Workbench", caseSensitive: true, recycled: false);
```

### Mounting a Smart File System volume

First the Smart File System volume has to be mounted using SmartFileSystemVolume class. Mounted Smart File System volume has current directory set to root directory by default. Smart File System volume implements disposable and will flush changes with a using statement.

Example of mounting a Smart File System volume from a partition block in a stream:
```
await using var sfsVolume = await SmartFileSystemVolume.Mount(stream, partitionBlock);
```

### List entries

Example of listing entries from current directory:
```
var entries = await sfsVolume.ListEntries();
```

### Change directory

Example of changing current directory to relative path from current directory:
```
await sfsVolume.ChangeDirectory("New Dir");
```

Example of changing current directory to absolute path from root directory:
```
await sfsVolume.ChangeDirectory("/New Dir");
```

### Creating a directory

Example of creating a directory in current directory:
```
await sfsVolume.CreateDirectory("New Dir");
```

### Creating a file

Example of creating a file in current directory:
```
await sfsVolume.CreateFile("New File");
```

### Reading data from a file

Example of reading data from a file in current directory:
```
await using (var entryStream = await sfsVolume.OpenFile("New File", FileMode.Read))
{
    var buffer = new byte[entryStream.Length];
    var bytesRead = await entryStream.ReadAsync(buffer, 0, buffer.Length);
}
```

### Writing data to a file

Example of writing data to a file in current directory:
```
var buffer = AmigaTextHelper.GetBytes("New file with some text.");
await using (var entryStream = await sfsVolume.OpenFile("New File", FileMode.Write, overwrite: true))
{
    await entryStream.WriteAsync(buffer, 0, buffer.Length);
}
```

### Deleting a file or directory

Example of deleting a file in current directory:
```
await sfsVolume.Delete("New File");
```

### Rename or move a file or directory

Example of moving a file from current directory to a sub directory:
```
await sfsVolume.Rename("New File", "New Dir/Moved File");
```

### Creating a soft link

Example of creating a soft link in current directory to a file in a sub directory:
```
await sfsVolume.CreateLink("Link", "New Dir/Moved File");
```

### Set comment, protection bits and date

```
await sfsVolume.SetComment("New File", "Comment for file");
await sfsVolume.SetProtectionBits("New File", ProtectionBits.Write | ProtectionBits.Read);
await sfsVolume.SetDate("New File", DateTime.Now.AddDays(-10));
```
