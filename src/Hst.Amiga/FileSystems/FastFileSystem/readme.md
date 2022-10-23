# Fast File System

Fast File System directory contains classes to read and write partitions with Fast File System (FFS). Fast File System is the most common file system used by Amiga computers as it comes part of Amiga OS.

The code is inspired by ADFLib http://lclevy.free.fr/adflib/adf_info.html, https://github.com/lclevy/ADFlib by Laurent Clévy and Wikipedia page about Fast File System https://en.wikipedia.org/wiki/Amiga_Fast_File_System.

## Blocks

See [Blocks](Blocks) page for details about blocks used by Fast File System.

## DOS types

| Dostype | des                                               |
|---------|---------------------------------------------------|
| DOS\0   | Old file system                                   |
| DOS\1   | Fast file system                                  |
| DOS\2   | International mode                                |
| DOS\3   | Fast file system + International mode             |
| DOS\4   | International mode + Dir cache                    |
| DOS\5   | Fast file system + International mode + Dir cache |

## Usage

The usage section describes how to use classes for reading and writing PFS3 partitions.

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