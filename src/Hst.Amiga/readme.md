# Hst Amiga

Hst Amiga is a library for reading and writing Amiga partition table, file systems and data types.

Features:
- Data types
    - Read, write disk objects containing icons and tool types. Supports planer, new icon and color icon images.
- Read and write Rigid Disk Block (RDB), Amiga partition table.
- Move and resize partitions with FFS, PFS3 and SFS file systems and expand or shrink Rigid Disk Block within the size of the disk.
- File systems
    - Read, write and format Fast File System (FFS) partitions. Supports DOS types DOS0-7.
    - Read, write and format Professional File System 3 (PFS3) partitions. Supports DOS types PFS3, PDS3.
    - Read, write and format Smart File System (SFS) partitions. Supports DOS type SFS\0.
- Read version strings.