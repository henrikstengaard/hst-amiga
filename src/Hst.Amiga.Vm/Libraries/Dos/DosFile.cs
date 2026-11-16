namespace Hst.Amiga.Vm.Libraries.Dos;

using System;
using System.IO;
using FileSystem;

/// <summary>
/// Open dos file handle mapped to host streams. Console file handles read from input stream and write to
/// output stream, files use same stream for reading and writing.
/// </summary>
internal sealed class DosFile : IDisposable
{
    private readonly bool ownsStreams;
    private int ungetChar = -1;
    private int lastChar = -1;

    public DosFile(uint address, Stream input, Stream output, bool isConsole, bool isInteractive, AmigaPath path,
        bool ownsStreams)
    {
        Address = address;
        Input = input;
        Output = output;
        IsConsole = isConsole;
        IsInteractive = isInteractive;
        Path = path;
        this.ownsStreams = ownsStreams;
    }

    /// <summary>
    /// Address of FileHandle structure.
    /// </summary>
    public uint Address { get; }

    public Stream Input { get; }
    public Stream Output { get; }
    public bool IsConsole { get; }
    public bool IsInteractive { get; }

    /// <summary>
    /// Amiga path of file, null for console.
    /// </summary>
    public AmigaPath Path { get; }

    /// <summary>
    /// Action called before reading from console, used to flush console output.
    /// </summary>
    public Action BeforeConsoleRead { get; set; }

    public bool CanRead => Input != null && Input.CanRead;
    public bool CanWrite => Output != null && Output.CanWrite;

    public int Read(byte[] buffer, int offset, int count)
    {
        if (!CanRead)
        {
            return -1;
        }

        var start = offset;
        var read = 0;
        if (count > 0 && ungetChar >= 0)
        {
            buffer[offset++] = (byte)ungetChar;
            ungetChar = -1;
            count--;
            read++;
        }

        if (count > 0 && IsConsole && read == 0)
        {
            BeforeConsoleRead?.Invoke();

            // console reads return available data, typically a line
            var bytesRead = Input.Read(buffer, offset, count);
            if (bytesRead > 0)
            {
                read += bytesRead;
            }
        }
        else if (!IsConsole)
        {
            while (count > 0)
            {
                var bytesRead = Input.Read(buffer, offset, count);
                if (bytesRead <= 0)
                {
                    break;
                }

                offset += bytesRead;
                count -= bytesRead;
                read += bytesRead;
            }
        }

        if (read > 0)
        {
            lastChar = buffer[start + read - 1];
        }

        return read;
    }

    /// <summary>
    /// Read character.
    /// </summary>
    /// <returns>Character or -1 at end of file.</returns>
    public int ReadChar()
    {
        if (ungetChar >= 0)
        {
            lastChar = ungetChar;
            ungetChar = -1;
            return lastChar;
        }

        if (!CanRead)
        {
            return -1;
        }

        if (IsConsole)
        {
            BeforeConsoleRead?.Invoke();
        }

        var value = Input.ReadByte();
        lastChar = value;
        return value;
    }

    /// <summary>
    /// Push character back to be read again, -1 pushes back last character read.
    /// </summary>
    public bool UnGetChar(int character)
    {
        if (character == -1)
        {
            if (lastChar < 0)
            {
                return false;
            }

            character = lastChar;
        }

        ungetChar = character & 0xFF;
        return true;
    }

    public void Write(byte[] buffer, int offset, int count)
    {
        Output.Write(buffer, offset, count);
        if (IsConsole && Array.IndexOf(buffer, (byte)'\n', offset, count) >= 0)
        {
            Output.Flush();
        }
    }

    public void Flush()
    {
        if (CanWrite)
        {
            Output.Flush();
        }

        // discard pushed back character
        ungetChar = -1;
    }

    public long Position
    {
        get => IsConsole ? 0 : Input.Position - (ungetChar >= 0 ? 1 : 0);
        set
        {
            ungetChar = -1;
            Input.Position = value;
        }
    }

    public long Length => IsConsole ? 0 : Input.Length;

    public void Dispose()
    {
        if (CanWrite)
        {
            try
            {
                Output.Flush();
            }
            catch (IOException)
            {
            }
        }

        if (!ownsStreams)
        {
            return;
        }

        Input?.Dispose();
        if (!ReferenceEquals(Input, Output))
        {
            Output?.Dispose();
        }
    }
}
