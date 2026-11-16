namespace Hst.Amiga.Vm.Libraries.Utility;

using System;
using Exec;
using Structs;

/// <summary>
/// Emulated utility.library with tag item, date, 32-bit math and string functions used by compiler runtimes.
/// </summary>
public class UtilityLibrary : Library
{
    private const uint TagDone = 0;
    private const uint TagIgnore = 1;
    private const uint TagMore = 2;
    private const uint TagSkip = 3;

    private static readonly string[] Names =
    {
        "FindTagItem", "GetTagData", "PackBoolTags", "NextTagItem", "FilterTagChanges", "MapTags",
        "AllocateTagItems", "CloneTagItems", "FreeTagItems", "RefreshTagItemClones", "TagInArray",
        "FilterTagItems", "CallHookPkt", "UtilityPrivate1", "UtilityPrivate2", "Amiga2Date", "Date2Amiga",
        "CheckDate", "SMult32", "UMult32", "SDivMod32", "UDivMod32", "Stricmp", "Strnicmp", "ToUpper", "ToLower",
        "ApplyTagChanges", "UtilityPrivate3", "SMult64", "UMult64", "PackStructureTags", "UnpackStructureTags",
        "AddNamedObject", "AllocNamedObjectA", "AttemptRemNameSpace", "FindNamedObject", "FreeNamedObject",
        "NamedObjectName", "ReleaseNamedObject", "RemNamedObject", "GetUniqueID"
    };

