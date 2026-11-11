# Blocks

Amiga smart file system uses following blocks:
- Root block: Root blocks contain partition geometry, block size and links to other administration blocks. Located at first and last block of the partition.
- Admin space container block: Admin space container blocks keep track of admin spaces, areas of 32 blocks used for administration blocks.
- Bitmap block: Bitmap blocks contain information about free and allocated blocks.
- Object container block: Object container blocks contain one or more objects (files, directories and soft links) of a directory.
- Hash table block: Hash table blocks contain hash chains for fast lookup of objects in a directory.
- Soft link block: Soft link blocks contain the path of a soft link.
- Node container block: Node container blocks form the object node tree, which maps object node numbers to object container blocks.
- B-tree node container block: B-tree node container blocks form the extent B-tree, which contains extents describing data blocks used by files.
- Transaction ok block, transaction failure block and transaction storage block: Transaction blocks are used to safely write changes to administration blocks.
- Data block: Data blocks contain file data without any header.

All values are stored big endian and all block numbers are relative to the start of the partition.
Structures are packed as on 68k processors, where 16-bit and 32-bit values are aligned to 2 bytes.
Offsets in this document are shown for 512 bytes block size, where structures depend on block size the size is given as a formula with "bs" as block size in bytes.

Supported block sizes are 512 to 32768 bytes and must be a power of 2.

## Data types

| Data type | Size    | Description                                                                                                  |
|-----------|---------|--------------------------------------------------------------------------------------------------------------|
| UBYTE     | 1 byte  | Unsigned byte.                                                                                               |
| UWORD     | 2 bytes | Unsigned 16-bit integer.                                                                                     |
| ULONG     | 4 bytes | Unsigned 32-bit integer.                                                                                     |
| BLCK      | 4 bytes | Block number relative to start of partition.                                                                 |
| NODE      | 4 bytes | Object node number.                                                                                          |
| BLCKn     | 4 bytes | Block number shifted left by (log2(bs) - 5) with bit 0 used as a flag, used in node index containers.        |
| DATE      | 4 bytes | Seconds since 1. January 1978 00:00:00.                                                                      |
| CHAR      | 1 byte  | Character using ISO-8859-1 character set.                                                                    |

## Block ids

Every block, except data blocks, starts with a block header containing an id identifying the type of block.

| Hex id     | String id | Type                         | Structure in smart file system source             |
|------------|-----------|------------------------------|---------------------------------------------------|
| 0x53465300 | SFS\0     | Root block                   | struct fsRootBlock, fs/blockstructure.h           |
| 0x41444D43 | ADMC      | Admin space container block  | struct fsAdminSpaceContainer, fs/adminspaces.h    |
| 0x42544D50 | BTMP      | Bitmap block                 | struct fsBitmap, fs/bitmap.h                      |
| 0x4F424A43 | OBJC      | Object container block       | struct fsObjectContainer, fs/objects.h            |
| 0x48544142 | HTAB      | Hash table block             | struct fsHashTable, fs/objects.h                  |
| 0x534C4E4B | SLNK      | Soft link block              | struct fsSoftLink, fs/objects.h                   |
| 0x4E444320 | NDC       | Node container block         | struct fsNodeContainer, fs/nodes.h                |
| 0x424E4443 | BNDC      | B-tree node container block  | struct fsBNodeContainer, fs/btreenodes.h          |
| 0x54524F4B | TROK      | Transaction ok block         | struct fsBlockHeader, fs/transactions.h           |
| 0x54524641 | TRFA      | Transaction failure block    | struct fsTransactionFailure, fs/transactions.h    |
| 0x54525354 | TRST      | Transaction storage block    | struct fsTransactionStorage, fs/transactions.h    |

## Block header

Block header (struct fsBlockHeader) is present at the start of every block, except data blocks.

| Offset | Data type | Name     | Comment                                     |
|--------|-----------|----------|---------------------------------------------|
| 0x000  | ULONG     | Id       | Block id, see block ids                     |
| 0x004  | ULONG     | Checksum | Checksum of block                           |
| 0x008  | BLCK      | OwnBlock | Block number of this block                  |

