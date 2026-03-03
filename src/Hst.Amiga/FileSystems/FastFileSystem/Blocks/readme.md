# Blocks

Amiga fast file system uses following blocks:
- Bitmap block: Bitmap blocks contain information about free and allocated blocks.
- Bitmap extension block: Bitmap extension blocks contain additional bitmap blocks.
- Data block: Data blocks contains data for file header blocks.
- Dir block: Dir blocks contains a directory entry per block.
- Dir cache block: Dir cache blocks contains records for caching and improving reading directories.
- File extension block: File extension blocks contain additional file entry information.
- File header block: File header blocks contains a file entry per block.
- OFS data block: OFS data blocks contains data for file header blocks in DOS\0 FastFileSystem.
- Root block: Root blocks contain volume name, etc. and links to other blocks.

## Type and secondary type

Amiga fast file system uses a type and secondary type to identify the type of block and its purpose. The type is a long integer that identifies the general type of block, while the secondary type is a long integer that provides additional information about the block's purpose.

Types:
- T_SHORT = 2: A short block that contains a directory entry or a file entry. The secondary type of a short block can be used to identify the specific type of entry, such as a file header block or a directory block.
- T_DATA = 8: A data block that contains data for a file header block.
- T_LIST = 16: File extension block, which contains additional information about a file entry.
- T_DIRC = 33; Directory cache block, which contains records for caching and improving reading directories.
- T_COMMENT = 64: A comment block that contains a comment for a file or directory entry. Only used in Amiga file system using long file names.

Secondary types:
- ST_LINKFILE = -4: A hard link to a file, which is a file header block that points to the real file header block.
- ST_FILE = -3: A file header block that contains information about a file.
- ST_ROOT = 1: A root block that contains information about the volume and root directories and files.
- ST_USERDIR = 2: A directory block that contains information about a directory.
- ST_SOFTLINK = 3: A soft link to a file or directory, which is a file header block or directory block that contains the path name of the target file or directory.
- ST_LINKDIR = 4: A hard link to a directory, which is a directory block that points to the real directory block.

## Hard and soft links

Hard and soft links refers to a single file or directory.
A hard link associates a new name with an existing file or directory by linking to its physical location on disk.
A soft link associates a new name with an existing file or directory by linking to its path name. 
Hard and soft links are implemented in the fast file system as file header blocks and a new file header block is added for each link created.

A hard link has a type of T_SHORT (2) and a secondary type of ST_LINKFILE (-4) or ST_LINKDIR (4) depending on whether it is linked to a file or directory.

The hard link is defined by the "RealEntry" field linking to the physical location of the real directory or file.

The real directory or file points back to the link with the "NextLink" field.
The "NextLink" field is used to create a chain of links for a directory or file.
When a directory or file has more than one link, the "NextLink" field of the real directory or file points to the first link in the chain.
The chain of links goes from the newest added link to the oldest link. When a new link is added, the "NextLink" field of the real directory or file is updated to point to the new link and the "NextLink" field of the new link is set to the previous first link in the chain.

If a hard link is deleted, it is first removed from the chain of hard links and then its file header block is freed. If the object a hard link points to is deleted, then the first hard link in the chain is altered so that it becomes the new file header block. The original file header block is then freed.

Creating a new file creates file header block with "RealEntry" field set to 0 and "NextLink" field set to 0. 

| Sector | RealEntry | NextLink | Name |
|--------|-----------|----------|------|
| 882    | 0         | 0        | File |

When a hard link is created to the file, a new file header block is created with "RealEntry" field set to the sector of the real file header block and "NextLink" field set to 0. The "NextLink" field of the real file header block is updated to point to the new link.

| Sector | RealEntry | NextLink | Name  |
|--------|-----------|----------|-------|
| 882    | 0         | 884      | File  |
| 884    | 882       | 0        | Link1 |

Creating another hard link to the file creates another file header block with "RealEntry" field set to the sector of the real file header block and "NextLink" field set to 884, the sector of the previous first link.
The "NextLink" field of the real file header block is updated to point to the new link.

| Sector | RealEntry | NextLink | Name  |
|--------|-----------|----------|-------|
| 882    | 0         | 886      | File  |
| 884    | 882       | 0        | Link1 |
| 886    | 882       | 884      | Link2 |

The chain of links for the file looks like this:
```
882 (File) -> 886 (Link2) -> 884 (Link1) -> 0 (end of chain)
```

Soft links have type T_SHORT (2) and secondary type of ST_SOFTLINK (3).
The soft link uses the hash table area is used to store a string representing the path and name of the object being linked to, for example, "work:dir/file". The file system does not attempt to access "work:dir/file" but tells the caller that the file they are trying to access is a soft link.
The caller must then execute the correct DOS call, ReadLink(), to find out what file should really be opened.

