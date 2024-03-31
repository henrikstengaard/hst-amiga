# UAE Metafiles

FS-UAE uses UAE metafiles with extension `.uaem` to store metadata like protection bits, date and file comment when using a directory added as a harddrive. Special characters are encoded in filename with the format `%dd` where dd is the hex value of the spacial character.

FS-UAE does also support UAEFSDB files with one `_UAEFSFB.___` UAEFSDB file per direct0ry.

Regular filesystems like FAT, NTFS and EXT doesn't not support storing this kind of metadata as the AmigaOS does, therefore UAE metafiles are created and used to store metadata for Amiga filesystem.

AmigaOS allows use of special character \, *, ?, ", <, > and | in filename and these are invalid in most modern filesystems.

# File structure

UAE metafile has following structure:

| Offset   | Type | Size | Description                   |
|----------|------|------|-------------------------------|
| 0x0      | Char | 8    | Protection bits.              |
| 0x8      | Char | 1    | Space delimiter (0x20).       |
| 0x9      | Char | 10   | Date in format `yyyy-mm-dd`.  |
| 0x13     | Char | 1    | Space delimiter (0x20).       |
| 0x14     | Char | 11   | Time in format `hh:mm:ss.ff`. |
| 0x1f     | Char | 1    | Space delimiter (0x20).       |
| 0x20     | Char | 0-x  | File comment.                 |
| 0x20 + x | Char | 1    | Newline.                      |

The metadata is stored in text format and can be edited using a regular text editor:
```
----rwed 2024-01-01 22:30:10.96 Comment
```

## Creating an UAE metafile

Following commands can be used in Amiga CLI to create an UAE metafile using FS-UAE:
```
echo "" >file1*
echo "" >file2<
protect file1* +s
filenote file2< "comment on file2"
```

This will create following files:
- `file1%2a.`: contains content of `file1*`. Filename has special
- `file1%2a.uaem`: UAE metafile with metadata for `file1*`.
- `file2%3c.`: contains content of `file2<`.
- `file2%3c.uaem`: UAE metafile with metadata for `file1*`.

# References

- https://fs-uae.net/devel/docs/hard-drives.html: FS-UAE documentation about file permission and metadata files.
- https://github.com/cnvogelg/amitools/blob/2a6af0cd778b7e9d11a05618e6996e9a164301b7/amitools/fs/MetaInfoFSUAE.py: Amitools implementation of uae metafile.