A block is valid when id is the expected id, checksum is valid and own block is equal to the block number the block was read from.

### Checksum

The checksum is calculated by adding all longs in the block to an initial value of 1. The checksum field is set to 0 before calculating the sum and the checksum stored is the negated sum.
A block has a valid checksum, when the sum of all longs in the block including the checksum and with the initial value of 1 is 0.

```
ULONG CALCCHECKSUM(ULONG blocksize, ULONG *block) {
    ULONG sum = 1;
    blocksize >>= 2;
    while (blocksize--) {
        sum += *block++;
    }
    return sum;
}

void setchecksum(ULONG blocksize, struct fsBlockHeader *bh) {
    bh->checksum = 0;
    bh->checksum = -CALCCHECKSUM(blocksize, (ULONG *)bh);
}
```

## Root block

Root block (struct fsRootBlock) is located at the first block (block 0) and last block (total blocks - 1) of the partition.
Both root blocks contain the same data except own block and checksum.
Smart file system doesn't change root blocks after the partition is formatted. Volume name and free blocks are stored in the root object container.

| Offset | Data type  | Name                | Comment                                                                                         |
|--------|------------|---------------------|-------------------------------------------------------------------------------------------------|
| 0x000  | ULONG      | Id                  | = 0x53465300 "SFS\0"                                                                            |
| 0x004  | ULONG      | Checksum            |                                                                                                 |
| 0x008  | BLCK       | OwnBlock            | = 0 for first root block and = total blocks - 1 for second root block                           |
| 0x00C  | UWORD      | Version             | Version of structures, = 3                                                                      |
| 0x00E  | UWORD      | SequenceNumber      | = 0, not used                                                                                   |
| 0x010  | DATE       | DateCreated         | Date volume was created                                                                         |
| 0x014  | UBYTE      | Bits                | Root bits, see root bits                                                                        |
| 0x015  | UBYTE      | Pad1                | = 0                                                                                             |
| 0x016  | UWORD      | Pad2                | = 0                                                                                             |
| 0x018  | ULONG * 2  | Reserved1           | = 0                                                                                             |
| 0x020  | ULONG      | FirstByteHigh       | High 32 bits of byte offset of partition start on disk                                          |
| 0x024  | ULONG      | FirstByte           | Low 32 bits of byte offset of partition start on disk = LowCyl * Surfaces * BlocksPerTrack * SectorSize |
| 0x028  | ULONG      | LastByteHigh        | High 32 bits of byte offset of partition end on disk (exclusive)                                |
| 0x02C  | ULONG      | LastByte            | Low 32 bits of byte offset of partition end on disk = (HighCyl + 1) * Surfaces * BlocksPerTrack * SectorSize |
| 0x030  | BLCK       | TotalBlocks         | Total number of blocks in partition                                                             |
| 0x034  | ULONG      | BlockSize           | Size of blocks in bytes                                                                         |
| 0x038  | ULONG * 2  | Reserved2           | = 0                                                                                             |
| 0x040  | ULONG * 8  | Reserved3           | = 0                                                                                             |
| 0x060  | BLCK       | BitmapBase          | First bitmap block                                                                              |
| 0x064  | BLCK       | AdminSpaceContainer | First admin space container block                                                               |
| 0x068  | BLCK       | RootObjectContainer | Root object container block containing root directory object and root info                      |
| 0x06C  | BLCK       | ExtentBNodeRoot     | Root B-tree node container block of extent B-tree                                               |
| 0x070  | BLCK       | ObjectNodeRoot      | Root node container block of object node tree                                                   |
| 0x074  | ULONG * 3  | Reserved4           | = 0                                                                                             |
| 0x080  | UBYTE * (bs - 128) | Unused      | = 0                                                                                             |

Root bits:

| Value | Name                   | Comment                                                    |
|-------|------------------------|------------------------------------------------------------|
| 0x80  | ROOTBITS_CASESENSITIVE | Set if names are case sensitive.                           |
| 0x40  | ROOTBITS_RECYCLED      | Set if volume has a recycled directory for deleted files.  |

