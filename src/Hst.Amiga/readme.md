# Hst Amiga

Hst Amiga is a library for reading and writing Amiga partition table, file systems and data types.

Features:
- Data types:
  - Disk objects: Read and write icons and tool types. Supports planar, new icon and color icon (glow icon) images, and true color icons (PNG icons).
  - UAEFSDBs: Read and write UAEFSDB metadata files created by WinUAE.
  - UAE metafiles: Read and write UAE metafiles created by FS-UAE.
  - Hunk files: Read hunk executables and resident structures (romtags).
  - IFF: Write interleaved bitmap (ILBM) images.
- Rigid Disk Block (RDB), Amiga partition table:
  - Read and write rigid disk block, partitions, file systems and bad blocks.
  - Recover a broken rigid disk block by fixing it or rebuilding it from file systems found on the disk.
  - Move and resize partitions with FFS, PFS3 and SFS file systems and expand or shrink rigid disk block within the size of the disk.
- File systems:
  - Read, write and format Fast File System (FFS) partitions and adf images. Supports DOS types DOS0-7.
  - Read, write and format Professional File System 3 (PFS3) partitions. Supports DOS types PFS3, PDS3.
  - Read, write and format Smart File System (SFS) partitions. Supports DOS type SFS\0.
  - Doctors to check and repair FFS, PFS3 and SFS volumes, and salvage files from damaged volumes to a directory or another volume.
- Read version strings from `$VER` or the resident structure id string in Amiga files.
- Build EPROMs from Kickstart ROMs.

Supports .NET Standard 2.1, .NET 8, .NET 9 and .NET 10.

Running Amiga CLI executables in a virtual machine is available in the [Hst Amiga Vm](https://www.nuget.org/packages/Hst.Amiga.Vm) package.