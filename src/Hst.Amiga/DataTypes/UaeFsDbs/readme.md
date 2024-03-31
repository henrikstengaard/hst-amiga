# UAEFSDB's

WinUAE uses a filesystem database called UAEFSDB to store metadata like protection bits, Amiga filenames with special characters and file comments when using a directory added as a harddrive.

UAEFSDB files are stored as one `_UAEFSFB.___` file per directory on FAT filesystems or as alternative streams on NTFS filesystems.

Regular filesystems like FAT, NTFS and EXT doesn't not support storing this kind of metadata, therefore UAEFSDB files are created and used to store metadata for Amiga filesystem. 

AmigaOS allows use of special character \, *, ?, ", <, > and | in filename and these are invalid in most modern filesystems.

## File structure

A UAEFSDB node represent metadata for one file in a UAEFSDB file and it can have following sizes:
- 600 bytes, version 1:
    - Contains one or more UAEFSDB nodes stored in one `_UAEFSFB.___` file per directory.
    - Created when adding FAT filesystem directories as harddrive in WinUAE.
- 1632 bytes: version 2, extension of version 1:
    - Contains one UAEFSDB node stored as alternative stream per file.
    - Created when adding NTFS filesystem directories as harddrive in WinUAE.

UAEFSDB node version 1 has following structure:

| Offset | Type  | Size | Description                                          |
|--------|-------|------|------------------------------------------------------|
| 0x0    | Byte  | 1    | Valid, always 1.                                     |
| 0x1    | ULong | 4    | Mode, protection bits.                               |
| 0x5    | Char  | 257  | Amiga name, max. 256 chars + null termination char.  |
| 0x106  | Char  | 257  | Normal name, max. 256 chars + null termination char. |
| 0x207  | Char  | 81   | Comment, max. 80 chars + null termination.           |

This structure is repeated for each node within the UAEFSDB file.

UAEFSDB node version 2 has following structure:

| Offset | Type  | Size | Description                                                                                                            |
|--------|-------|------|------------------------------------------------------------------------------------------------------------------------|
| 0x0    | Byte  | 1    | Valid, always 1.                                                                                                       |
| 0x1    | ULong | 4    | Mode, protection bits.                                                                                                 |
| 0x5    | Char  | 257  | Amiga name, ascii encoding, max. 256 chars + null termination char.                                                    |
| 0x106  | Char  | 257  | Normal name, ascii encoding, max. 256 chars + null termination char.                                                   |
| 0x207  | Char  | 81   | Comment, max. 80 chars + null termination.                                                                             |
| 0x258  | ULong | 4    | Windows-side mode, file attributes set on Windows side.                                                                |
| 0x25c  | Char  | 514  | Amiga name, unicode encoding, max. 256 chars (unicode uses 512 bytes) + null termination char (unicode uses 2 bytes).  |
| 0x45e  | Char  | 514  | Normal name, unicode encoding, max. 256 chars (unicode uses 512 bytes) + null termination char (unicode uses 2 bytes). |

## Creating a UAEFSDB file

Following commands can be used in Amiga CLI to create an UAEFSDB file using WinUAE:
```
echo "" >file1*
echo "" >file2<
protect file1* +s
filenote file2< "comment on file2"
```

On a FAT filesystem, this will create following files:
- `__uae___file1_`: contains content of `file1*`
- `__uae___file2_`: contains content of `file2<`
- `_UAEFSDB.___`: UAEFSDB file created either during it runs or when stopping WinUAE and contains metadata for both files.

On a NTFS filesystem, this will create following files:
- `__uae___file1_`: contains content of `file1*`
- `__uae___file2_`: contains content of `file2<`

The `_UAEFSDB.___`: UAEFSDB file appears to be hidden, but on NTFS filesystems WinUAE utilises alternative streams to store additional data and stores a `_UAEFSDB.___`: UAEFSDB file as an alternative stream for each file.

The following PowerShell command can be used to list `__uae___file1_` file's alternative streams:
```
Get-Item .\__uae___file1_ -Stream *
```

The following PowerShell command can be used to extract `_UAEFSDB.___` UAEFSDB file from `__uae___file1_` file's alternative streams:
```
Get-Content .\__uae___file1_ -Stream _UAEFSDB.___ | Set-Content .\_UAEFSDB.___ -NoNewline
```

The following PowerShell command can be used to store `_UAEFSDB.___` UAEFSDB file to `__uae___file1_` file's alternative streams:
```
Get-Content .\_UAEFSDB.___ -Raw | Set-Content .\__uae___file1_ -Stream _UAEFSDB.___ -NoNewline
```