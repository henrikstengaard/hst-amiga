# Structure

The partition is split into 2 areas: reserved block area, data block area.

At the beginning of the partition is the reserved block area and the rest of the partition is generic blocks that can be allocated to store data.

## Reserved block size

Reserved block size is determined by partition size:

| Partition size    | Supermode | Reserved block size | Experimental feature |
|-------------------|-----------|---------------------|----------------------|
| Smaller than 5GB  | No        | 1024                | No                   |
| Larger than 5GB   | Yes       | 1024                | No                   |
| Larger than 104GB | Yes       | 2048                | Yes                  |
| Larger than 411GB | Yes       | 4096                | Yes                  |        

Super mode uses super blocks.

Experimental feature has been added as part of PFS3AIO.

## Blocks

PFS3 reads and writes sectors via block numbers, so block number 0 is the same as sector 0, which would be the first block/sector of the PFS3 partition. A block has always size of 512 bytes, so reserved blocks of e.g. size 1024 will use 2 blocks of disk space. 

Root anode is always block no. 5

Reserved block number:
- 0: Boot block
- 1: Boot block (continued)
- 2: Root block
- 3: BM: Reserved bitmap block (g.glob_allocdata.res_bitmap)
- 4: BM: Reserved bitmap block (continued)
- 5: Blank
- 6: EX: Root block extension
- 8: MI: Bitmap index block
- 9: MI: Bitmap index block (continued)
- 10 - x: BM: Bitmap blocks for partition.
-
- x - 14 : IB
- x - 12 : AB
- x - 10 : DB
- x - 8 : DD
- x - 6 : DD
- x - 4 : EX
- x - 2 : EX
- x : EX

## Root directory

anode rootdir = block no. 5;




dirblock
- direntry -> anode block


## Notes

PFS3 uses own packets in `void NormalCommands(struct DosPacket *action, globaldata *g)`
- ACTION_KILL_EMPTY
- ACTION_REMOVE_DIRENTRY
- ACTION_CREATE_ROLLOVER
- ACTION_SET_ROLLOVER
- ACTION_SET_DELDIR
- ACTION_SET_FNSIZE

How are these handled?