When smart file system mounts a partition, it requires both root blocks to be valid, version to be 3, block size to match partition block size and first byte, last byte and total blocks to match partition geometry.
AROS smart file system only checks the size of the partition (last byte - first byte), not the absolute first and last byte.

## Admin space container block

Admin space container block (struct fsAdminSpaceContainer) keeps track of admin spaces.
An admin space is an area of 32 continuous blocks, which are marked as used in the bitmap and used for administration blocks: object containers, hash tables, soft links, node containers, B-tree node containers, admin space containers and transaction ok/failure block.
Root blocks and bitmap blocks are not allocated from admin spaces.

| Offset | Data type                  | Name        | Comment                                                    |
|--------|----------------------------|-------------|------------------------------------------------------------|
| 0x000  | ULONG                      | Id          | = 0x41444D43 "ADMC"                                        |
| 0x004  | ULONG                      | Checksum    |                                                            |
| 0x008  | BLCK                       | OwnBlock    |                                                            |
| 0x00C  | BLCK                       | Next        | Next admin space container block, 0 if last                |
| 0x010  | BLCK                       | Previous    | Previous admin space container block, 0 if first           |
| 0x014  | UBYTE                      | Bits        | Number of blocks in each admin space, = 32                 |
| 0x015  | UBYTE                      | Pad1        | = 0                                                        |
| 0x016  | UWORD                      | Pad2        | = 0                                                        |
| 0x018  | AdminSpace * ((bs-24)/8)   | AdminSpaces | Admin spaces, 61 admin spaces for 512 bytes block size     |

Admin space (struct fsAdminSpace):

| Offset | Data type | Name  | Comment                                                                                         |
|--------|-----------|-------|-------------------------------------------------------------------------------------------------|
| 0x000  | BLCK      | Space | First block of admin space, 0 if admin space entry is unused                                    |
| 0x004  | ULONG     | Bits  | One bit per block in admin space. Bit 31 is first block and bit 0 is last block. Set bit = used |

Allocating an administration block finds the first clear bit in the admin spaces of the admin space container chain.
If all admin spaces are full, 32 continuous free blocks are found in the bitmap starting from block 0, marked as used in the bitmap and added as a new admin space.
If no admin space container has an unused admin space entry, the first block of the new admin space becomes a new admin space container linked at the end of the admin space container chain.

Freeing an administration block clears the bit in the admin space. The admin space remains allocated in the bitmap.

## Bitmap block

Bitmap block (struct fsBitmap) contains one bit per block in the partition. Bitmap blocks are consecutive blocks starting at the bitmap base block from root block.
Bits are stored most significant bit first. A set bit means the block is free, a cleared bit means the block is used.
All blocks in the partition are covered by the bitmap including reserved blocks, root blocks, admin spaces and bitmap blocks, which are marked used.
Bits after the last block of the partition are cleared.

| Offset | Data type             | Name     | Comment                                                         |
|--------|-----------------------|----------|-----------------------------------------------------------------|
| 0x000  | ULONG                 | Id       | = 0x42544D50 "BTMP"                                             |
| 0x004  | ULONG                 | Checksum |                                                                 |
| 0x008  | BLCK                  | OwnBlock |                                                                 |
| 0x00C  | ULONG * ((bs-12)/4)   | Bitmap   | Bitmap with 125 longs covering 4000 blocks for 512 bytes block size |

Each bitmap block covers (bs - 12) * 8 blocks and number of bitmap blocks = ceil(total blocks / ((bs - 12) * 8)).

Location of bit for block n:
- Bitmap block = bitmap base + n / ((bs - 12) * 8)
- Bit in bitmap block b = n % ((bs - 12) * 8)
- Long in bitmap = b / 32
- Mask = 0x80000000 >> (b % 32)

Number of free blocks must match free blocks in root info, which is updated every time blocks are marked used or free.

## Object container block

