# Fast File System

Fast File System directory contains classes to read and write partitions with Fast File System (FFS). Fast File System is the most common file system used by Amiga computers as it comes part of Amiga OS.

The code is inspired by ADFLib http://lclevy.free.fr/adflib/adf_info.html, https://github.com/lclevy/ADFlib by Laurent Clévy and Wikipedia page about Fast File System https://en.wikipedia.org/wiki/Amiga_Fast_File_System.

## Blocks

See [Blocks](Blocks) page for details about blocks used by Fast File System.

## DOS types

FFS operates in several modes, defined by "dostypes". AmigaOS filesystems are identified by a four letter descriptor which is specified either in the RDB or a mountlist or dosdriver; alternatively (as was the case in trackdisk-like devices like floppy disks), the disk itself could be formatted in any dostype specified.

FFS can be set to following dostypes:

| Dostype | Dostype hex | Mode                                                       | Max filename length | Description                                                                                                                                                                             |
|---------|-------------|------------------------------------------------------------|---------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| DOS\0   | 0x444F5300  | Old file system (OFS)                                      | 31                  | The original Amiga filesystem (OFS).                                                                                                                                                    |
| DOS\1   | 0x444F5301  | Fast file system (FFS)                                     | 31                  | The new filesystem (FFS).                                                                                                                                                               |
| DOS\2   | 0x444F5302  | International mode (OFS-INTL)                              | 31                  | International mode for OFS to handle filenames with "international characters" - i.e. those not found in English (Latin character set), such as ä and ê.                                |
| DOS\3   | 0x444F5303  | Fast file system + International mode (FFS-INTL)           | 31                  | International mode for FFS (FFS-INTL). This was the most commonly used FFS mode. (All higher dostypes have international mode always enabled).                                          |
| DOS\4   | 0x444F5304  | International mode + Dir cache (OFS-DC)                    | 31                  | Directory Cache (OFS-DC) mode enables a primitive cache by creating dedicated directory lists instead of reading the linked directory/file entries that can be scattered over the disk. |
| DOS\5   | 0x444F5305  | Fast file system + International mode + Dir cache (FFS-DC) | 31                  | Both dircache modes were not backwards compatible with earlier versions of FFS.                                                                                                         |
| DOS\6   | 0x444F5306  | Old file system + Long Filename (OFS-LNFS)                 | 107                 | Long filenames for OFS (OFS-LNFS). Requires AmigaOS 3.1.4+, FastFileSystem v46+ and Workbench prefs maximum filename length set to 101.                                                 |
| DOS\7   | 0x444F5307  | Fast file system + Long Filename (FFS-LNFS)                | 107                 | Long filenames for FFS (FFS-LNFS). Requires AmigaOS 3.1.4+, FastFileSystem v46+ and Workbench prefs maximum filename length set to 101.                                                 |

## Usage

The usage section describes how to use classes for reading and writing PFS3 partitions.

### Formatting a partition with Fast File System

Example of quick formatting a partition block in a stream with Fast File System and volume name "Workbench":
```
await FastFileSystemFormatter.FormatPartition(stream, partitionBlock, "Workbench");
```

### Mounting a Fast File System volume

First the Fast File System volume has to be mounted using FastFileSystemVolume class. Mounted Fast File System volume has current directory set to root directory by default. Fast File System volume implements disposable and will automatically unmount with a using statement.

Example of mounting a Fast File System volume from a partition block in a stream:
```
var ffsVolume = await ffsVolume.Mount(stream, partitionBlock);
```

### List entries

Example of listing entries from current directory:
```
var entries = await ffsVolume.ListEntries();
```

### Change directory

Example of changing current directory to relative path from current directory:
```
await ffsVolume.ChangeDirectory("New Dir");
```

Example of changing current directory to absolute path from root directory:
```
await ffsVolume.ChangeDirectory("/New Dir");
```

### Creating a directory

Example of creating a directory in current directory:
```
await ffsVolume.CreateDirectory("New Dir");
```

### Creating a file

Example of creating a file in current directory:
```
await ffsVolume.CreateFile("New File");
```

### Opening a file

Opening a file returns a stream for read and write data to and from files.

Example of opening a file in current directory:
```
var stream = await ffsVolume.OpenFile("New File", false);
```

### Reading data from a file

Reading data from a file is done using the stream returned from opening a file.

Example of reading data from a file in current directory:
```
await using (var entryStream = await ffsVolume.OpenFile("New File", false))
{
    var buffer = new byte[entryStream.Length];
    var bytesRead = await entryStream.ReadAsync(buffer, 0, buffer.Length);
}
```

### Writing data to a file

Writing data to a file is done using the stream returned from opening a file.

Example of writing data to a file in current directory:
```
var buffer = AmigaTextHelper.GetBytes("New file with some text.");
await using (var entryStream = await ffsVolume.OpenFile("New File", true))
{
    await entryStream.WriteAsync(buffer, 0, buffer.Length);
}
```

### Seek to a position in file

Seek to a position in a file can be used to change position with in the file to read or write.

Example of seeking to a position in file in current directory:
```
await using (var entryStream = await ffsVolume.OpenFile("New File", false))
{
    entryStream.Seek(10, SeekOrigin.Begin);
}
```

### Deleting a file or directory

Example of deleting a file in current directory:
```
await ffsVolume.Delete("New File");
```

Example of deleting a directory in current directory:
```
await ffsVolume.Delete("New Dir");
```

### Rename a file or directory

Example of renaming a file in current directory:
```
await ffsVolume.Rename("New File", "Renamed File");
```

### Move a file or directory

Example of moving a file from current directory to a sub directory:
```
await ffsVolume.Rename("New File", "New Dir/Moved File");
```

### Set comment for a file

Example of setting a comment for a file from current directory:
```
await ffsVolume.SetComment("New File", "Comment for file");
```

### Set protection bits for a file

Example of setting read and write protection bits for a file from current directory:
```
await ffsVolume.SetProtectionBits("New File", ProtectionBits.Write | ProtectionBits.Read);
```

### Set date for a file

Example of setting date for a file from current directory:
```
await ffsVolume.SetDate("New File", DateTime.Now.AddDays(-10));
```