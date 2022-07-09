# Blocks

## Root block

| Offset | Data type     | Name           | Comment                                           |
|--------|---------------|----------------|---------------------------------------------------|
| 0x000  | LONG          | Type           | = 2                                               |
| 0x004  | LONG          | Header key     | = 0 unused                                        |
| 0x008  | LONG          | High Seq       | = 0 unused                                        |
| 0x00C  | LONG          | Hashtable size | = 72                                              |
| 0x010  | LONG          | First data     | = 0 unused                                        |
| 0x014  | ULONG         | Checksum       | Root block checksum                               |
| 0x018  | LONG * 72     | Hashtable      | Hashtable with 72 items                           |
| 0x138  | LONG          | bmFlag         | bitmap flag, -1 means VALID                       |
| 0x13C  | LONG * 25     | Bitmap pages   | Hashtable with 72 items                           |
| 0x1A0  | LONG          | bmExt          | first bitmap extension block                      |
| 0x1A4  | LONG          | Days           | last root alteration date                         |
| 0x1A8  | LONG          | Minutes        |                                                   |
| 0x1AC  | LONG          | Ticks          |                                                   |
| 0x1B0  | CHAR          | Name length    | Length of name                                    |
| 0x1B1  | CHAR * length | Name           | Volume name, MAXNAMELEN = 30                      |
| 0x1D0  | LONG * 2      | Reserved       | = 0 unused                                        |
| 0x1D8  | LONG          | Days           | last disk alteration date : days after 1 jan 1978 |
| 0x1DC  | LONG          | Minutes        | hours and minutes in minutes                      |
| 0x1E0  | LONG          | Ticks          | 1/50 seconds                                      |
| 0x1E4  | LONG          | Days           | filesystem creation date : days after 1 jan 1978  |
| 0x1E8  | LONG          | Minutes        | hours and minutes in minutes                      |
| 0x1EC  | LONG          | Ticks          | 1/50 seconds                                      |
| 0x1F0  | LONG          | nextSameHash   | unused (value = 0)                                |
| 0x1F4  | LONG          | parent         | unused (value = 0)                                |
| 0x1F8  | LONG          | extension      | FFS : first directory cache                       |
| 0x1FC  | LONG          | secType        | = 1                                               |

## Dir block

| Offset | Data type     | Name              | Comment                     |
|--------|---------------|-------------------|-----------------------------|
| 0x000  | LONG          | Type              | = 2                         |
| 0x004  | LONG          | Header key        | current block number        |
| 0x008  | LONG          | High Seq          | = 0                         |
| 0x00C  | LONG          | Hashtable size    | = 0                         |
| 0x010  | LONG          | Reserved          | = 0                         |
| 0x014  | ULONG         | Checksum          |                             |
| 0x018  | LONG * 72     | Hashtable         | Hashtable with 72 items     |
| 0x138  | LONG * 2      | Reserved          | = 0                         |
| 0x140  | LONG          | Access            |                             |
| 0x144  | LONG          | Reserved          | = 0                         |
| 0x148  | CHAR          | Length of comment |                             |
| 0x149  | CHAR * length | Comment           |                             |
| 0x1A4  | LONG          | Days              | last access                 |
| 0x1A8  | LONG          | Minutes           |                             |
| 0x1AC  | LONG          | Ticks             |                             |
| 0x1B0  | CHAR          | Name length       | Length of directory name    |
| 0x1B1  | CHAR * length | Name              | Directory name              |
| 0x1D4  | LONG          | real              | =0                          |
| 0x1D8  | LONG          | nextLink          | link list                   |
| 0x1DC  | LONG * 5      | Reserved          | = 0                         |
| 0x1F0  | LONG          | nextSameHash      |                             |
| 0x1F4  | LONG          | parent            | parent directory            |
| 0x1F8  | LONG          | extension         | FFS : first directory cache |
| 0x1FC  | LONG          | secType           | = 2                         |

## File header block

