

PeterK icon library.

</font><font color="#FFFFFF" class="agshine">The support for PNG icons is limited to RGB and RGBA.</font><font color="#000000" class="agtext">
Only these two TrueColor formats are decoded, <b>but no</b>
<b>grayscale or interlaced or PLTE palette based images.</b>
<b>Only the chunks IHDR, IDAT, icOn and IEND are parsed</b>,
all other PNG chunks are skipped over and ignored. A
wrong image type causes a fallback to default icons.
</pre>


https://en.wikibooks.org/wiki/Aros/Developer/Docs/Libraries/Icon

png images with special ic0n chunk embedded inside png. can be 1 or 2 png images.

ic0n chunk stores the 'old' data required to fill f.e. a drawerdata structure.

it is a taglist that can contain zero or more tags from the following list:

ATTR_DRAWERX,
ATTR_DRAWERY,
ATTR_DRAWERWIDTH,
ATTR_DRAWERHEIGHT,
ATTR_DRAWERFLAGS,
ATTR_DRAWERFLAGS2,
ATTR_DRAWERFLAGS3,
ATTR_VIEWMODES,
ATTR_VIEWMODES2,
ATTR_DD_CURRENTX,
ATTR_DD_CURRENTY :
ATTR_ICONX,
ATTR_ICONY,
ATTR_STACKSIZE,
ATTR_TYPE,
ATTR_FRAMELESS:
ATTR_DEFAULTTOOL,
ATTR_TOOLTYPE: