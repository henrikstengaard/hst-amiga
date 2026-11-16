namespace Hst.Amiga.Vm.Libraries.Dos;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using FileSystem;
using Structs;

public partial class DosLibrary
{
    private const uint GvfGlobalOnly = 0x100;
    private const uint GvfLocalOnly = 0x200;
    private const uint GvfBinaryVar = 0x400;
    private const uint GvfDontNullTerm = 0x800;
    private const uint GvfSaveVar = 0x1000;

    private const int DosFileHandle = 0;
    private const int DosExAllControl = 1;
    private const int DosFib = 2;
    private const int DosStdPkt = 3;
    private const int DosCli = 4;
    private const int DosRdArgs = 5;

    private readonly Dictionary<string, byte[]> localVariables = new(StringComparer.OrdinalIgnoreCase);

    private void RegisterMiscFunctions()
    {
        Register("DateStamp", () => D0 = WriteDateStamp(D1, DateTime.Now));
        Register("Delay", () =>
        {
            var ticks = (int)D1;
            if (ticks > 0)
            {
                FlushConsole();
                Context.CancellationToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(ticks * 20.0));
            }
        });
        Register("CompareDates", () =>
        {
            var date1 = ReadDateStampTicks(D1);
            var date2 = ReadDateStampTicks(D2);
            D0 = date1 > date2 ? 0xFFFFFFFF : date1 < date2 ? 1u : 0u;
        });
        Register("DateToStr", DateToStr);
        Register("GetVar", GetVar);
        Register("SetVar", SetVar);
        Register("DeleteVar", DeleteVar);
        Register("FindVar", () => D0 = 0);
        Register("AllocDosObject", () => D0 = AllocDosObject((int)D1));
        Register("FreeDosObject", () =>
        {
            if (D2 != 0)
            {
                Context.Allocator.Free(D2);
            }
        });
    }

    private long ReadDateStampTicks(uint address) =>
        ((long)(int)Ram.ReadLong(address + DateStampStruct.Days) * 24 * 60 +
         (int)Ram.ReadLong(address + DateStampStruct.Minute)) * 3000 +
        (int)Ram.ReadLong(address + DateStampStruct.Tick);

    private uint AllocDosObject(int type)
    {
        var size = type switch
        {
            DosFileHandle => FileHandleStruct.Size,
            DosExAllControl => ExAllControlStruct.Size,
            DosFib => FileInfoBlockStruct.Size,
            DosStdPkt => 68,
            DosCli => CliStruct.Size,
            DosRdArgs => RdArgsStruct.Size,
            _ => 0
        };

        if (size == 0)
        {
            SetIoErr(DosErrors.BadNumber);
            return 0;
        }

        var address = Context.Allocator.Allocate((uint)size);
        if (address == 0)
        {
            SetIoErr(DosErrors.NoFreeStore);
        }

        return address;
    }

    private void DateToStr()
    {
        var dateTime = D1;
        var format = Ram.ReadByte(dateTime + DateTimeStruct.Format);
        var flags = Ram.ReadByte(dateTime + DateTimeStruct.Flags);
        var strDay = Ram.ReadLong(dateTime + DateTimeStruct.StrDay);
        var strDate = Ram.ReadLong(dateTime + DateTimeStruct.StrDate);
        var strTime = Ram.ReadLong(dateTime + DateTimeStruct.StrTime);
        var date = ReadDateStamp(dateTime + DateTimeStruct.Stamp);

        var dayName = date.ToString("dddd", CultureInfo.InvariantCulture);
        var dateText = format switch
        {
            1 => date.ToString("yy-MM-dd", CultureInfo.InvariantCulture), // FORMAT_INT
            2 => date.ToString("MM-dd-yy", CultureInfo.InvariantCulture), // FORMAT_USA
            3 => date.ToString("dd-MM-yy", CultureInfo.InvariantCulture), // FORMAT_CDN
            _ => date.ToString("dd-MMM-yy", CultureInfo.InvariantCulture) // FORMAT_DOS
        };

        // DTF_SUBST substitutes today, yesterday and tomorrow
        if ((flags & 1) != 0)
        {
            var days = (date.Date - DateTime.Now.Date).Days;
            var substitute = days switch
            {
                0 => "Today",
                -1 => "Yesterday",
                1 => "Tomorrow",
                _ => null
            };

            if (substitute != null)
            {
                dateText = substitute;
            }
        }

        if (strDay != 0)
        {
            Ram.WriteCString(strDay, dayName);
        }

        if (strDate != 0)
        {
            Ram.WriteCString(strDate, dateText);
        }

        if (strTime != 0)
        {
            Ram.WriteCString(strTime, date.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        }

        D0 = unchecked((uint)DosTrue);
    }

    private AmigaPath GetEnvPath(string name, bool archive) =>
        ResolvePath($"{(archive ? "ENVARC" : "ENV")}:{name}");

    private void GetVar()
    {
        var name = Ram.ReadCString(D1) ?? string.Empty;
        var buffer = D2;
        var size = (int)D3;
        var flags = D4;

        byte[] value = null;
        if ((flags & GvfGlobalOnly) == 0 && localVariables.TryGetValue(name, out var localValue))
        {
            value = localValue;
        }
        else if ((flags & GvfLocalOnly) == 0)
        {
            var path = GetEnvPath(name, false);
            if (path != null && File.Exists(path.HostPath))
            {
                try
                {
                    value = File.ReadAllBytes(path.HostPath);
                }
                catch (IOException)
                {
                }
            }
        }

        if (value == null)
        {
            SetIoErr(DosErrors.ObjectNotFound);
            D0 = 0xFFFFFFFF;
            return;
        }

        // text variables stop at first linefeed
        var length = value.Length;
        if ((flags & GvfBinaryVar) == 0)
        {
            var lineFeed = Array.IndexOf(value, (byte)'\n');
            if (lineFeed >= 0)
            {
                length = lineFeed;
            }
        }

        if (size <= 0)
        {
            D0 = 0;
            return;
        }

        var nullTerminate = (flags & GvfDontNullTerm) == 0;
        var count = Math.Min(length, nullTerminate ? size - 1 : size);
        Ram.WriteBytes(buffer, value, 0, count);
        if (nullTerminate)
        {
            Ram.WriteByte(buffer + (uint)count, 0);
        }

        if (count < length)
        {
            SetIoErr(DosErrors.BufferOverflow);
        }

        D0 = (uint)count;
    }

    private void SetVar()
    {
        var name = Ram.ReadCString(D1) ?? string.Empty;
        var size = (int)D3;
        var flags = D4;
        byte[] value;
        if (D2 == 0)
        {
            value = null;
        }
        else if (size < 0)
        {
            value = Ram.GetBytes(Ram.ReadCString(D2));
        }
        else
        {
            value = Ram.ReadBytes(D2, size);
        }

        if (value == null)
        {
            DeleteVariable(name, flags);
            D0 = unchecked((uint)DosTrue);
            return;
        }

        if ((flags & GvfGlobalOnly) == 0)
        {
            localVariables[name] = value;
            D0 = unchecked((uint)DosTrue);
            return;
        }

        if (!WriteEnvVariable(name, value, false) ||
            ((flags & GvfSaveVar) != 0 && !WriteEnvVariable(name, value, true)))
        {
            D0 = 0;
            return;
        }

        D0 = unchecked((uint)DosTrue);
    }

    private bool WriteEnvVariable(string name, byte[] value, bool archive)
    {
        var path = GetEnvPath(name, archive);
        if (path == null)
        {
            SetIoErr(DosErrors.ObjectNotFound);
            return false;
        }

        try
        {
            var directory = Path.GetDirectoryName(path.HostPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(path.HostPath, value);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            SetIoErr(MapException(e));
            return false;
        }
    }

    private void DeleteVar()
    {
        D0 = DeleteVariable(Ram.ReadCString(D1) ?? string.Empty, D2) ? unchecked((uint)DosTrue) : 0;
    }

    private bool DeleteVariable(string name, uint flags)
    {
        if ((flags & GvfGlobalOnly) == 0 && localVariables.Remove(name))
        {
            return true;
        }

        if ((flags & GvfLocalOnly) != 0)
        {
            SetIoErr(DosErrors.ObjectNotFound);
            return false;
        }

        var path = GetEnvPath(name, false);
        if (path == null || !File.Exists(path.HostPath))
        {
            SetIoErr(DosErrors.ObjectNotFound);
            return false;
        }

        try
        {
            File.Delete(path.HostPath);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            SetIoErr(MapException(e));
            return false;
        }
    }
}
