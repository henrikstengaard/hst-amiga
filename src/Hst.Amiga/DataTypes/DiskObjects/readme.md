# Disk objects

Disk objects directory contains classes to read and write icon files for Amiga OS. Disk object is the structure used to store and manage icons for Amiga computers. Amiga OS icon files uses filename extension `.icon`. 

Icons contains information about icon size, size of window it opens for drawers and ToolTypes used to specify parameters used by a program.

The code is inspired by following links:
- https://wiki.amigaos.net/wiki/Icon_Library
- http://www.evillabs.net/index.php/Amiga_Icon_Formats
- https://wiki.amigaos.net/wiki/AmigaOS_Manual:_Workbench_Fundamentals#Icon_Tool_Types

## Icon image types

A disk object can contain following icon image types:
- Planar
- New icon
- Color icon (also called Glow icons)

### Planar icon images

Planar icon images are standard AmigaOS Workbench icons, which were simple and limited to four colors.
Unlike standard images, planar icon images only includes a palette index per pixel and uses the palette defined in AmigaOS.

Planar icon images encodes the icon data in bit planes like the IFF file format.

### New icon images

NewIcons was introduced to solve the limitations of standard AmigaOS Workbench icons containing RGB color information in the icon file itself. It's uses a memory-resident program (called a Commodity), which tries to adapt the icon's colors into the current Workbench screen palette.

New icons standard size is 36×40 pixels and supports icons sizes up to 93x93 pixels with up to 256 colors.

New icons encodes the icon data in a 7-bit encoding in ASCII and stores it's palette colors as 8-bit data and the image data is encoded in the number of bits needed to address the color map index.
The encoded new icon data is stored as text in the icons Tool Type metadata after the line `*** DON'T EDIT THE FOLLOWING LINES!! ***`.
This makes new icon images relatively large in file size compared to conventional Amiga icons.

The use of ASCII encoding is seen as inefficient and improper use of the icons Tool Type metadata.

### Color icon images

AmigaOS 3.5 introduced Color icons (also called GlowIcons) and supported New icons without need of third-party applications.

The standard icon size is 46×46 pixels with maximum 256 (8-bit) colors.
New icons data is stored as an extension to standard AmigaOS by adding after the disk object data.

Color icons are based on the more general IFF file format and is the native icon format used in AmigaOS 3.5, 3.9 and 4.0.

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