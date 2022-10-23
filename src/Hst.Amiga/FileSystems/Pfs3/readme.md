# Professional File System 3

PFS3 directory contains classes to read and write partitions with Professional File System 3 (PFS3). Professional File System 3 is a popular file system used by Amiga computers. 

PFS3 is originally developed by Michiel Pelt.

The code is based on pfs3aio (https://github.com/tonioni/pfs3aio) by Toni Wilen and is almost identical to it's C code with exceptions of structs used to read and write data, unions and moving pointers.

## Usage

The usage section describes how to use classes for reading and writing PFS3 partitions.

### Mounting a PFS3 volume

First the PFS3 volume has to be mounted using Pfs3Volume class. Mounted Pfs3 volume has current directory set to root directory by default. PFS3 volume implements disposable and will automatically unmount with a using statement.

Example of mounting a PFS3 volume from a partition block in a stream:
```
var pfs3Volume = await Pfs3Volume.Mount(stream, partitionBlock);
```

### List entries

Example of listing entries from current directory:
```
var entries = await pfs3Volume.ListEntries();
```

### Change directory

Example of changing current directory to relative path from current directory:
```
await pfs3Volume.ChangeDirectory("New Dir");
```

Example of changing current directory to absolute path from root directory:
```
await pfs3Volume.ChangeDirectory("/New Dir");
```

### Creating a directory

Example of creating a directory in current directory:
```
await pfs3Volume.CreateDirectory("New Dir");
```

### Creating a file

Example of creating a file in current directory:
```
await pfs3Volume.CreateFile("New File");
```

### Opening a file

Opening a file returns a stream for read and write data to and from files.

Example of opening a file in current directory:
```
var stream = await pfs3Volume.OpenFile("New File");
```

### Reading data from a file

Reading data from a file is done using the stream returned from opening a file.

Example of reading data from a file in current directory:
```
await using (var entryStream = await pfs3Volume.OpenFile("New File", false))
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
await using (var entryStream = await pfs3Volume.OpenFile("New File", true))
{
    await entryStream.WriteAsync(buffer, 0, buffer.Length);
}
```

### Seek to a position in file

Seek to a position in a file can be used to change position with in the file to read or write.

Example of seeking to a position in file in current directory:
```
await using (var entryStream = await pfs3Volume.OpenFile("New File", false))
{
    entryStream.Seek(10, SeekOrigin.Begin);
}

```

### Deleting a file or directory

Example of deleting a file in current directory:
```
await pfs3Volume.Delete("New File");
```

Example of deleting a directory in current directory:
```
await pfs3Volume.Delete("New Dir");
```

### Rename a file or directory

Example of renaming a file in current directory:
```
await pfs3Volume.Rename("New File", "Renamed File");
```

### Move a file or directory

Example of moving a file from current directory to a sub directory:
```
await pfs3Volume.Rename("New File", "New Dir/Moved File");
```

### Set comment for a file

Example of setting a comment for a file from current directory:
```
await pfs3Volume.SetComment("New File", "Comment for file");
```

### Set protection bits for a file

Example of setting read and write protection bits for a file from current directory:
```
await pfs3Volume.SetProtectionBits("New File", ProtectionBits.Write | ProtectionBits.Read);
```

### Set date for a file

Example of setting date for a file from current directory:
```
await pfs3Volume.SetDate("New File", DateTime.Now.AddDays(-10));
```