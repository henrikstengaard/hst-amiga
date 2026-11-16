namespace Hst.Amiga.Vm.Libraries.Dos;

using System;
using System.IO;
using System.Text;
using System.Threading;
using Exec;
using Structs;

public partial class DosLibrary
{
    private const int TransferChunkSize = 64 * 1024;

    private void RegisterFileFunctions()
    {
        Register("Open", () => D0 = Open(Ram.ReadCString(D1), (int)D2));
        Register("Close", () =>
        {
            if (D1 != 0)
            {
                FreeFileHandle(D1);
            }

            D0 = unchecked((uint)DosTrue);
        });
        Register("Read", () => D0 = (uint)Read(D1, D2, (int)D3, false));
        Register("Write", () => D0 = (uint)Write(D1, D2, (int)D3));
        Register("Seek", Seek);
        Register("Input", () => D0 = InputHandle);
        Register("Output", () => D0 = OutputHandle);
        Register("SelectInput", () =>
        {
            var old = InputHandle;
            Ram.WriteLong(Process + ProcessStruct.Cis, D1);
            D0 = old;
        });
        Register("SelectOutput", () =>
        {
            var old = OutputHandle;
            Ram.WriteLong(Process + ProcessStruct.Cos, D1);
            D0 = old;
        });
        Register("IsInteractive", () =>
        {
            var file = GetFile(D1);
            D0 = file != null && file.IsInteractive ? unchecked((uint)DosTrue) : 0;
        });
        Register("WaitForChar", WaitForChar);
        Register("Flush", () =>
        {
            GetFile(D1)?.Flush();
            D0 = unchecked((uint)DosTrue);
        });
        Register("SetVBuf", () => D0 = 0);
        Register("SetMode", () => D0 = unchecked((uint)DosTrue));
        Register("ChangeMode", () => D0 = unchecked((uint)DosTrue));
        Register("SetFileSize", SetFileSize);
        Register("LockRecord", () => D0 = unchecked((uint)DosTrue));
        Register("LockRecords", () => D0 = unchecked((uint)DosTrue));
        Register("UnLockRecord", () => D0 = unchecked((uint)DosTrue));
        Register("UnLockRecords", () => D0 = unchecked((uint)DosTrue));

        // buffered io
        Register("FGetC", () =>
        {
            var file = GetFile(D1);
            D0 = file == null ? 0xFFFFFFFF : (uint)file.ReadChar();
        });
        Register("FPutC", () =>
        {
            var file = GetFile(D1);
            if (file == null || !file.CanWrite)
            {
                D0 = 0xFFFFFFFF;
                return;
            }

            file.Write(new[] { (byte)D2 }, 0, 1);
            D0 = D2 & 0xFF;
        });
        Register("UnGetC", () =>
        {
            var file = GetFile(D1);
            var character = (int)D2;
            D0 = file != null && file.UnGetChar(character) ? (character == -1 ? 1u : D2 & 0xFF) : 0;
        });
        Register("FRead", () =>
        {
            var blockLength = D3;
            var number = D4;
            if (blockLength == 0 || number == 0)
            {
                D0 = 0;
                return;
            }

            var total = (long)blockLength * number;
            var read = Read(D1, D2, (int)Math.Min(total, int.MaxValue));
            D0 = read <= 0 ? 0 : (uint)(read / blockLength);
        });
        Register("FWrite", () =>
        {
            var blockLength = D3;
            var number = D4;
            if (blockLength == 0 || number == 0)
            {
                D0 = 0;
                return;
            }

            var total = (long)blockLength * number;
            var written = Write(D1, D2, (int)Math.Min(total, int.MaxValue));
            D0 = written <= 0 ? 0 : (uint)(written / blockLength);
        });
        Register("FGets", FGets);
        Register("FPuts", () => D0 = WriteString(D1, Ram.ReadCString(D2)) ? 0 : 0xFFFFFFFF);
        Register("PutStr", () => D0 = WriteString(OutputHandle, Ram.ReadCString(D1)) ? 0 : 0xFFFFFFFF);
        Register("WriteChars", () => D0 = (uint)Write(OutputHandle, D1, (int)D2));
        Register("VFPrintf", () => D0 = (uint)Printf(D1, D2, D3));
        Register("VPrintf", () => D0 = (uint)Printf(OutputHandle, D1, D2));
        Register("VFWritef", () => D0 = (uint)WriteBytes(D1, WritefFormatter.Format(Ram, D2, D3)));
    }

