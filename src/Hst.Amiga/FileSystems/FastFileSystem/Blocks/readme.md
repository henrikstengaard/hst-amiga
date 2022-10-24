# Blocks

FFS uses following blocks:

- Root block: Root blocks contain volume name, etc. and links to other blocks.
- Bitmap block: Bitmap blocks contain information about free and allocated blocks.
- Bitmap extension block: Bitmap extension blocks contain additional bitmap blocks.
- Dir cache block: Dir cache blocks contains records for caching and improving reading directories.
- Entry block: Entry blocks contains directory and file entries.
- File ext block: File extension blocks contain additional file entry information.
- Data block: Data blocks contains data for entry blocks.

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

| Offset | Data type     | Name              | Comment                     |
|--------|---------------|-------------------|-----------------------------|
| 0x000  | LONG          | Type              | = 2                         |
| 0x004  | ULONG         | Header key        | current block number        |
| 0x008  | ULONG         | High Seq          | = 0                         |
| 0x00C  | ULONG         | Hashtable size    | = 0                         |
| 0x010  | ULONG         | Reserved          | = 0                         |
| 0x014  | LONG          | Checksum          |                             |
| 0x018  | ULONG * 72    | Hashtable         | Hashtable with 72 items     |
| 0x138  | ULONG * 2     | Reserved          | = 0                         |
| 0x140  | ULONG         | Access            |                             |
| 0x144  | ULONG         | Reserved          | = 0                         |
| 0x148  | CHAR          | Length of comment |                             |
| 0x149  | CHAR * length | Comment           |                             |
| 0x1A4  | ULONG         | Days              | last access                 |
| 0x1A8  | ULONG         | Minutes           |                             |
| 0x1AC  | ULONG         | Ticks             |                             |
| 0x1B0  | CHAR          | Name length       | Length of directory name    |
| 0x1B1  | CHAR * length | Name              | Directory name              |
| 0x1D4  | ULONG         | real              | =0                          |
| 0x1D8  | ULONG         | nextLink          | link list                   |
| 0x1DC  | ULONG * 5     | Reserved          | = 0                         |
| 0x1F0  | ULONG         | nextSameHash      |                             |
| 0x1F4  | ULONG         | parent            | parent directory            |
| 0x1F8  | ULONG         | extension         | FFS : first directory cache |
| 0x1FC  | LONG          | secType           | = 2                         |

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