Object container block (struct fsObjectContainer) contains one or more objects of a directory. Objects are files, directories and soft links.
Object container blocks of a directory are linked in a double linked list starting with first dir block of the directory object.

| Offset | Data type     | Name     | Comment                                                                       |
|--------|---------------|----------|-------------------------------------------------------------------------------|
| 0x000  | ULONG         | Id       | = 0x4F424A43 "OBJC"                                                           |
| 0x004  | ULONG         | Checksum |                                                                               |
| 0x008  | BLCK          | OwnBlock |                                                                               |
| 0x00C  | NODE          | Parent   | Object node of directory containing objects, 0 for root object container      |
| 0x010  | BLCK          | Next     | Next object container block of directory, 0 if last                           |
| 0x014  | BLCK          | Previous | Previous object container block of directory, 0 if first                      |
| 0x018  | Object * n    | Objects  | Objects with variable size                                                    |

Objects are stored one after another. Each object starts at an even offset.
An object exists while offset is less than bs - sizeof(struct fsObject) - 2 and the first character of the name is not 0. Unused space after the last object is 0.
sizeof(struct fsObject) is 26 on 68k and 28 on AROS builds for other processors.

The space needed to create an object in an object container is sizeof(struct fsObject) + length of name + 2 + length of comment.
Objects are added to the first object container in the directory with enough free space. If no object container has enough space, a new object container is created and linked first in the directory.
Directories with quick dir bit set only look in the first object container for free space.

When an object is removed from an object container, objects after it are moved down and the freed space is cleared. If it's the only object, the object container is unlinked from the directory and freed.

### Object

Object (struct fsObject) describes a file, directory or soft link. Offsets are relative to start of object.

| Offset | Data type     | Name                 | Comment                                                                                        |
|--------|---------------|----------------------|------------------------------------------------------------------------------------------------|
| 0x000  | UWORD         | OwnerUid             | Owner user id, = 0                                                                             |
| 0x002  | UWORD         | OwnerGid             | Owner group id, = 0                                                                            |
| 0x004  | NODE          | ObjectNode           | Object node number of object                                                                   |
| 0x008  | ULONG         | Protection           | Protection bits, see protection                                                                |
| 0x00C  | BLCK          | Data / HashTable     | File: first data block and key of first extent, 0 if file is empty. Soft link: soft link block. Directory: hash table block, 0 if directory has no hash table |
| 0x010  | ULONG         | Size / FirstDirBlock | File: size of file in bytes (max 4 GB - 2 bytes), 0 for soft link. Directory: first object container block of directory, 0 if directory is empty |
| 0x014  | DATE          | DateModified         | Date object was last modified                                                                  |
| 0x018  | UBYTE         | Bits                 | Object bits, see object bits                                                                   |
| 0x019  | CHAR * n      | Name                 | Name terminated with 0, max 100 characters                                                     |
| 0x019 + n + 1 | CHAR * m | Comment          | Comment terminated with 0, max 79 characters. An empty comment is a single 0                   |

Object size = 25 + length of name + 1 + length of comment + 1, padded to an even number of bytes.

Object bits:

| Value | Name              | Comment                                                                                   |
|-------|-------------------|-------------------------------------------------------------------------------------------|
| 0x01  | OTYPE_HIDDEN      | Object is hidden and not listed, e.g. recycled directory                                  |
| 0x02  | OTYPE_UNDELETABLE | Object can't be deleted, e.g. recycled directory                                          |
| 0x04  | OTYPE_QUICKDIR    | Directory only looks in first object container for free space when adding objects         |
| 0x08  | OTYPE_RINGLIST    | Not used                                                                                  |
| 0x20  | OTYPE_HARDLINK    | Not supported                                                                             |
| 0x40  | OTYPE_LINK        | Object is a soft link, when OTYPE_HARDLINK is not set                                     |
| 0x80  | OTYPE_DIR         | Object is a directory                                                                     |

### Protection

Protection is stored as AmigaDOS protection bits (fib_Protection) XOR 0x0F, which inverts the read, write, execute and delete bits.
This means read, write, execute and delete bits are set when allowed, opposite of AmigaDOS. Default protection for new objects is 0x0F, which allows read, write, execute and delete.
Hold, script, pure and archive bits and bits above bit 7 (group and other bits) are stored unchanged.

| Bit | Value | Name    | Comment                       |
|-----|-------|---------|-------------------------------|
| 7   | 0x80  | Hold    | Set = hold                    |
| 6   | 0x40  | Script  | Set = script                  |
| 5   | 0x20  | Pure    | Set = pure                    |
| 4   | 0x10  | Archive | Set = archived                |
| 3   | 0x08  | Read    | Set = read allowed            |
| 2   | 0x04  | Write   | Set = write allowed           |
| 1   | 0x02  | Execute | Set = execute allowed         |
| 0   | 0x01  | Delete  | Set = delete allowed          |

When a directory is changed, date modified of the directory is updated and archive bit is cleared.

## Root info

Root info (struct fsRootInfo) is stored in the last 36 bytes of the root object container block at offset bs - 36.
The root object container only contains the root directory object with the volume name (max 30 characters), object node 1 and parent 0. It's never part of a directory's object container chain.

| Offset   | Data type | Name                    | Comment                                                       |
|----------|-----------|-------------------------|---------------------------------------------------------------|
| bs - 36  | ULONG     | DeletedBlocks           | Number of blocks used by deleted files in recycled directory  |
| bs - 32  | ULONG     | DeletedFiles            | Number of deleted files in recycled directory                 |
| bs - 28  | ULONG     | FreeBlocks              | Number of free blocks, must match bitmap                      |
| bs - 24  | DATE      | DateCreated             | Date volume was created                                       |
| bs - 20  | BLCK      | LastAllocatedBlock      | Not used                                                      |
| bs - 16  | BLCK      | LastAllocatedAdminSpace | Not used                                                      |
| bs - 12  | ULONG     | LastAllocatedExtentNode | Not used                                                      |
| bs - 8   | ULONG     | LastAllocatedObjectNode | Not used                                                      |
| bs - 4   | ULONG     | RovingPointer           | Not used, smart file system keeps roving pointer in memory    |

## Hash table block

Hash table block (struct fsHashTable) contains hash chains used to find objects in a directory by name.
Each hash entry contains the object node of the first object in the hash chain and object nodes are linked using next in object node.

| Offset | Data type             | Name       | Comment                                                                 |
|--------|-----------------------|------------|-------------------------------------------------------------------------|
| 0x000  | ULONG                 | Id         | = 0x48544142 "HTAB"                                                     |
| 0x004  | ULONG                 | Checksum   |                                                                         |
| 0x008  | BLCK                  | OwnBlock   |                                                                         |
| 0x00C  | NODE                  | Parent     | Object node of directory                                                |
| 0x010  | NODE * ((bs-16)/4)    | HashEntry  | First object node in each hash chain, 0 if empty. 124 entries for 512 bytes block size |

### Hash function

The hash function calculates a 16-bit hash of the name. Hash starts with length of name and for each character hash = hash * 13 + character, where all arithmetic wraps at 16 bits.
If volume is not case sensitive, characters are converted to upper case using ISO-8859-1 character set before calculating hash.
Hash chain = hash % ((bs - 16) / 4).

```
UBYTE upperchar(UBYTE c) {
    if ((c >= 224 && c <= 254 && c != 247) || (c >= 'a' && c <= 'z')) {
        c -= 32;
    }
    return c;
}

UWORD hash(UBYTE *name, WORD casesensitive) {
    UWORD hash = 0;
    while (name[hash] != 0 && name[hash] != '/') {
        hash++;
    }
    while (*name != 0 && *name != '/') {
        hash = hash * 13 + (casesensitive ? *name++ : upperchar(*name++));
    }
    return hash;
}

#define HASHCHAIN(x) (UWORD)(x % (UWORD)((bytes_block - sizeof(struct fsHashTable)) >> 2))
```

