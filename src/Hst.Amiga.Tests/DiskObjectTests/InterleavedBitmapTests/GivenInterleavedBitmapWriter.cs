using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hst.Amiga.DataTypes.InterleavedBitmaps;
using Hst.Core.Converters;
using Hst.Imaging;
using Xunit;

namespace Hst.Amiga.Tests.DiskObjectTests.InterleavedBitmapTests;

public class GivenInterleavedBitmapWriter
{
    [Fact]
    public async Task When_WritingUncompressed4BppImage_Then_BytesContainInterleavedBitmap()
    {
        // arrange
        var image = CreateImage();

        // act
        byte[] interleavedBitmapImageBytes;
        using (var memoryStream = new MemoryStream())
        {
            var writer  = new InterleavedBitmapWriter(memoryStream);
            await writer.Write(image, false);
            interleavedBitmapImageBytes = memoryStream.ToArray();
        }
        
        // assert
        var offset = 0;
        Assert.Equal(Encoding.ASCII.GetBytes("FORM"), interleavedBitmapImageBytes.Take(4));
        offset += 4;
        var formChunkSize = (int)BigEndianConverter.ConvertBytesToUInt32(interleavedBitmapImageBytes, offset);
        Assert.Equal(interleavedBitmapImageBytes.Length - 8, formChunkSize);
        offset += 4;
        
        // assert - ilbm chunk
        Assert.Equal(Encoding.ASCII.GetBytes("ILBM"), interleavedBitmapImageBytes.Skip(offset).Take(4));
        offset += 4;

        // assert - bitmap header chunk
        Assert.Equal(Encoding.ASCII.GetBytes("BMHD"), interleavedBitmapImageBytes.Skip(offset).Take(4));
        offset += 4;
        var bmhdChunkSize = (int)BigEndianConverter.ConvertBytesToUInt32(interleavedBitmapImageBytes, offset);
        offset += 4;
        var bmhdChunkBytes = interleavedBitmapImageBytes.Skip(offset).Take(bmhdChunkSize).ToArray();
        offset += bmhdChunkSize;

        const ushort width = 1;
        const ushort height = 1;
        const short x = 0;
        const short y = 0;
        const byte planes = 4;
        const byte mask = 0;
        const byte compress = 0;
        const byte pad1 = 0;
        const ushort transparentColor = 0;
        const byte xAspect = 60;
        const byte yAspect = 60;
        const ushort pageWidth = width;
        const ushort pageHeight = height;
        var expectedBitmapHeaderBytes = new byte[]
        {
            0, (byte)width,
            0, (byte)height, 
            0, (byte)x,
            0, (byte)y,
            planes,
            mask,
            compress,
            pad1,
            0, (byte)transparentColor,
            xAspect,
            yAspect,
            0, (byte)pageWidth,
            0, (byte)pageHeight
        };
        Assert.Equal(expectedBitmapHeaderBytes.Length, bmhdChunkBytes.Length);
        Assert.Equal(expectedBitmapHeaderBytes, bmhdChunkBytes);
        
        // assert - color map chunk
        Assert.Equal(Encoding.ASCII.GetBytes("CMAP"), interleavedBitmapImageBytes.Skip(offset).Take(4));
        offset += 4;
        var cmapChunkSize = (int)BigEndianConverter.ConvertBytesToUInt32(interleavedBitmapImageBytes, offset);
        offset += 4;
        var cmapChunkBytes = interleavedBitmapImageBytes.Skip(offset).Take(cmapChunkSize).ToArray();
        offset += cmapChunkSize;
        var expectedColorMapBytes = new byte[]
        {
            0, 0, 0, // black
            255, 0, 0, // red
        };
        Assert.Equal(expectedColorMapBytes.Length, cmapChunkBytes.Length);
        Assert.Equal(expectedColorMapBytes, cmapChunkBytes);

        // assert - body chunk
        Assert.Equal(Encoding.ASCII.GetBytes("BODY"), interleavedBitmapImageBytes.Skip(offset).Take(4));
        offset += 4;
        var bodyChunkSize = (int)BigEndianConverter.ConvertBytesToUInt32(interleavedBitmapImageBytes, offset);
        offset += 4;
        var bodyChunkBytes = interleavedBitmapImageBytes.Skip(offset).Take(bodyChunkSize).ToArray();
        offset += bodyChunkSize;

        // assert - calculated body chunk size matches
        var planeWidth = Math.Floor(((double)width + 15) / 16) * 16;
        var bytesPerRow = planeWidth / 8;
        var planeSize = bytesPerRow * height;
        var calculatedBodyChunkSize = (int)planeSize * planes;
        Assert.Equal(8, calculatedBodyChunkSize);
        
        // arrange - create expected body chunk bytes
        var expectedBodyChunkBytes = new byte[8];
        var pixel1x = 0;
        var pixel1y = 0;
        var pixel1Color = 1;
        var pixelOffset = Convert.ToInt32(pixel1y * bytesPerRow + (double)pixel1x / 8);
        var xmod = 7 - (pixel1x & 7);
        for (var plane = 0; plane < image.BitsPerPixel; plane++)
        {
            expectedBodyChunkBytes[pixelOffset] = (byte)(expectedBodyChunkBytes[pixelOffset] | (((pixel1Color >> plane) & 1) << xmod));
        }

        Assert.Equal(expectedBodyChunkBytes.Length, bodyChunkBytes.Length);
        Assert.Equal(expectedBodyChunkBytes, bodyChunkBytes);
    }

    private static Image CreateImage()
    {
        const int width = 1;
        const int height = 1;
        const int bitsPerPixel = 4;
        
        var palette = new Palette(new Color[]
        {
            new(0, 0, 0),
            new(255, 0, 0),
        }, 0);
        
        var pixelData = new byte[]{ 1 };
        
        return new Image(width, height, bitsPerPixel, palette, pixelData);
    }
}