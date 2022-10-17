# Blocks

Following sections describes blocks used by PFS3.

## Reserved blocks

Reserved block size is determined by partition size:

| Partition size    | Supermode | Reserved block size | Experimental feature |
|-------------------|-----------|---------------------|----------------------|
| Smaller than 5GB  | No        | 1024                | No                   |
| Larger than 5GB   | Yes       | 1024                | No                   |
| Larger than 104GB | Yes       | 2048                | Yes                  |
| Larger than 411GB | Yes       | 4096                | Yes                  |        

Super mode uses super blocks.

Experimental feature has been added as part of PFS3AIO.

## Bitmap block

A bitmap block contain information about free and allocated blocks.
One bit is used per block. If the bit is set, the block is free, a cleared bit means an allocated block.

| Offset | Data type       | Name      | Comment |
|--------|-----------------|-----------|---------|
| 0x000  | UWORD           | Id        | BM      |
| 0x002  | UWORD           | Not used  |         |
| 0x004  | ULONG           | Datestamp |         |
| 0x008  | ULONG           | Seqnr     |         |
| 0x00c  | ULONG * entries | Map       |         |

Entries = (Reserved block size - 0x00c) / 4 (ULONG)

## Delete directory block

Delete directory blocks (deldirblock) stores deleted files and is used for file-recovery.

| Offset | Data type       | Name            | Comment                          |
|--------|-----------------|-----------------|----------------------------------|
| 0x000  | UWORD           | Id              | DD                               |
| 0x002  | UWORD           | Not used        |                                  |
| 0x004  | ULONG           | Datestamp       |                                  |
| 0x008  | ULONG           | Seqnr           |                                  |
| 0x00c  | UWORD * 2       | Not used        |                                  |
| 0x010  | UWORD           |                 | roving in older versions	(<17.9) |
| 0x012  | UWORD           | Uid             |                                  |
| 0x014  | UWORD           | Gid             |                                  |
| 0x016  | ULONG           | Protection      |                                  |
| 0x01a  | UWORD           | Creation day    |                                  |
| 0x01c  | UWORD           | Creation minute |                                  |
| 0x01e  | UWORD           | Creation tick   |                                  |
| 0x020  | UBYTE * entries | Entries         |                                  |

Entries = Reserved block size - 0x020