Example: Hash of ".recycled" is 0x47EA when not case sensitive and 0x72EA when case sensitive. For 512 bytes block size, hash chains are 58 and 30.

New objects are added first in a hash chain. Names are compared using upper case characters, if volume is not case sensitive.

## Soft link block

Soft link block (struct fsSoftLink) contains the path of a soft link. The object for the soft link has OTYPE_LINK bit set, data set to the soft link block and size 0.

| Offset | Data type             | Name     | Comment                                  |
|--------|-----------------------|----------|------------------------------------------|
| 0x000  | ULONG                 | Id       | = 0x534C4E4B "SLNK"                      |
| 0x004  | ULONG                 | Checksum |                                          |
| 0x008  | BLCK                  | OwnBlock |                                          |
| 0x00C  | NODE                  | Parent   | Object node of soft link object          |
| 0x010  | BLCK                  | Next     | = 0, not used                            |
| 0x014  | BLCK                  | Previous | = 0, not used                            |
| 0x018  | CHAR * (bs - 24)      | String   | Path of soft link terminated with 0      |

## Node container block

Node container block (struct fsNodeContainer) is part of the object node tree, which maps object node numbers to the object container blocks containing objects.
A leaf node container (nodes = 1) contains object nodes. An index node container contains pointers to child node containers.
The root node container never changes block and when it's full, a new level is added by moving the contents of the root node container to a new block.

| Offset | Data type                   | Name        | Comment                                                                                    |
|--------|-----------------------------|-------------|--------------------------------------------------------------------------------------------|
| 0x000  | ULONG                       | Id          | = 0x4E444320 "NDC "                                                                        |
| 0x004  | ULONG                       | Checksum    |                                                                                            |
| 0x008  | BLCK                        | OwnBlock    |                                                                                            |
| 0x00C  | NODE                        | NodeNumber  | Node number of first node covered by node container                                        |
| 0x010  | ULONG                       | Nodes       | Number of nodes covered by each entry, = 1 for leaf node container                         |
| 0x014  | ObjectNode * ((bs-20)/10)   | ObjectNodes | Leaf node container: object nodes, 49 object nodes for 512 bytes block size                |
| 0x014  | BLCKn * ((bs-20)/4)         | Pointers    | Index node container: pointers to child node containers, 123 pointers for 512 bytes block size |

Pointers in index node containers are block number shifted left by (log2(bs) - 5), e.g. shifted left by 4 for 512 bytes block size. Bit 0 is set when child node container is full, 0 means pointer is unused.
Child node container for pointer at index i has node number = node number + i * nodes. Nodes for child node container is 1, if nodes = leaf capacity ((bs - 20) / 10), otherwise nodes / ((bs - 20) / 4).

### Object node

Object node (struct fsObjectNode):

| Offset | Data type | Name   | Comment                                                                                       |
|--------|-----------|--------|-----------------------------------------------------------------------------------------------|
| 0x000  | BLCK      | Data   | Object container block containing object, 0 = free object node and 0xFFFFFFFF = reserved       |
| 0x004  | NODE      | Next   | Next object node in hash chain, 0 if last                                                     |
| 0x008  | UWORD     | Hash16 | 16-bit hash of object name                                                                    |

Object node numbers:

| Node number | Comment                                       |
|-------------|-----------------------------------------------|
| 0           | Doesn't exist, root node container starts at node number 1 |
| 1           | Root directory                                |
| 2           | Recycled directory                            |
| 3 - 6       | Reserved, data = 0xFFFFFFFF                   |

Finding object node n:
1. Read root node container.
2. While node container is an index node container: Read child node container at pointer (n - node number) / nodes, shifting pointer right by (log2(bs) - 5).
3. Object node is at index n - node number in leaf node container.

## B-tree node container block

B-tree node container block (struct fsBNodeContainer) is part of the extent B-tree.
The extent B-tree contains extents for all files sorted by key, which is the first block of the extent.
Leaf containers contain extent B-tree nodes and internal containers contain B-tree nodes pointing to child containers.
The root B-tree node container never changes block. When it's full, the contents of the root is moved to a new block and root becomes an internal container with one node with key 0 pointing to it.

