# Color icons

## Compression

Compressed color icons uses RLE compression.

copy block
repeat block

##

if (debug)
puts("coloriconencode");

if (imgwidth > 256 || imgheight > 256)
{
printf("\nNew icon is too large: %dx%d (max is 256 wide, 256 high)\n", imgwidth, imgheight);
return img;
// exit(1);
}

if (num == 1)
{
// if we're writing the first image,
// we also need the preceding FORM and FACE chunks
*(img++) = 'F';
*(img++) = 'O';
*(img++) = 'R';
*(img++) = 'M';
// size (need to update this later!)
e = img - icondata;
*(img++) = (e >> 24) & 0xff;
*(img++) = (e >> 16) & 0xff;
*(img++) = (e >> 8) & 0xff;
*(img++) = e  & 0xff;
*(img++) = 'I';
*(img++) = 'C';
*(img++) = 'O';
*(img++) = 'N';
*(img++) = 'F';
*(img++) = 'A';
*(img++) = 'C';
*(img++) = 'E';
// size = 6
*(img++) = 0;
*(img++) = 0;
*(img++) = 0;
*(img++) = 6;
*(img++) = imgwidth - 1;
*(img++) = imgheight - 1;
// flags, bit 0 = frameless (i.e. 1 = no border)
if (border & 2)
*(img++) = 0;
else
*(img++) = 1;
// aspect ratio in higher/lower nybble
*(img++) = 0x11;
// maximum palette - 1
if (ncolors[1] > ncolors[2])
e = ncolors[1] * 4 - 1;
else
e = ncolors[2] * 4 - 1;
*(img++) = (e >> 8) & 0xff;
*(img++) = e & 0xff;
// fixed size, no need to pad 1 byte if odd
}

// IMAG chunk (is this an actual IFF chunk??)
*(img++) = 'I';
*(img++) = 'M';
*(img++) = 'A';
*(img++) = 'G';
// size, 0 for now
imaglen = img;
*(img++) = 0;
*(img++) = 0;
*(img++) = 0;
*(img++) = 0;
// transparent color
*(img++) = 0;
if (num == 1)
{
// number of colors in palette
*(img++) = ncolors[num] - 1;
// flags, bit 0 = transparent, bit 1 = palette included
if (transpguess != 0)
*(img++) = 3;
else
*(img++) = 2;
}
else
{
// number of colors in palette
if (ncolors[2] > 1)
{
*(img++) = ncolors[num] - 1;
if (transpguess != 0)
*(img++) = 3;
else
*(img++) = 2;
}
else
{
*(img++) = 0;
if (transpguess != 0)
*(img++) = 1;
else
*(img++) = 0;
}
}
// image format, bit 0 = compressed
*(img++) = compress;
// palette format, bit 0 = compressed
*(img++) = 0;
// image depth
*(img++) = imgdepth;
// image size in bytes - 1
imaglen2 = img;
*(img++) = 0;
*(img++) = 0;
// palette size in bytes - 1
palettelen = img;
*(img++) = 0;
*(img++) = 0;

// the image
imginit(pixels);
if (compress)
{
// code for compressed coloricon image
img = bitsinit(img);
while (imgleft(pixels))
{
rlesize = imgscan(pixels);
if (rlesize < 0)
{
rlesize = -rlesize;
img = bitsadd(img, 8, rlesize - 1);
for (i = 0; i < rlesize; i++)
img = bitsadd(img, imgdepth, imgpop(pixels));
}
else
{
img = bitsadd(img, 8, 257 - rlesize);
img = bitsadd(img, imgdepth, imgpop(pixels));
for (i = 1; i < rlesize; i++)
imgpop(pixels);
}
}
e = imgleft(pixels);
img = bitsfinish(img);
}
else
{
// code for uncompressed coloricon
e = imgwidth * imgheight;
for (i = 0; i < e; i++)
*(img++) = imgpop(pixels);
}

// image length
e = img - imaglen2 - 4 - 1;
imaglen2[0] = (e >> 8) & 0xff;
imaglen2[1] = e & 0xff;

// the palette
if (num == 1 || ncolors[num] > 1)
{
// without compression
palettelenimg = img;
for (x = 0; x < ncolors[num] * 3; x++)
*(img++) = p[x];
/*
// with compression, only worth it if more than half of
// the colors are grayscale, so disabled
for (x = 0; x < ncolors[num] * 3; x += 3)
{
if (p[x] == p[x + 1] && p[x] == p[x + 2])
{
*(img++) = 0xfe;
*(img++) = p[x];
}
else
{
*(img++) = 2;
*(img++) = p[x];
*(img++) = p[x + 1];
*(img++) = p[x + 2];
}
}
*/

      // palette length
      e = img - palettelenimg - 1;
      palettelen[0] = (e >> 8) & 0xff;
      palettelen[1] = e & 0xff;
    }

// update length of IMAG chunk
e = img - imaglen - 4;
imaglen[0] = (e >> 24) & 0xff;
imaglen[1] = (e >> 16) & 0xff;
imaglen[2] = (e >> 8) & 0xff;
imaglen[3] = e & 0xff;
// padding?
if (e & 1)
{
// if (debug)
// puts("IMAG chunk odd length, padding with a zero byte");
*(img++) = 0;
}

return img;
