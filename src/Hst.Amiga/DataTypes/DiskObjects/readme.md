# Disk objects

Disk objects directory contains classes to read and write icon files for Amiga OS. Disk object is the structure used to store and manage icons for Amiga computers. Amiga OS icon files uses filename extension `.icon`. 

Icons contain information about icon size, size of window it opens for drawers and ToolTypes used to specify parameters used by a program.

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

The use of ASCII encoding is seen as inefficient and misuse of the icons Tool Type metadata.

### Color icon images

AmigaOS 3.5 introduced Color icons (also called GlowIcons) and supported New icons without need of third-party applications.

The standard icon size is 46×46 pixels with maximum 256 (8-bit) colors.
New icons data is stored as an extension to standard AmigaOS by adding a new data block appended at the end of the icon file.

Color icons are based on the more general IFF file format and is the native icon format used in AmigaOS 3.5, 3.9 and 4.0.

## Disk object file

The format of the Amiga disk object .info file is as follows:

| Type               | Size     | Description                                                                     |
|--------------------|----------|---------------------------------------------------------------------------------|
| DiskObject header  | 78 bytes |                                                                                 |
| DrawerData header  | 56 bytes | Present, if drawer data pointer value is not 0.                                 |
| First icon header  | 20 bytes | Present, if gadget render pointer value is not 0.                               |
| First icon data    | Varies   | Present, if gadget render pointer value is not 0.                               |
| Second icon header | 20 bytes | Present, if select render pointer value is not 0.                               |
| Second icon data   | Varies   | Present, if select render pointer value is not 0.                               |
| Default tool data  | Varies   | Present, if default tool pointer value is not 0.                                |
| Tool types data    | Varies   | Present, if tool types pointer value is not 0.                                  |
| Drawer data 2      | Varies   | Present, if drawer data pointer value is not 0 and it has gadget user data = 1. |

### Data types

A note about the used data descriptors. All elements are in Motorola byte order (the highest byte first):

| Type  | Description                                                   | Range                   |
|-------|---------------------------------------------------------------|-------------------------|
| APTR  | Memory pointer (usually this gets a boolean meaning on disk). | 0..4294967295           |           
| BYTE  | Single byte.                                                  | -128..127               | 
| UBYTE | Unsigned byte.                                                | 0..255                  |
| WORD  | Signed 16 bit value.                                          | -32768..32767           |
| UWORD | Unsigned 16 bit value.                                        | 0..65535                |
| LONG  | Signed 32 bit value.                                          | -2147483648..2147483647 |
| ULONG | Unsigned 32 bit value.                                        | 0..4294967295           |

### Disk object structure

Disk object has the following structure:

| Offset | Type   | Size | Field          | Description                                                                                                      |
|--------|--------|------|----------------|------------------------------------------------------------------------------------------------------------------|
| 0x0    | UWORD  | 2    | do_Magic       | Magic bytes, always 0xE310.                                                                                      |
| 0x2    | UWORD  | 2    | do_Version     | Version, always 1.                                                                                               |
| 0x4    | struct | 2    | do_Gadget      | Gadget structure.                                                                                                |
| 0x30   | UBYTE  | 1    | do_Type        | Type of icon.                                                                                                    |
| 0x31   | UBYTE  | 1    | -              | Padding, always 0.                                                                                               |
| 0x32   | APTR   | 4    | do_DefaultTool | Pointer to default tool. Boolean: 1..4294967295 = has default tool, 0 = no default tool.                         |
| 0x36   | APTR   | 4    | do_ToolTypes   | Pointer to tool types. Boolean: 1..4294967295 = has tool types, 0 = no tool types.                               |
| 0x3a   | LONG   | 4    | do_CurrentX    | Current x position of icon.                                                                                      |
| 0x3e   | LONG   | 4    | do_CurrentY    | Current y position of icon,                                                                                      |
| 0x42   | APTR   | 4    | do_DrawerData  | Pointer to drawer data. Boolean: 1..4294967295 = has drawer data, 0 = no drawer data.                            |
| 0x46   | APTR   | 4    | do_ToolWindow  | Pointer to tool window. Boolean: 1..4294967295 = has tool window, 0 = no tool window. **Only applies to tools.** |
| 0x4a   | LONG   | 4    | do_StackSize   | Stack size for program execution (values < 4096 mean 4096 is used). **Only applies to tools.**                   |

Type of icon is defined by the `do_Type` field in the DiskObject structure and can have the following values:

| Type | Name    | Description                                  |
|------|---------|----------------------------------------------|
| 1    | DISK    | A disk.                                      |
| 2    | DRAWER  | A directory.                                 |
| 3    | TOOL    | A program.                                   |
| 4    | PROJECT | A project file with defined program to start |
| 5    | GARBAGE | The trashcan.                                |
| 6    | DEVICE  | Should never appear.                         |
| 7    | KICK    | A kickstart disk.                            |
| 8    | APPICON | Should never appear.                         |

### Gadget structure

Gadget has the following structure:

| Offset | Type  | Size | Field            | Description                                                              |
|--------|-------|------|------------------|--------------------------------------------------------------------------|
| 0x0    | APTR  | 4    | ga_NextGadget    | <undefined> always 0.                                                    |
| 0x4    | WORD  | 2    | ga_LeftEdge      | unused ???.                                                              |
| 0x6    | WORD  | 2    | ga_TopEdge       | unused ???.                                                              |
| 0x8    | WORD  | 2    | ga_Width         | the width of the gadget.                                                 |
| 0xa    | WORD  | 2    | ga_Height        | the height of the gadget.                                                |
| 0xc    | UWORD | 2    | ga_Flags         | gadget flags.                                                            |
| 0xe    | UWORD | 2    | ga_Activation    | <undefined>.                                                             |
| 0x10   | UWORD | 2    | ga_GadgetType    | <undefined>.                                                             |
| 0x12   | APTR  | 4    | ga_GadgetRender  | <boolean> unused??? always true.                                         |
| 0x16   | APTR  | 4    | ga_SelectRender  | <boolean> (true if second image present).                                |
| 0x1a   | APTR  | 4    | ga_GadgetText    | <undefined> always 0 ???.                                                |
| 0x1e   | LONG  | 4    | ga_MutualExclude | <undefined>.                                                             |
| 0x22   | APTR  | 4    | ga_SpecialInfo   | <undefined>.                                                             |
| 0x26   | UWORD | 2    | ga_GadgetID      | <undefined>.                                                             |
| 0x28   | APTR  | 4    | ga_UserData      | lower 8 bits:  0 for old, 1 for icons >= OS2.x upper 24 bits: undefined. |

Gadget flags are defined by the `ga_Flags` field in the Gadget structure and can have the following values:

| Bit | Description                                       |
|-----|---------------------------------------------------|
| 0   | if set we use backfill mode, else complement mode |
| 1   | if set, we use 2 image-mode                       |
| 2   | always set (image 1 is an image ;-)               |

complement mode: gadget colors are inverted
backfill mode: like complement, but region outside (color 0) of image is not inverted

### NewWindow structure

NewWindow structure used for DrawerData has the following structure:

| Offset | Type  | Size | Field          | Description                                                                                          |
|--------|-------|------|----------------|------------------------------------------------------------------------------------------------------|
| 0x00   | WORD  | 2    | nw_LeftEdge    | Left edge distance (x position) of window.                                                           |
| 0x02   | WORD  | 2    | nw_TopEdge     | Top edge distance (y position) of window.                                                            |
| 0x04   | WORD  | 2    | nw_Width       | The width of the window.                                                                             |
| 0x06   | WORD  | 2    | nw_Height      | The height of the window.                                                                            |
| 0x08   | UBYTE | 1    | nw_DetailPen   | Use for bar/border/gadget rendering, always 255.                                                     |
| 0x09   | UBYTE | 1    | nw_BlockPen    | Use for bar/border/gadget rendering, always 255.                                                     |
| 0x0A   | ULONG | 4    | nw_IDCMPFlags  | User-selected IDCMP flags, always 0.                                                                 |
| 0x0E   | ULONG | 4    | nw_Flags       | Flags for window.                                                                                    |
| 0x12   | APTR  | 4    | nw_FirstGadget | Linked-list of gadgets for the window, always 0.                                                     |
| 0x16   | APTR  | 4    | nw_CheckMark   | CheckMark is a pointer to imagery used when rendering menu items of window, always 0.                |
| 0x1A   | APTR  | 4    | nw_Title       | Title text for this window, alwaus 0.                                                                |
| 0x1E   | APTR  | 4    | nw_Screen      | Screen pointer is used when defining a CUSTOMSCREEN, always 0.                                       |
| 0x22   | APTR  | 4    | nw_BitMap      | Pointer to bitmap for WFLG_SUPER_BITMAP, always 0.                                                   |
| 0x26   | WORD  | 2    | nw_MinWidth    | Minimum window width, often 94.                                                                      |
| 0x28   | WORD  | 2    | nw_MinHeight   | Minimum window height, often 65.                                                                     |
| 0x2A   | UWORD | 2    | nw_MaxWidth    | Maximum window width, often 0xFFFF.                                                                  |
| 0x2C   | UWORD | 2    | nw_MaxHeight   | Maximum window width, often 0xFFFF.                                                                  |
| 0x2E   | UWORD | 2    | nw_Type        | Type of screen the window opens, can be CUSTOMSCREEN or one of other screen types like WBENCHSCREEN. |

NewWindow flags are defined by the `nw_Flags` field in the NewWindow structure and can have the following values:

| Value      | Flag                 | Description                                      |
|------------|----------------------|--------------------------------------------------|
| 0x1        | WFLG_SIZEGADGET      | Include sizing system-gadget?                    |
| 0x2        | WFLG_DRAGBAR         | Include dragging system-gadget?                  |
| 0x4        | WFLG_DEPTHGADGET     | Include depth arrangement gadget?                |
| 0x8        | WFLG_CLOSEGADGET     | Include close-box system-gadget?                 |
| 0x10       | WFLG_SIZEBRIGHT      | Size gadget uses right border                    |
| 0x20       | WFLG_SIZEBBOTTOM     | Size gadget uses bottom border                   |
| 0xc0       | WFLG_REFRESHBITS     | Refresh modes, refresh bits.                     |
| 0x0        | WFLG_SMART_REFRESH   | Refresh modes, smart refresh.                    |
| 0x40       | WFLG_SIMPLE_REFRESH	 | Refresh modes, simple refresh.                   |
| 0x80       | WFLG_SUPER_BITMAP	   | Refresh modes, super bitmap.                     |
| 0xc0       | WFLG_OTHER_REFRESH   | Refresh modes, other refresh.                    |
| 0x100      | WFLG_BACKDROP        | This is a backdrop window.                       |
| 0x200      | WFLG_REPORTMOUSE     | To hear about every mouse move.                  |
| 0x400      | WFLG_GIMMEZEROZERO   | A GimmeZeroZero window, make extra border stuff. |
| 0x800      | WFLG_BORDERLESS      | To get a Window sans border.                     |
| 0x1000     | WFLG_ACTIVATE        | When Window opens, it's active.                  |
| 0x2000     | WFLG_WINDOWACTIVE    | This window is the active one.                   |
| 0x4000     | WFLG_INREQUEST       | This window is in request mode.                  |
| 0x8000     | WFLG_MENUSTATE       | Window is active with menus on.                  |
| 0x10000    | WFLG_RMBTRAP         | Catch RMB events for your own.                   |
| 0x20000    | WFLG_NOCAREREFRESH   | Not to be bothered with REFRESH.                 |
| 0x1000000  | WFLG_WINDOWREFRESH   | Window is currently refreshing                   |
| 0x2000000  | WFLG_WBENCHWINDOW    | WorkBench tool ONLY Window                       |
| 0x4000000  | WFLG_WINDOWTICKED    | Only one timer tick at a time                    |
| 0x40000    | WFLG_NW_EXTENDED     | Extension data provided.                         |
| 0x8000000  | WFLG_VISITOR         | Visitor window, used by Intuition                |
| 0x10000000 | WFLG_ZOOMED          | Identifies "zoom state", used by Intuition       |
| 0x20000000 | WFLG_HASZOOM         | Window has a zoom gadget, used by Intuition      |

### Drawer data structure

Drawer data structure used for DrawerData has the following structure:

| Offset | Type  | Size | Field        | Description                                                                                                     |
|--------|-------|------|--------------|-----------------------------------------------------------------------------------------------------------------|
| 0x00   | ULONG | 4    | dd_Flags     | Flags for drawer display.                                                                                       |
| 0x04   | WORD  | 2    | dd_ViewModes | View modes of drawer display, 0 = handle viewmode like parent drawer current setting (OS1.x compatibility mode) |

Drawer data flags are defined by the `dd_Flags` field in the drawer data structure and can have the following values:

| Bit | Description                                          |
|-----|------------------------------------------------------|
| 0   | View icons.                                          |
| 1   | View all files (bit 0 maybe set or unset with this). |

View modes are defined by the `dd_ViewModes` field in the drawer data structure and can have the following values:

| Value | Description                            |
|-------|----------------------------------------|
| 0     | Show icons (OS1.x compatibility mode). |
| 1     | Show icons.                            |
| 2     | Show sorted by name.                   |
| 3     | Show sorted by date.                   |
| 4     | Show sorted by size.                   |
| 5     | Show sorted by type.                   |

### Image header structure

Each image header contains the icon width and height, which can be smaller than the object width and height, and the number of bit-planes.

Icon header structure used for icon images has the following structure:

| Offset | Type  | Size | Field         | Description                                                          |
|--------|-------|------|---------------|----------------------------------------------------------------------|
| 0x00   | WORD  | 2    | im_LeftEdge   | Left edge distance (x position) of icon image, always 0.             |
| 0x02   | WORD  | 2    | im_TopEdge    | Top edge distance (y position) of icon image, always 0.              |
| 0x04   | WORD  | 2    | im_Width      | The width of the icon image.                                         |
| 0x06   | WORD  | 2    | im_Height     | The height of the icon image.                                        |
| 0x08   | WORD  | 2    | im_Depth      | The icon image bitmap depth.                                         |
| 0x0A   | APTR  | 4    | im_ImageData  | Pointer to image data, always 1.                                     |
| 0x0E   | UBYTE | 1    | im_PlanePick  | Foreground color register index, used for new icons and color icons. |
| 0x0F   | UBYTE | 1    | im_PlaneOnOff | Background color register index, used for new icons and color icons. |
| 0x10   | APTR  | 4    | im_Next       | Pointer to next image, always 0.                                     |

This is followed by the image data in planar mode. The width of the
image is always rounded to next 16bit boundary.

### Image data structure

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

## References

List of references:
- Disk object structure: https://amigadev.elowar.com/read/ADCD_2.1/Includes_and_Autodocs_3._guide/node05D6.html#line64
- Gadget structure: https://amigadev.elowar.com/read/ADCD_2.1/Libraries_Manual_guide/node0149.html
- NewWindow structure: https://amigadev.elowar.com/read/ADCD_2.1/Includes_and_Autodocs_2._guide/node00D4.html#line976