If a soft link is deleted then its file header block is free. If the object a soft link points to is deleted then the soft link is left pointing at a nonexistent file. Subsequent references to the soft link will return the "object not found" error from AmigaDOS.

## Bitmap block

A bitmap block contain information about free and allocated blocks.
One bit is used per block. If the bit is set, the block is free, a cleared bit means an allocated block.

| Offset | Data type   | Name     | Comment |
|--------|-------------|----------|---------|
| 0x000  | LONG        | Checksum |         |
| 0x004  | ULONG * 127 | Map      |         |

## Bitmap extension block

| Offset | Data type   | Name        | Comment                                 |
|--------|-------------|-------------|-----------------------------------------|
| 0x000  | ULONG * 127 | BitmapPages | Bitmap page representing 32 blocks each |
| 0x1FC  | ULONG       | Next        | Next bitmap extension block             |

## Data block

| Offset | Data type   | Name | Comment |
|--------|-------------|------|---------|
| 0x000  | UCHAR * 512 | Data |         |

## Dir block

| Offset | Data type     | Name              | Comment                           |
|--------|---------------|-------------------|-----------------------------------|
| 0x000  | LONG          | Type              | = 2                               |
| 0x004  | ULONG         | Header key        | current block number              |
| 0x008  | ULONG         | High seq          | Higest sequence number = 0        |
| 0x00C  | ULONG         | Hashtable size    | = 0                               |
| 0x010  | ULONG         | Reserved          | = 0                               |
| 0x014  | LONG          | Checksum          |                                   |
| 0x018  | ULONG * 72    | Hashtable         | Hashtable with 72 items           |
| 0x138  | ULONG * 2     | Reserved          | = 0                               |
| 0x140  | ULONG         | Access            | Protection bits                   |
| 0x144  | ULONG         | Reserved          | = 0                               |
| 0x148  | CHAR          | Length of comment |                                   |
| 0x149  | CHAR * length | Comment           |                                   |
| 0x1A4  | ULONG         | Days              | last access                       |
| 0x1A8  | ULONG         | Minutes           |                                   |
| 0x1AC  | ULONG         | Ticks             |                                   |
| 0x1B0  | CHAR          | Name length       | Length of directory name          |
| 0x1B1  | CHAR * length | Name              | Directory name                    |
| 0x1D4  | ULONG         | Real              | =0                                |
| 0x1D8  | ULONG         | Next link         | link list                         |
| 0x1DC  | ULONG * 5     | Reserved          | = 0                               |
| 0x1F0  | ULONG         | Next same hash    |                                   |
| 0x1F4  | ULONG         | Parent            | Parent directory block number     |
| 0x1F8  | ULONG         | Extension         | FFS : first directory cache       |
| 0x1FC  | LONG          | SecType           | Secondary type = 2 = (ST_USERDIR) |

## Dir cache block

| Offset | Data type   | Name       | Comment              |
|--------|-------------|------------|----------------------|
| 0x000  | LONG        | Type       | = 33                 |
| 0x004  | ULONG       | Header key | current block number |
| 0x008  | ULONG       | parent     |                      |
| 0x00C  | ULONG       | recordsNb  |                      |
| 0x010  | ULONG       | nextDirC   |                      |
| 0x014  | LONG        | Checksum   |                      |
| 0x018  | UBYTE * 488 | records    |                      |

## File extension block

| Offset | Data type  | Name         | Comment                                |
|--------|------------|--------------|----------------------------------------|
| 0x000  | LONG       | Type         | = 0x10                                 |
| 0x004  | ULONG      | Header key   | current block number                   |
| 0x008  | ULONG      | High Seq     | number of data block in this hdr block |
| 0x00C  | ULONG      | Data size    | = 0                                    |
| 0x010  | ULONG      | First data   | = 0                                    |
| 0x014  | LONG       | Checksum     |                                        |
| 0x018  | ULONG * 72 | Datatable    | Datatable with 72 items                |
| 0x138  | ULONG * 45 | Reserved     | = 0                                    |
| 0x1EC  | ULONG      | Info         | = 0                                    |
| 0x1F0  | ULONG      | NextSameHash | = 0                                    |
| 0x1F4  | ULONG      | Parent       | pointer to file header block           |
| 0x1F8  | ULONG      | Extension    | pointer to next file extension block   |
| 0x1FC  | LONG       | SecType      | = -3                                   |

## File header block