| Offset | Data type                  | Name      | Comment                                                                            |
|--------|----------------------------|-----------|------------------------------------------------------------------------------------|
| 0x000  | ULONG                      | Id        | = 0x424E4443 "BNDC"                                                                |
| 0x004  | ULONG                      | Checksum  |                                                                                    |
| 0x008  | BLCK                       | OwnBlock  |                                                                                    |
| 0x00C  | UWORD                      | NodeCount | Number of nodes in container                                                       |
| 0x00E  | UBYTE                      | IsLeaf    | = 1 for leaf container, = 0 for internal container                                 |
| 0x00F  | UBYTE                      | NodeSize  | Size of each node, = 14 for leaf container and = 8 for internal container          |
| 0x010  | BNode * ((bs-16)/nodesize) | Nodes     | Nodes sorted by key. 35 extent nodes or 62 B-tree nodes for 512 bytes block size   |

B-tree node (struct BNode) used in internal containers:

| Offset | Data type | Name | Comment                                       |
|--------|-----------|------|-----------------------------------------------|
| 0x000  | ULONG     | Key  | First key in child container                  |
| 0x004  | BLCK      | Data | Child B-tree node container block             |

Extent B-tree node (struct fsExtentBNode) used in leaf containers:

| Offset | Data type | Name   | Comment                                                                                            |
|--------|-----------|--------|----------------------------------------------------------------------------------------------------|
| 0x000  | BLCK      | Key    | First block of extent                                                                              |
| 0x004  | BLCK      | Next   | Key of next extent of file, 0 if last extent                                                       |
| 0x008  | ULONG     | Prev   | Key of previous extent of file. For first extent: object node of file with bit 31 set (0x80000000) |
| 0x00C  | UWORD     | Blocks | Number of continuous blocks in extent, 1 - 65535                                                   |

Searching for a key in a container scans from the last node backwards and returns the first node with key less than or equal to the key, or the first node if no such node exists.
Searching descends internal containers until a leaf container is reached.

When a container is full, it's split into two containers, where the new container gets the upper half of nodes and a B-tree node with the first key of the new container is inserted in the parent container.
When a node is deleted and the container contains less than (capacity + 1) / 2 nodes, nodes are stolen from or merged with the next or previous container.

### Reading a file

1. Key = data from file object. If key is 0, the file is empty.
2. Find extent with key in extent B-tree.
3. Read blocks of extent starting at key.
4. Key = next from extent and repeat from step 2 until key is 0.
5. Stop reading when size of file is reached.

When blocks are added to the end of a file, the last extent is extended if the new blocks follow the last extent and extent has less than 65536 blocks.
Otherwise a new extent is created with prev set to key of last extent and next of last extent is set to key of new extent.

## Transaction blocks

Smart file system uses transactions to safely write changes to administration blocks. Data blocks are not part of transactions.
Block root object container + 2 is the transaction block, which contains either a transaction ok block or a transaction failure block.

Changes are committed in following order:
1. Write compressed changes to transaction storage blocks, which are free blocks not marked used in bitmap.
2. Write transaction failure block pointing to first transaction storage block.
3. Write changed blocks.
4. Write transaction ok block.

When a partition is mounted and the transaction block is a transaction failure block, the transaction stored in transaction storage blocks is loaded and reapplied before the transaction ok block is written.

### Transaction ok block

| Offset | Data type          | Name     | Comment                |
|--------|--------------------|----------|------------------------|
| 0x000  | ULONG              | Id       | = 0x54524F4B "TROK"    |
| 0x004  | ULONG              | Checksum |                        |
| 0x008  | BLCK               | OwnBlock | Root object container + 2 |
| 0x00C  | UBYTE * (bs - 12)  | Unused   | = 0                    |

### Transaction failure block

Transaction failure block (struct fsTransactionFailure):