    /// <summary>
    /// Write bytes to file.
    /// </summary>
    /// <returns>Number of bytes written or -1 on error.</returns>
    private int WriteBytes(uint fileHandle, byte[] bytes)
    {
        var file = GetFile(fileHandle);
        if (file == null || !file.CanWrite)
        {
            return -1;
        }

        try
        {
            file.Write(bytes, 0, bytes.Length);
            return bytes.Length;
        }
        catch (IOException e)
        {
            SetIoErr(MapException(e));
            return -1;
        }
    }

    /// <summary>
    /// Open file or console.
    /// </summary>
    /// <returns>BCPL pointer to file handle or zero, if file can't be opened.</returns>
    public uint Open(string name, int mode)
    {
        name ??= string.Empty;
        var upperName = name.ToUpperInvariant();

        if (name == "*" || upperName == "CONSOLE:" || upperName.StartsWith("CON:") || upperName.StartsWith("RAW:"))
        {
            return CreateConsoleHandle(consoleInput, consoleOutput);
        }

        if (upperName == "NIL:")
        {
            return CreateFileHandle(address => new DosFile(address, Stream.Null, Stream.Null, false, false, null,
                false));
        }

        var path = ResolveOrFail(name);
        if (path == null)
        {
            return 0;
        }

        if (path.IsRoot || Directory.Exists(path.HostPath))
        {
            SetIoErr(DosErrors.ObjectWrongType);
            return 0;
        }

        FileStream stream;
        var existed = File.Exists(path.HostPath);
        try
        {
            switch (mode)
            {
                case ModeNewFile:
                    stream = new FileStream(path.HostPath, FileMode.Create, FileAccess.ReadWrite,
                        FileShare.ReadWrite | FileShare.Delete);
                    break;
                case ModeReadWrite:
                    stream = new FileStream(path.HostPath, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                        FileShare.ReadWrite | FileShare.Delete);
                    break;
                default:
                    if (!File.Exists(path.HostPath))
                    {
                        SetIoErr(DosErrors.ObjectNotFound);
                        return 0;
                    }

                    try
                    {
                        stream = new FileStream(path.HostPath, FileMode.Open, FileAccess.ReadWrite,
                            FileShare.ReadWrite | FileShare.Delete);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // read only file
                        stream = new FileStream(path.HostPath, FileMode.Open, FileAccess.Read,
                            FileShare.ReadWrite | FileShare.Delete);
                    }

                    break;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            SetIoErr(MapException(e));
            return 0;
        }

        if (!existed)
        {
            Metadata.Created(path.HostPath, path.Name);
        }

        return CreateFileHandle(address => new DosFile(address, stream, stream, false, false, path, true));
    }

    /// <summary>
    /// Read from file to memory.
    /// </summary>
    /// <returns>Number of bytes read, 0 at end of file or -1 on error.</returns>
    private int Read(uint fileHandle, uint buffer, int length, bool buffered = true)
    {
        var file = GetFile(fileHandle);
        if (file == null || !file.CanRead || length < 0)
        {
            return -1;
        }

        // limit length to available memory
        length = (int)Math.Min(length, Math.Max(0, (long)Ram.Size - buffer));

        try
        {
            if (file.IsConsole)
            {
                var consoleBuffer = new byte[Math.Min(length, TransferChunkSize)];
                var consoleRead = file.Read(consoleBuffer, 0, consoleBuffer.Length, buffered);
                if (consoleRead > 0)
                {
                    Ram.WriteBytes(buffer, consoleBuffer, 0, consoleRead);
                }

                return Math.Max(0, consoleRead);
            }

            var chunk = new byte[Math.Min(length, TransferChunkSize)];
            var total = 0;
            while (total < length)
            {
                var read = file.Read(chunk, 0, Math.Min(chunk.Length, length - total), buffered);
                if (read <= 0)
                {
                    break;
                }

                Ram.WriteBytes(buffer + (uint)total, chunk, 0, read);
                total += read;
            }

            return total;
        }
        catch (IOException e)
        {
            SetIoErr(MapException(e));
            return -1;
        }
    }

    /// <summary>
    /// Write from memory to file.
    /// </summary>
    /// <returns>Number of bytes written or -1 on error.</returns>
    private int Write(uint fileHandle, uint buffer, int length)
    {
        var file = GetFile(fileHandle);
        if (file == null || !file.CanWrite || length < 0)
        {
            return -1;
        }

        try
        {
            var written = 0;
            while (written < length)
            {
                var count = Math.Min(TransferChunkSize, length - written);
                var bytes = Ram.ReadBytes(buffer + (uint)written, count);
                file.Write(bytes, 0, count);
                written += count;
            }

            return written;
        }
        catch (IOException e)
        {
            SetIoErr(e.Message.Contains("space") ? DosErrors.DiskFull : MapException(e));
            return -1;
        }
    }

    private bool WriteString(uint fileHandle, string value)
    {
        var file = GetFile(fileHandle);
        if (file == null || !file.CanWrite)
        {
            return false;
        }

        try
        {
            var bytes = Ram.GetBytes(value);
            file.Write(bytes, 0, bytes.Length);
            return true;
        }
        catch (IOException e)
        {
            SetIoErr(MapException(e));
            return false;
        }
    }

    private void Seek()
    {
        var file = GetFile(D1);
        var position = (int)D2;
        var mode = (int)D3;
        if (file == null)
        {
            D0 = 0xFFFFFFFF;
            return;
        }

        if (file.IsConsole || file.Path == null)
        {
            SetIoErr(DosErrors.ActionNotKnown);
            D0 = 0xFFFFFFFF;
            return;
        }

        try
        {
            file.Flush();
            var oldPosition = file.Position;
            var newPosition = mode switch
            {
                OffsetBeginning => position,
                OffsetCurrent => oldPosition + position,
                OffsetEnd => file.Length + position,
                _ => -1L
            };

            if (newPosition < 0 || newPosition > file.Length)
            {
                SetIoErr(DosErrors.SeekError);
                D0 = 0xFFFFFFFF;
                return;
            }

            file.Position = newPosition;
            D0 = (uint)oldPosition;
        }
        catch (IOException e)
        {
            SetIoErr(MapException(e));
            D0 = 0xFFFFFFFF;
        }
    }

    private void SetFileSize()
    {
        var file = GetFile(D1);
        var position = (int)D2;
        var mode = (int)D3;
        if (file == null || file.Path == null)
        {
            D0 = 0xFFFFFFFF;
            return;
        }

        try
        {
            var newSize = mode switch
            {
                OffsetBeginning => position,
                OffsetCurrent => file.Position + position,
                OffsetEnd => file.Length + position,
                _ => -1L
            };

            if (newSize < 0)
            {
                SetIoErr(DosErrors.SeekError);
                D0 = 0xFFFFFFFF;
                return;
            }

            file.Flush();
            ((FileStream)file.Input).SetLength(newSize);
            D0 = (uint)newSize;
        }
        catch (IOException e)
        {
            SetIoErr(MapException(e));
            D0 = 0xFFFFFFFF;
        }
    }

    private void WaitForChar()
    {
        var file = GetFile(D1);
        if (file == null)
        {
            D0 = 0;
            return;
        }

        if (!file.IsInteractive)
        {
            D0 = unchecked((uint)DosTrue);
            return;
        }

        FlushConsole();
        var timeoutMs = (int)Math.Min(D2 / 1000, int.MaxValue);
        try
        {
            var start = Environment.TickCount;
            while (true)
            {
                if (Console.KeyAvailable)
                {
                    D0 = unchecked((uint)DosTrue);
                    return;
                }

                if (Environment.TickCount - start >= timeoutMs)
                {
                    D0 = 0;
                    return;
                }

                Thread.Sleep(10);
            }
        }
        catch (InvalidOperationException)
        {
            // console input is redirected
            D0 = unchecked((uint)DosTrue);
        }
    }

    private void FGets()
    {
        var file = GetFile(D1);
        var buffer = D2;
        var length = (int)D3;
        if (file == null || length <= 0)
        {
            D0 = 0;
            return;
        }

        var bytes = new System.Collections.Generic.List<byte>();
        while (bytes.Count < length - 1)
        {
            var character = file.ReadChar();
            if (character < 0)
            {
                break;
            }

            bytes.Add((byte)character);
            if (character == '\n')
            {
                break;
            }
        }

        if (bytes.Count == 0)
        {
            SetIoErr(0);
            D0 = 0;
            return;
        }

        Ram.WriteBytes(buffer, bytes.ToArray());
        Ram.WriteByte(buffer + (uint)bytes.Count, 0);
        D0 = buffer;
    }

    /// <summary>
    /// Format string with RawDoFmt() formatting and write to file.
    /// </summary>
    /// <returns>Number of bytes written or -1 on error.</returns>
    private int Printf(uint fileHandle, uint format, uint arguments)
    {
        return WriteBytes(fileHandle, RawDoFmtFormatter.Format(Ram, format, arguments, out _));
    }

    /// <summary>
    /// Read line from console input of process.
    /// </summary>
    private string ReadInputLine()
    {
        var file = files.TryGetValue(InputHandle, out var f) ? f : null;
        if (file == null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        while (true)
        {
            var character = file.ReadChar();
            if (character < 0 || character == '\n')
            {
                break;
            }

            builder.Append((char)character);
        }

        return builder.ToString();
    }
}
