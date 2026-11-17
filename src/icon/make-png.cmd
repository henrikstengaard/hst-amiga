:: https://imagemagick.org/script/download.php#windows

set path=%PATH%;c:\Program Files\ImageMagick-7.1.1-Q16-HDRI
magick -background none -density 384 hst-amiga.svg -trim +repage -resize 1024x1024 -gravity center -extent 1024x1024 PNG32:hst-amiga.png
magick hst-amiga.png -resize 512x512 PNG32:hst-amiga-512x512.png
magick hst-amiga.png -resize 128x128 PNG32:hst-amiga-128x128.png
pause
