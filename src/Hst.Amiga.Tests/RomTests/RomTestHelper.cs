using System;

namespace Hst.Amiga.Tests.RomTests;

public static class RomTestHelper
{
    public static byte[] Create16BitKickstartRomBytes(int size = 524288)
    {
        if (size % 2 != 0)
        {
            throw new ArgumentException("Size must be a multiple of 2", nameof(size));
        }

        var kickstartRomBytes = new byte[size];
        for (var i = 0; i < kickstartRomBytes.Length; i++)
        {
            kickstartRomBytes[i] = (byte)(i % 2 == 0 ? 1 : 2);
        }

        return kickstartRomBytes;
    }

    public static byte[] Create32BitKickstartRomBytes(int size = 524288)
    {
        if (size % 4 != 0)
        {
            throw new ArgumentException("Size must be a multiple of 4", nameof(size));
        }

        var kickstartRomBytes = new byte[size];
        var value = 0;
        for (var i = 0; i < kickstartRomBytes.Length; i++)
        {
            kickstartRomBytes[i] = (byte)++value;
            if (value >= 4)
            {
                value = 0;
            }
        }

        return kickstartRomBytes;
    }
    
    public static byte[] SplitHiRomBytes(byte[] romBytes)
    {
        var hiRomBytes = new byte[romBytes.Length / 2];
        var hiRomPos = 0;
        for (var i = 0; i < romBytes.Length; i += 4)
        {
            hiRomBytes[hiRomPos++] = romBytes[i];
            hiRomBytes[hiRomPos++] = romBytes[i + 1];
        }

        return hiRomBytes;
    }
    
    public static byte[] SplitLoRomBytes(byte[] romBytes)
    {
        var loRomBytes = new byte[romBytes.Length / 2];
        var loRomPos = 0;
        for (var i = 2; i < romBytes.Length; i += 4)
        {
            loRomBytes[loRomPos++] = romBytes[i];
            loRomBytes[loRomPos++] = romBytes[i + 1];
        }

        return loRomBytes;
    }

    public static void ByteSwapRomBytes(byte[] bytes)
    {
        for (var i = 0; i < bytes.Length; i += 2)
        {
            (bytes[i], bytes[i + 1]) = (bytes[i + 1], bytes[i]);
        }
    }
    
    public static byte[] FillRomBytes(byte[] romBytes, int epromSize)
    {
        if (romBytes.Length % 2 != 0)
        {
            throw new ArgumentException("Invalid rom bytes", nameof(romBytes));
        }

        var filledRomBytes = new byte[epromSize];
        var filledRomPos = 0;
        while (filledRomPos < epromSize)
        {
            Array.Copy(romBytes, 0, filledRomBytes, filledRomPos, romBytes.Length);
            filledRomPos += romBytes.Length;
        }

        return filledRomBytes;
    }
}