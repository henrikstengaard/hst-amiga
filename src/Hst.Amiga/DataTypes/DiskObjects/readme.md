# Disk objects

Disk objects directory contains classes to read and write icon files for Amiga OS. Disk object is the structure used to store and manage icons for Amiga computers. Amiga OS icon files uses filename extension `.icon`. 

Icons contains information about icon size, size of window it opens for drawers and ToolTypes used to specify parameters used by a program.

The code is inspired by following links:
- https://wiki.amigaos.net/wiki/Icon_Library
- http://www.evillabs.net/index.php/Amiga_Icon_Formats
- https://wiki.amigaos.net/wiki/AmigaOS_Manual:_Workbench_Fundamentals#Icon_Tool_Types

## Icon file structure

The format of the Amiga .info file is as follows:

DiskObject header 78 bytes
Optional DrawerData header 56 bytes
First icon header 20 bytes
First icon data Varies
Second icon header 20 bytes
Second icon data Varies

The DiskObject header contains, among other things, the magic number (0xE310), the object width and height (inside the embedded Gadget header), and the version.

Each icon header contains the icon width and height, which can be smaller than the object width and height, and the number of bit-planes.

The icon data has the following format:

BIT-PLANE planes, each with HEIGHT rows of (WIDTH
+15) / 16 * 2 bytes length.

So if you have a 9x3x2 icon, the icon data will look like this:

aaaa aaaa a000 0000
aaaa aaaa a000 0000
aaaa aaaa a000 0000
bbbb bbbb b000 0000
bbbb bbbb b000 0000
bbbb bbbb b000 0000

where a is a bit for the first bit-plane, b is a bit for the second bit-plane, and 0 is padding.