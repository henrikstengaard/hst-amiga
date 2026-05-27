# TrueColor icons

True color icons (also known as PowerIcons) are 24-bit RGB or 32-bit RGBA PNG icons for Amiga Workbench.
24-bit RBG PNG icons doesn't have alpha channel and is therefore not transparent and 32-bit RGBA PNG icons have an alpha channel and are transparent.
True color icons does not support grayscale, interlaced or palette based images.

True color icons can contain 1 or 2 PNG images appended after each other. First PNG image is the render icon shown in Workbench and the second PNG image is the select icon shown when selected.

## Requirements

True color icons requires PeterK icon library or PowerIcons is installed. PeterK icon library is the preferred choice as is the most updated library with the broadest support for various icon formats.

## File header

A true color icon starts with an 8 bytes PNG signature:
```
89 50 4E 47 0D 0A 1A 0A
```

The PNG signature begins with the byte `89` is used to reduce the chance of the the file being mistaken as a text file.
The bytes `50 4E 47` represent the ASCII text `PNG` to easily identify format using a text viewer.
The bytes `0D 0A` is a Windows/Dos style line ending (CRLF) to detect DOS/Unix line ending.
The byte `1A` is a end-of-file byte displaying the file when the command `type` is used.
The byte `0A` is a Unix style line ending (LF) to detect DOS/Linux line ending.

## Chunks

The true color icon contains a series of chunks after the file header.
declare themselves as critical or ancillary, and a program encountering an ancillary chunk that it does not understand can safely ignore it.
The chunk based structure is similar to Amiga IFF and is designed to allow the PNG format to be extended while maintaining compatibility with older versions making it forward compatible.

A chunk consists of the following:

| Size     | Type   | Description                                                                                                                  |
|----------|--------|------------------------------------------------------------------------------------------------------------------------------|
| 4        | Length | Length of chunk data (big-endian).                                                                                           |
| 4        | Type   | Chunk type/name.                                                                                                             |
| *Length* | Data   | Chunk data of length bytes.                                                                                                  |
| 4        | CRC    | Cyclic redundancy code/checksum, a network-byte-order CRC-32 computed over the chunk type and chunk data, but not the length |

Chunk types are given a four-letter case sensitive ASCII type/name.
The case of the different letters in the name (bit 5 of the numeric value of the character) is a bit field that provides the decoder with some information on the nature of chunks it does not recognize:
- Case of first letter: Indicates whether the chunk is critical or not. If the first letter is uppercase, the chunk is critical. If not, the chunk is ancillary.
- Case of second letter: Indicates whether the chunk is "public" (standard) or "private" (non-standard/custom).
- Case of third letter: Must be uppercase to conform to the PNG specification. It is reserved for future expansion.
- Case of fourth letter: Indicates whether the chunk is safe to copy by editors that do not recognize it. If lowercase, the chunk may be safely copied regardless of modifications to the file. If uppercase, it may only be copied if the modifications have not touched any critical chunks.

True color PNG icons has the following chunks:
- `IHDR`: Contains width, height and bit depth. It must be the first chunk.
- `IDAT`: Contains image data
- `icOn`: Contains Amiga icon type, icon position, drawer position and drawer size.
- `IEND`: Marks image end.

All other PNG chunks are ignored and an incorrect PNG image can cause a fallback to default icons.

### IHDR chunk

The `IHDR` chunk is a 13 byte header chunk with following structure:

| Offset | Size | Description                                                    |
|--------|------|----------------------------------------------------------------|
| 0x0    | 4    | Width                                                          |
| 0x4    | 4    | Height                                                         |
| 0x8    | 1    | Bit depth. Values 1, 2, 4, 8, or 16.                           |
| 0x9    | 1    | Color type. Values 0, 2, 3, 4, or 6.                           |
| 0xa    | 1    | Compression method. Value 0.                                   |
| 0xb    | 1    | Filter method. Value 0.                                        |
| 0xc    | 1    | Interlace mode. values 0 "no interlace" or 1 "Adam7 interlace" |                     

Color types:
- 0: Grayscale.
- 2: True color / RGB.
- 3: Indexed color using palette.
- 4: Grayscale with alpha.
- 6: True color with alpha / RGBA.

### IDAT chunk

The `IDAT` chunk contains image data as a stream of the compression algorithm.
PNG uses DEFLATE, a non-patented lossless data compression algorithm involving a combination of LZ77 and Huffman coding. Permissively licensed DEFLATE implementations, such as zlib, are widely available.

### icOn chunk

The `icOn` chunk is a non-standard PNG chunk that contains Amiga icon information.
A normal Amiga icon contains a disk object structure with icon information followed by a color icon.
Since true color icons are PNG, the icon chunk contains a series of icon attribute tags and a value to represent the same kind of Amiga icon information.

Icon attribute tags in the `icOn` chunk has the following structure:

| Size     | Type  | Description          |
|----------|-------|----------------------|
| 4        | Tag   | Icon attribute tag.  |
| 4        | Value | Tag value of 4 bytes |

True color PNG icons can contain following icon attribute tags:

| Icon attribute tag | Tag bytes  | Description                                                               |
|--------------------|------------|---------------------------------------------------------------------------|
| ATTR_ICONX         | 0x80001001 | X position of icon.                                                       |
| ATTR_ICONY         | 0x80001002 | Y position of icon.                                                       |
| ATTR_DRAWERX       | 0x80001003 | X position of drawer.                                                     |
| ATTR_DRAWERY       | 0x80001004 | Y position of drawer.                                                     |
| ATTR_DRAWERWIDTH   | 0x80001005 | Width of drawer.                                                          |
| ATTR_DRAWERHEIGHT  | 0x80001006 | Height of drawer.                                                         |
| ATTR_DRAWERFLAGS   | 0x80001007 | Flags for drawer.                                                         |
| ATTR_TOOLWINDOW    | 0x80001008 | OS4: STRPTR, tool window string, length including the tag, multiple of 8. |
| ATTR_STACKSIZE     | 0x80001009 | Stack size.                                                               |
| ATTR_DEFAULTTOOL   | 0x8000100a | STRPTR, default tool.                                                     |
| ATTR_TOOLTYPE      | 0x8000100b | STRPTR, tool type.                                                        |
| ATTR_VIEWMODES     | 0x8000100c | OS4 PNG use it.                                                           |
| ATTR_DD_CURRENTX   | 0x8000100d | OS4 ULONG, drawer view X offset.                                          |
| ATTR_DD_CURRENTY   | 0x8000100e | OS4 ULONG, drawer view Y offset.                                          |
| ATTR_TYPE          | 0x8000100f | OS4 icon type (WBDISK...WBKICK).                                          |
| ATTR_FRAMELESS     | 0x80001010 | OS4 ULONG, frameless property.                                            |
| ATTR_DRAWERFLAGS3  | 0x80001011 | OS4 ULONG, drawer flags.                                                  |
| ATTR_VIEWMODES2    | 0x80001012 | OS4 ULONG, drawer view modes.                                             |
| ATTR_DRAWERFLAGS2  | 0x80001107 | DOpus Magellan settings.                                                  |

Drawer related icon attribute tags are only present for icon types DISK, DRAWER and GARBAGE.

### IEND chunk

The `IEND` chunk marks the image end. The data field of the IEND chunk is empty and has 0 bytes.

## References

- https://en.wikibooks.org/wiki/Aros/Developer/Docs/Libraries/Icon
