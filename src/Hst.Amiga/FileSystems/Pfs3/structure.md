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

Super blocks are used when disk is larger than 4GB.

Reserved bitmap block contains a bit per reserved block. Blocks used for reserved bitmap block is calculated by longs a bitmap block can contain divided by block size (usually 512 bytes).
First block of bitmap block can contain 512 - 2 * uword - 2 * ulong, 500 / 4 = 125.
Following blocks can contain 512 / 4 = 128 longs.

Each reserved block uses reserved block size, but root block first and last reserved are sector offsets.

Calculations:
- Reserved cluster size: reserved block size / block size. E.g. 1024 / 512 = 2.
- Reserved blocks: (last reserved - first reserved + 1) / reserved cluster size. E.g. (6529 - 2 + 1) / 2 = 3264. 

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




[19:00:57 DBG] Update: MakeBlockDirty, block nr = 446, block type 'indexblock'
[19:00:57 DBG] Allocation: AllocReservedBlock Enter
[19:00:57 DBG] Allocation: AllocReservedBlock, allocated block nr = 462, alloc_data.res_roving = 230
[19:00:57 DBG] Update: UpdateIBLK, small, oldblocknr = 446, newblocknr = 462


[19:00:57 DBG] Allocation: FreeReservedBlock, block nr = 446, t = 222
[19:00:57 DBG] Allocation: FreeReservedBlock, bits 1 = '00000001000001100011111110111111', uint = 4261175424
[19:00:57 DBG] Allocation: FreeReservedBlock, bits 2 = '01000001000001100011111110111111', uint = 4261175426
[19:00:57 DBG] Disk: Raw write block type 'indexblock' to block nr 462 with size of 2 blocks
[19:00:57 DBG] Disk: Raw write bytes to block nr 462 with size of 2 blocks