| Offset | Data type     | Name              | Comment                                |
|--------|---------------|-------------------|----------------------------------------|
| 0x000  | LONG          | Type              | = 2                                    |
| 0x004  | ULONG         | Header key        | current block number                   |
| 0x008  | ULONG         | High Seq          | number of data block in this hdr block |
| 0x00C  | ULONG         | Data size         | = 0                                    |
| 0x010  | ULONG         | First Data        | = 0                                    |
| 0x014  | LONG          | Checksum          |                                        |
| 0x018  | ULONG * 72    | Hashtable         | Hashtable with 72 items                |
| 0x138  | ULONG * 2     | Reserved          | = 0                                    |
| 0x140  | ULONG         | Access            | bit0=del, 1=modif, 2=write, 3=read     |
| 0x144  | ULONG         | Byte size         | Size of file in bytes                  |
| 0x148  | CHAR          | Length of comment |                                        |
| 0x149  | CHAR * length | Comment           |                                        |
| 0x1A4  | ULONG         | Days              | last access                            |
| 0x1A8  | ULONG         | Minutes           |                                        |
| 0x1AC  | ULONG         | Ticks             |                                        |
| 0x1B0  | CHAR          | Name length       | Length of file name                    |
| 0x1B1  | CHAR * length | Name              | File name                              |
| 0x1D4  | ULONG         | Real              | unused == 0                            |
| 0x1D8  | ULONG         | NextLink          | link chain                             |
| 0x1DC  | ULONG * 5     | Reserved          | = 0                                    |
| 0x1F0  | ULONG         | Next same hash    | next entry with same hash              |
| 0x1F4  | ULONG         | Parent            | parent directory                       |
| 0x1F8  | ULONG         | Extension         | pointer to extension block             |
| 0x1FC  | LONG          | SecType           | = -3                                   |

## OFS data block

| Offset | Data type   | Name       | Comment                   |
|--------|-------------|------------|---------------------------|
| 0x000  | LONG        | Type       | == 8                      |
| 0x004  | ULONG       | Header key | pointer to file_hdr block |
| 0x008  | ULONG       | Seq num    | file data block number    |
| 0x00C  | ULONG       | Data size  | <= 0x1e8                  |
| 0x010  | ULONG       | Next Data  | next data block           |
| 0x014  | LONG        | Checksum   |                           |
| 0x018  | UCHAR * 488 | Data       |                           |

## Root block

| Offset | Data type     | Name           | Comment                                           |
|--------|---------------|----------------|---------------------------------------------------|
| 0x000  | LONG          | Type           | = 2                                               |
| 0x004  | ULONG         | Header key     | = 0 unused                                        |
| 0x008  | ULONG         | High Seq       | = 0 unused                                        |
| 0x00C  | ULONG         | Hashtable size | = 72                                              |
| 0x010  | ULONG         | First data     | = 0 unused                                        |
| 0x014  | LONG          | Checksum       | Root block checksum                               |
| 0x018  | ULONG * 72    | Hashtable      | Hashtable with 72 items                           |
| 0x138  | ULONG         | bmFlag         | bitmap flag, -1 means VALID                       |
| 0x13C  | ULONG * 25    | Bitmap pages   | Hashtable with 72 items                           |
| 0x1A0  | ULONG         | bmExt          | first bitmap extension block                      |
| 0x1A4  | ULONG         | Days           | last root alteration date                         |
| 0x1A8  | ULONG         | Minutes        |                                                   |
| 0x1AC  | ULONG         | Ticks          |                                                   |
| 0x1B0  | CHAR          | Name length    | Length of name                                    |
| 0x1B1  | CHAR * length | Name           | Volume name, MAXNAMELEN = 30                      |
| 0x1D0  | ULONG * 2     | Reserved       | = 0 unused                                        |
| 0x1D8  | ULONG         | Days           | last disk alteration date : days after 1 jan 1978 |
| 0x1DC  | ULONG         | Minutes        | hours and minutes in minutes                      |
| 0x1E0  | ULONG         | Ticks          | 1/50 seconds                                      |
| 0x1E4  | ULONG         | Days           | filesystem creation date : days after 1 jan 1978  |
| 0x1E8  | ULONG         | Minutes        | hours and minutes in minutes                      |
| 0x1EC  | ULONG         | Ticks          | 1/50 seconds                                      |
| 0x1F0  | ULONG         | nextSameHash   | unused (value = 0)                                |
| 0x1F4  | ULONG         | parent         | unused (value = 0)                                |
| 0x1F8  | ULONG         | extension      | FFS : first directory cache                       |
| 0x1FC  | LONG          | secType        | = 1                                               |