    private static readonly DateTime AmigaEpoch = new(1978, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    private readonly ExecLibrary exec;
    private uint uniqueId;

    public UtilityLibrary(VmContext context, ExecLibrary exec) : base(context)
    {
        this.exec = exec;
    }

    public override string Name => "utility.library";
    public override ushort Version => 40;
    public override ushort Revision => 1;
    protected override string[] FunctionNames => Names;

    protected override void Initialize()
    {
        exec.AddLibraryNode(this);
    }

    protected override void RegisterFunctions()
    {
        Register("FindTagItem", () => D0 = FindTagItem(D0, A0));
        Register("GetTagData", () =>
        {
            var tagItem = FindTagItem(D0, A0);
            D0 = tagItem == 0 ? D1 : Ram.ReadLong(tagItem + TagItemStruct.Data);
        });
        Register("NextTagItem", () => D0 = NextTagItem(A0));
        Register("PackBoolTags", () =>
        {
            var flags = D0;
            var tagList = A0;
            var boolMap = A1;
            for (var map = boolMap; Ram.ReadLong(map) != TagDone; map += TagItemStruct.Size)
            {
                var tagItem = FindTagItem(Ram.ReadLong(map), tagList);
                if (tagItem == 0)
                {
                    continue;
                }

                var flag = Ram.ReadLong(map + TagItemStruct.Data);
                flags = Ram.ReadLong(tagItem + TagItemStruct.Data) != 0 ? flags | flag : flags & ~flag;
            }

            D0 = flags;
        });
        Register("TagInArray", () =>
        {
            for (var address = A0; ; address += 4)
            {
                var tag = Ram.ReadLong(address);
                if (tag == TagDone)
                {
                    D0 = 0;
                    return;
                }

                if (tag == D0)
                {
                    D0 = 1;
                    return;
                }
            }
        });
        Register("AllocateTagItems", () => D0 = exec.AllocVec(D0 * TagItemStruct.Size));
        Register("FreeTagItems", () => exec.FreeVec(A0));
        Register("CloneTagItems", () =>
        {
            var count = 0U;
            var state = Context.Allocator.Allocate(4);
            Ram.WriteLong(state, A0);
            while (A0 != 0 && NextTagItem(state) != 0)
            {
                count++;
            }

            var clone = exec.AllocVec((count + 1) * TagItemStruct.Size);
            if (clone != 0)
            {
                Ram.WriteLong(state, A0);
                var target = clone;
                uint tagItem;
                while (A0 != 0 && (tagItem = NextTagItem(state)) != 0)
                {
                    Ram.Copy(tagItem, target, TagItemStruct.Size);
                    target += TagItemStruct.Size;
                }
            }

            Context.Allocator.Free(state);
            D0 = clone;
        });
        Register("CallHookPkt", () =>
        {
            var hook = A0;
            Cpu.A[0] = hook;
            Cpu.A[1] = A1;
            Cpu.A[2] = A2;
            D0 = Context.CallSubroutine(Ram.ReadLong(hook + 8));
        });
        Register("Amiga2Date", Amiga2Date);
        Register("Date2Amiga", () => D0 = Date2Amiga(A0) ?? 0);
        Register("CheckDate", () => D0 = Date2Amiga(A0) ?? 0);
        Register("SMult32", () => D0 = (uint)((int)D0 * (int)D1));
        Register("UMult32", () => D0 = D0 * D1);
        Register("SDivMod32", () =>
        {
            var dividend = (int)D0;
            var divisor = (int)D1;
            if (divisor == 0)
            {
                throw new VmException("utility.library/SDivMod32 division by zero");
            }

            if (dividend == int.MinValue && divisor == -1)
            {
                D0 = 0x80000000;
                D1 = 0;
                return;
            }

            D0 = (uint)(dividend / divisor);
            D1 = (uint)(dividend % divisor);
        });
        Register("UDivMod32", () =>
        {
            var dividend = D0;
            var divisor = D1;
            if (divisor == 0)
            {
                throw new VmException("utility.library/UDivMod32 division by zero");
            }

            D0 = dividend / divisor;
            D1 = dividend % divisor;
        });
        Register("SMult64", () =>
        {
            var result = (long)(int)D0 * (int)D1;
            D0 = (uint)(result >> 32);
            D1 = (uint)result;
        });
        Register("UMult64", () =>
        {
            var result = (ulong)D0 * D1;
            D0 = (uint)(result >> 32);
            D1 = (uint)result;
        });
        Register("Stricmp", () => D0 = (uint)Compare(Ram.ReadCString(A0), Ram.ReadCString(A1), int.MaxValue));
        Register("Strnicmp", () => D0 = (uint)Compare(Ram.ReadCString(A0), Ram.ReadCString(A1), (int)D0));
        Register("ToUpper", () => D0 = ToUpper((byte)D0));
        Register("ToLower", () => D0 = ToLower((byte)D0));
        Register("GetUniqueID", () => D0 = ++uniqueId);
    }

    private uint NextTagItem(uint tagListPointer)
    {
        var tagItem = Ram.ReadLong(tagListPointer);
        while (tagItem != 0)
        {
            var tag = Ram.ReadLong(tagItem + TagItemStruct.Tag);
            switch (tag)
            {
                case TagDone:
                    Ram.WriteLong(tagListPointer, 0);
                    return 0;
                case TagIgnore:
                    tagItem += TagItemStruct.Size;
                    break;
                case TagMore:
                    tagItem = Ram.ReadLong(tagItem + TagItemStruct.Data);
                    break;
                case TagSkip:
                    tagItem += (Ram.ReadLong(tagItem + TagItemStruct.Data) + 1) * TagItemStruct.Size;
                    break;
                default:
                    Ram.WriteLong(tagListPointer, tagItem + TagItemStruct.Size);
                    return tagItem;
            }
        }

        Ram.WriteLong(tagListPointer, 0);
        return 0;
    }

    private uint FindTagItem(uint tagValue, uint tagList)
    {
        if (tagList == 0)
        {
            return 0;
        }

        var state = Context.Allocator.Allocate(4);
        Ram.WriteLong(state, tagList);
        try
        {
            uint tagItem;
            while ((tagItem = NextTagItem(state)) != 0)
            {
                if (Ram.ReadLong(tagItem + TagItemStruct.Tag) == tagValue)
                {
                    return tagItem;
                }
            }

            return 0;
        }
        finally
        {
            Context.Allocator.Free(state);
        }
    }

    private void Amiga2Date()
    {
        var date = AmigaEpoch.AddSeconds(D0);
        var clockData = A0;
        Ram.WriteWord(clockData + ClockDataStruct.Sec, (ushort)date.Second);
        Ram.WriteWord(clockData + ClockDataStruct.Min, (ushort)date.Minute);
        Ram.WriteWord(clockData + ClockDataStruct.Hour, (ushort)date.Hour);
        Ram.WriteWord(clockData + ClockDataStruct.MDay, (ushort)date.Day);
        Ram.WriteWord(clockData + ClockDataStruct.Month, (ushort)date.Month);
        Ram.WriteWord(clockData + ClockDataStruct.Year, (ushort)date.Year);
        Ram.WriteWord(clockData + ClockDataStruct.WDay, (ushort)date.DayOfWeek);
    }

    private uint? Date2Amiga(uint clockData)
    {
        try
        {
            var date = new DateTime(Ram.ReadWord(clockData + ClockDataStruct.Year),
                Ram.ReadWord(clockData + ClockDataStruct.Month), Ram.ReadWord(clockData + ClockDataStruct.MDay),
                Ram.ReadWord(clockData + ClockDataStruct.Hour), Ram.ReadWord(clockData + ClockDataStruct.Min),
                Ram.ReadWord(clockData + ClockDataStruct.Sec));
            var seconds = (date - AmigaEpoch).TotalSeconds;
            return seconds < 0 ? null : (uint)seconds;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>
    /// Convert Latin-1 character to upper case.
    /// </summary>
    public static uint ToUpper(byte character) =>
        character is >= (byte)'a' and <= (byte)'z' or >= 0xE0 and <= 0xFE && character != 0xF7
            ? (uint)(character - 0x20)
            : character;

    /// <summary>
    /// Convert Latin-1 character to lower case.
    /// </summary>
    public static uint ToLower(byte character) =>
        character is >= (byte)'A' and <= (byte)'Z' or >= 0xC0 and <= 0xDE && character != 0xD7
            ? (uint)(character + 0x20)
            : character;

    private static int Compare(string string1, string string2, int length)
    {
        string1 ??= string.Empty;
        string2 ??= string.Empty;
        for (var i = 0; i < length; i++)
        {
            var c1 = i < string1.Length ? (int)ToLower((byte)string1[i]) : 0;
            var c2 = i < string2.Length ? (int)ToLower((byte)string2[i]) : 0;
            if (c1 != c2)
            {
                return c1 - c2;
            }

            if (c1 == 0)
            {
                return 0;
            }
        }

        return 0;
    }
}