| Offset | Data type | Name             | Comment                                 |
|--------|-----------|------------------|-----------------------------------------|
| 0x000  | ULONG     | Id               | = 0x54524641 "TRFA"                     |
| 0x004  | ULONG     | Checksum         |                                         |
| 0x008  | BLCK      | OwnBlock         | Root object container + 2               |
| 0x00C  | BLCK      | FirstTransaction | First transaction storage block         |

### Transaction storage block

Transaction storage block (struct fsTransactionStorage):

| Offset | Data type          | Name     | Comment                                        |
|--------|--------------------|----------|------------------------------------------------|
| 0x000  | ULONG              | Id       | = 0x54525354 "TRST"                            |
| 0x004  | ULONG              | Checksum |                                                |
| 0x008  | BLCK               | OwnBlock |                                                |
| 0x00C  | BLCK               | Next     | Next transaction storage block, 0 if last      |
| 0x010  | UBYTE * (bs - 16)  | Data     | Transaction data                               |

Transaction data from all transaction storage blocks is a continuous stream of operations, where operations can span transaction storage blocks.

Operation (struct OperationInformation):

| Offset | Data type     | Name   | Comment                                                                                        |
|--------|---------------|--------|------------------------------------------------------------------------------------------------|
| 0x000  | UWORD         | Length | Length of compressed data in bytes, 0 indicates end of operations                              |
| 0x002  | BLCK          | Block  | Block changed by operation                                                                     |
| 0x006  | UBYTE         | Bits   | Bit 0 (OI_EMPTY) = block has no original and starts as zeroes. Bit 1 (OI_DELETE) = block doesn't need to be written |
| 0x007  | UBYTE * n     | Data   | Compressed data                                                                                |

Each operation uses (length | 1) + 7 bytes, so the next operation starts at an even offset.

The new content of a block is the original block (or zeroes if OI_EMPTY is set) with the compressed data applied.
Compressed data is a sequence of codes, where each code byte is followed by n = (code & 0x3F) + 1 longs:

| Code     | Comment                                      |
|----------|----------------------------------------------|
| 00xxxxxx | Set n longs to 0x00000000                    |
| 01xxxxxx | Skip n longs, unchanged                      |
| 10xxxxxx | Set n longs to 0xFFFFFFFF                    |
| 11xxxxxx | Copy n longs following code byte             |

## Recycled directory

Volumes with ROOTBITS_RECYCLED bit set have a recycled directory ".recycled" in root directory with object node 2.
Recycled directory has no hash table, has bits OTYPE_DIR, OTYPE_UNDELETABLE and OTYPE_QUICKDIR set (and OTYPE_HIDDEN unless shown) and protection 0x0C (read and write allowed).
Smart file system moves deleted files to recycled directory and updates deleted files and deleted blocks in root info.

## Formatted partition layout

A partition formatted with smart file system has following layout, where R = max(reserved blocks, 1), E = max(pre alloc blocks, 1) and n = number of bitmap blocks:

| Block                     | Content                                                                                         |
|---------------------------|-------------------------------------------------------------------------------------------------|
| 0                         | Root block                                                                                      |
| 1 to R - 1                | Reserved                                                                                        |
| R                         | Admin space container block with one admin space at block R with bits 0xFE000000 (7 blocks used) |
| R + 1                     | Root object container block with root directory object and root info                           |
| R + 2                     | Root directory hash table block with recycled directory in hash chain                           |
| R + 3                     | Transaction ok block                                                                            |
| R + 4                     | Extent B-tree root container block, empty leaf container                                        |
| R + 5                     | Object node tree root container block, leaf container with node number 1                        |
| R + 6                     | Recycled directory object container block                                                       |
| R + 7 to R + 31           | Free admin space blocks                                                                         |
| R + 32 to R + 32 + n - 1  | Bitmap blocks                                                                                   |
| R + 32 + n to total - E - 1 | Free blocks                                                                                   |
| total - E to total - 2    | Reserved                                                                                        |
| total - 1                 | Second root block                                                                               |

Blocks 0 to R + 32 + n - 1 and the last E blocks are marked used in bitmap.
Free blocks = total blocks - R - 32 - n - E.