| Offset | Data type     | Name              | Comment                                |
|--------|---------------|-------------------|----------------------------------------|
| 0x000  | LONG          | Type              | = 2                                    |
| 0x004  | LONG          | Header key        | current block number                   |
| 0x008  | LONG          | High Seq          | number of data block in this hdr block |
| 0x00C  | LONG          | Data size         | = 0                                    |
| 0x010  | LONG          | first Data        | = 0                                    |
| 0x014  | ULONG         | Checksum          |                                        |
| 0x018  | LONG * 72     | Hashtable         | Hashtable with 72 items                |
| 0x138  | LONG * 2      | Reserved          | = 0                                    |
| 0x140  | LONG          | Access            | bit0=del, 1=modif, 2=write, 3=read     |
| 0x144  | LONG          | Reserved          | Size of file in bytes                  |
| 0x148  | CHAR          | Length of comment |                                        |
| 0x149  | CHAR * length | Comment           |                                        |
| 0x1A4  | LONG          | Days              | last access                            |
| 0x1A8  | LONG          | Minutes           |                                        |
| 0x1AC  | LONG          | Ticks             |                                        |
| 0x1B0  | CHAR          | Name length       | Length of file name                    |
| 0x1B1  | CHAR * length | Name              | File name                              |
| 0x1D4  | LONG          | Real              | unused == 0                            |
| 0x1D8  | LONG          | NextLink          | link chain                             |
| 0x1DC  | LONG * 5      | Reserved          | = 0                                    |
| 0x1F0  | LONG          | NextSameHash      | next entry with same hash              |
| 0x1F4  | LONG          | Parent            | parent directory                       |
| 0x1F8  | LONG          | Extension         | pointer to extension block             |
| 0x1FC  | LONG          | SecType           | = -3                                   |

## Dir Cache Block

| Offset | Data type   | Name       | Comment              |
|--------|-------------|------------|----------------------|
| 0x000  | LONG        | Type       | = 33                 |
| 0x004  | LONG        | Header key | current block number |
| 0x008  | LONG        | parent     |                      |
| 0x00C  | LONG        | recordsNb  |                      |
| 0x010  | LONG        | nextDirC   |                      |
| 0x014  | ULONG       | Checksum   |                      |
| 0x018  | UBYTE * 488 | records    |                      |

## File Ext Block

| Offset | Data type | Name         | Comment                                |
|--------|-----------|--------------|----------------------------------------|
| 0x000  | LONG      | Type         | = 0x10                                 |
| 0x004  | LONG      | Header key   | current block number                   |
| 0x008  | LONG      | High Seq     | number of data block in this hdr block |
| 0x00C  | LONG      | Data size    | = 0                                    |
| 0x010  | LONG      | First data   | = 0                                    |
| 0x014  | ULONG     | Checksum     |                                        |
| 0x018  | LONG * 72 | Hashtable    | Hashtable with 72 items                |
| 0x138  | LONG * 45 | Reserved     | = 0                                    |
| 0x1EC  | LONG      | Info         | = 0                                    |
| 0x1F0  | LONG      | NextSameHash | = 0                                    |
| 0x1F4  | LONG      | Parent       | pointer to file header block           |
| 0x1F8  | LONG      | Extension    | pointer to next file extension block   |
| 0x1FC  | LONG      | SecType      | = -3                                   |

## Bitmap Ext Block

| Offset | Data type   | Name        | Comment                                 |
|--------|-------------|-------------|-----------------------------------------|
| 0x000  | ULONG * 127 | BitmapPages | Bitmap page representing 32 blocks each |
| 0x1FC  | ULONG       | Next        | Next bitmap ext block                   |

## OFS data block

| Offset | Data type   | Name       | Comment                   |
|--------|-------------|------------|---------------------------|
| 0x000  | LONG        | Type       | == 8                      |
| 0x004  | LONG        | Header key | pointer to file_hdr block |
| 0x008  | LONG        | Seq num    | file data block number    |
| 0x00C  | LONG        | Data size  | <= 0x1e8                  |
| 0x010  | LONG        | Next Data  | next data block           |
| 0x014  | ULONG       | Checksum   |                           |
| 0x018  | UCHAR * 488 | Data       |                           |

