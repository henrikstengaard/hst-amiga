namespace Hst.Amiga.Vm;

using System;
using System.Text;

/// <summary>
/// Flat big-endian random access memory. Addresses are masked by address mask, 24-bit for 68000 and
/// 32-bit for 68020. Accessing addresses outside memory throws <see cref="BusErrorException"/>.
/// </summary>
public class Ram
{
    private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");

    private readonly byte[] data;
    private readonly uint size;

    public Ram(uint size, uint addressMask = 0xFFFFFFFF)
    {
        if (size < 0x10000)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Memory size must be at least 64 KB");
        }

        data = new byte[size];
        this.size = size;
        AddressMask = addressMask;
    }

    public uint Size => size;
    public uint AddressMask { get; }

    /// <summary>
    /// Raw memory bytes.
    /// </summary>
    public byte[] Data => data;

    private uint Translate(uint address, uint length, bool write)
    {
        address &= AddressMask;
        if (address > size - length)
        {
            throw new BusErrorException(address, write);
        }

        return address;
    }

    public byte ReadByte(uint address) => data[Translate(address, 1, false)];

    public ushort ReadWord(uint address)
    {
        address = Translate(address, 2, false);
        return (ushort)((data[address] << 8) | data[address + 1]);
    }

    public uint ReadLong(uint address)
    {
        address = Translate(address, 4, false);
        return ((uint)data[address] << 24) | ((uint)data[address + 1] << 16) | ((uint)data[address + 2] << 8) |
               data[address + 3];
    }

    public void WriteByte(uint address, byte value) => data[Translate(address, 1, true)] = value;

    public void WriteWord(uint address, ushort value)
    {
        address = Translate(address, 2, true);
        data[address] = (byte)(value >> 8);
        data[address + 1] = (byte)value;
    }

    public void WriteLong(uint address, uint value)
    {
        address = Translate(address, 4, true);
        data[address] = (byte)(value >> 24);
        data[address + 1] = (byte)(value >> 16);
        data[address + 2] = (byte)(value >> 8);
        data[address + 3] = (byte)value;
    }

    public byte[] ReadBytes(uint address, int count)
    {
        var bytes = new byte[count];
        if (count == 0)
        {
            return bytes;
        }

        Array.Copy(data, Translate(address, (uint)count, false), bytes, 0, count);
        return bytes;
    }

    public void WriteBytes(uint address, byte[] bytes, int offset = 0, int count = -1)
    {
        if (count < 0)
        {
            count = bytes.Length - offset;
        }

        if (count == 0)
        {
            return;
        }

        Array.Copy(bytes, offset, data, Translate(address, (uint)count, true), count);
    }

    public void Fill(uint address, uint count, byte value = 0)
    {
        if (count == 0)
        {
            return;
        }

        var start = Translate(address, count, true);
        for (var i = 0U; i < count; i++)
        {
            data[start + i] = value;
        }
    }

    public void Copy(uint source, uint destination, uint count)
    {
        if (count == 0)
        {
            return;
        }

        Array.Copy(data, Translate(source, count, false), data, Translate(destination, count, true), count);
    }

    /// <summary>
    /// Read zero terminated string.
    /// </summary>
    /// <returns>String or null, if address is zero.</returns>
    public string ReadCString(uint address, int maxLength = 0x10000)
    {
        if (address == 0)
        {
            return null;
        }

        var start = Translate(address, 1, false);
        var end = start;
        while (end < size && data[end] != 0 && end - start < maxLength)
        {
            end++;
        }

        return Latin1.GetString(data, (int)start, (int)(end - start));
    }

    /// <summary>
    /// Write zero terminated string.
    /// </summary>
    public void WriteCString(uint address, string value)
    {
        var bytes = Latin1.GetBytes(value ?? string.Empty);
        WriteBytes(address, bytes);
        WriteByte(address + (uint)bytes.Length, 0);
    }

    /// <summary>
    /// Read BCPL string with length byte followed by characters.
    /// </summary>
    /// <returns>String or null, if address is zero.</returns>
    public string ReadBString(uint address)
    {
        if (address == 0)
        {
            return null;
        }

        var length = ReadByte(address);
        return Latin1.GetString(ReadBytes(address + 1, length));
    }

    /// <summary>
    /// Write BCPL string with length byte followed by characters, max 255 characters.
    /// </summary>
    public void WriteBString(uint address, string value)
    {
        var bytes = Latin1.GetBytes(value ?? string.Empty);
        var length = Math.Min(bytes.Length, 255);
        WriteByte(address, (byte)length);
        WriteBytes(address + 1, bytes, 0, length);
    }

    public static byte[] GetBytes(string value) => Latin1.GetBytes(value ?? string.Empty);

    public static string GetString(byte[] bytes, int offset, int count) => Latin1.GetString(bytes, offset, count);
}
