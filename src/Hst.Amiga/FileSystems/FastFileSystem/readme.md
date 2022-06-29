# Fast File System

Fast File System directory contains classes to read and write Amiga Fast File System (FFS).

FFS uses following blocks:
- Root block: Root blocks contain volume name, etc. and links to other blocks.
- Bitmap block: Bitmap blocks contain information about free and allocated blocks.
- Bitmap extension block: Bitmap extension blocks contain additional bitmap blocks.
- Dir cache block: Dir cache blocks contains records for caching and improving reading directories.
- Entry block: Entry blocks contains directory and file entries.
- File ext block: File extension blocks contain additional file entry information.
- Data block: Data blocks contains data for entry blocks.

## References

The Fast File System classes are written with inspiration from following references:
- http://lclevy.free.fr/adflib/adf_info.html by Laurent Clévy.
- https://github.com/lclevy/ADFlib by Laurent Clévy.
- https://en.wikipedia.org/wiki/Amiga_Fast_File_System.

## Examples
