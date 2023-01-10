# Blocks

PFS3 uses following block types:

| Hex id | String id | Type                 |
|--------|-----------|----------------------|
| 0x4442 | DB        | Dir block            |
| 0x4142 | AB        | Anode block          |
| 0x4942 | IB        | Index block          |
| 0x424D | BM        | Bitmap block         |
| 0x4D49 | MI        | Bitmap index block   |
| 0x4558 | EX        | Root block extension |
| 0x4444 | DD        | Deldir block         |
| 0x5342 | SB        | Super block          |

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

## Allocation node block

| Offset | Data type     | Name      | Comment |
|--------|---------------|-----------|---------|
| 0x000  | UWORD         | Id        | AB      |
| 0x002  | UWORD         | Not used  |         |
| 0x004  | ULONG         | Datestamp |         |
| 0x008  | ULONG         | Seq nr    |         |
| 0x00c  | Anode * nodes | Nodes     |         |

Nodes = (Reserved block size - 0x00c) / anode size

## Anode

| Offset | Data type | Name         | Comment |
|--------|-----------|--------------|---------|
| 0x000  | ULONG     | Cluster size |         |
| 0x004  | ULONG     | Block nr     |         |
| 0x008  | ULONG     | Next         |         |

Size = 12 bytes

## Bitmap block

A bitmap block contain information about free and allocated blocks.
One bit is used per block. If the bit is set, the block is free, a cleared bit means an allocated block.

| Offset | Data type       | Name      | Comment |
|--------|-----------------|-----------|---------|
| 0x000  | UWORD           | Id        | BM      |
| 0x002  | UWORD           | Not used  |         |
| 0x004  | ULONG           | Datestamp |         |
| 0x008  | ULONG           | Seq nr    |         |
| 0x00c  | ULONG * entries | Map       |         |

Entries = (Reserved block size - 0x00c) / 4 (ULONG)

## Delete directory block

Delete directory blocks (deldirblock) stores deleted files and is used for file-recovery.

| Offset | Data type       | Name            | Comment                          |
|--------|-----------------|-----------------|----------------------------------|
| 0x000  | UWORD           | Id              | DD                               |
| 0x002  | UWORD           | Not used        |                                  |
| 0x004  | ULONG           | Datestamp       |                                  |
| 0x008  | ULONG           | Seq nr          |                                  |
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