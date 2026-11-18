namespace Hst.Amiga.Tests.VmTests;

using System;
using System.IO;
using Vm;
using Vm.FileSystem;
using Vm.Libraries.Dos;
using Vm.Libraries.Exec;
using Vm.Libraries.Structs;
using Xunit;

public class GivenStrToDate : IDisposable
{
    private const byte FormatDos = 0;
    private const byte FormatInt = 1;
    private const byte FormatUsa = 2;
    private const byte FormatCdn = 3;
    private const byte DtfSubst = 1;

    private readonly string sysPath;
    private readonly VmContext context;
    private readonly DosLibrary dos;

    public GivenStrToDate()
    {
        sysPath = Path.Combine(Path.GetTempPath(), $"hst-amiga-vm-dates-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sysPath);

        context = new VmContext(new VmOptions { Log = _ => { } });
        var exec = new ExecLibrary(context);
        exec.Install();
        context.AddLibrary(exec);

        var volumes = new VolumeManager();
        volumes.AddVolume("SYS", sysPath);
        dos = new DosLibrary(context, exec, volumes);
        dos.Install();
        context.AddLibrary(dos);

        var stack = context.Allocator.Allocate(4096);
        context.Cpu.A[7] = stack + 4096;
    }

    public void Dispose()
    {
        Directory.Delete(sysPath, true);
    }

    private uint String(string value)
    {
        if (value == null)
        {
            return 0;
        }

        var address = context.Allocator.Allocate((uint)value.Length + 1);
        context.Ram.WriteCString(address, value);
        return address;
    }

    private (uint Result, int Days, int Minutes, int Ticks) StrToDate(string date, string time, byte format,
        byte flags = 0, int days = 0, int minutes = 0, int ticks = 0)
    {
        var dateTime = context.Allocator.Allocate(DateTimeStruct.Size);
        context.Ram.WriteLong(dateTime + DateStampStruct.Days, (uint)days);
        context.Ram.WriteLong(dateTime + DateStampStruct.Minute, (uint)minutes);
        context.Ram.WriteLong(dateTime + DateStampStruct.Tick, (uint)ticks);
        context.Ram.WriteByte(dateTime + DateTimeStruct.Format, format);
        context.Ram.WriteByte(dateTime + DateTimeStruct.Flags, flags);
        context.Ram.WriteLong(dateTime + DateTimeStruct.StrDay, 0);
        context.Ram.WriteLong(dateTime + DateTimeStruct.StrDate, String(date));
        context.Ram.WriteLong(dateTime + DateTimeStruct.StrTime, String(time));

        context.Cpu.D[1] = dateTime;
        var result = context.CallSubroutine((uint)(dos.BaseAddress + dos.GetOffset("StrToDate")));
        return (result, (int)context.Ram.ReadLong(dateTime + DateStampStruct.Days),
            (int)context.Ram.ReadLong(dateTime + DateStampStruct.Minute),
            (int)context.Ram.ReadLong(dateTime + DateStampStruct.Tick));
    }

    private static int AmigaDays(int year, int month, int day) =>
        (new DateTime(year, month, day) - new DateTime(1978, 1, 1)).Days;

    [Theory]
    [InlineData("15-Jul-93", FormatDos)]
    [InlineData("93-Jul-15", FormatInt)]
    [InlineData("07-15-93", FormatUsa)]
    [InlineData("15-07-93", FormatCdn)]
    public void When_ConvertingDateInFormat_Then_DaysAreSet(string date, byte format)
    {
        var (result, days, minutes, ticks) = StrToDate(date, "13:45:10", format);

        Assert.Equal(0xFFFFFFFFU, result);
        Assert.Equal(AmigaDays(1993, 7, 15), days);
        Assert.Equal(13 * 60 + 45, minutes);
        Assert.Equal(10 * 50, ticks);
    }

    [Fact]
    public void When_ConvertingTwoDigitYearBefore78_Then_YearIsAfter2000()
    {
        var (result, days, _, _) = StrToDate("01-Jan-05", null, FormatDos);

        Assert.Equal(0xFFFFFFFFU, result);
        Assert.Equal(AmigaDays(2005, 1, 1), days);
    }

    [Fact]
    public void When_ConvertingOnlyDate_Then_TimeIsUnchanged()
    {
        var (result, days, minutes, ticks) = StrToDate("15-Jul-93", null, FormatDos, 0, 0, 100, 25);

        Assert.Equal(0xFFFFFFFFU, result);
        Assert.Equal(AmigaDays(1993, 7, 15), days);
        Assert.Equal(100, minutes);
        Assert.Equal(25, ticks);
    }

    [Fact]
    public void When_ConvertingTodayWithSubstitution_Then_DaysAreToday()
    {
        var (result, days, _, _) = StrToDate("Today", null, FormatDos, DtfSubst);

        Assert.Equal(0xFFFFFFFFU, result);
        Assert.Equal((DateTime.Now.Date - new DateTime(1978, 1, 1)).Days, days);
    }

    [Theory]
    [InlineData("28 Oct 1987", null)]
    [InlineData("32-Jan-93", null)]
    [InlineData("15-Foo-93", null)]
    [InlineData("Today", null)]
    [InlineData(null, "25:00")]
    public void When_ConvertingInvalidDateOrTime_Then_ZeroIsReturnedAndDateStampIsUnchanged(string date,
        string time)
    {
        var (result, days, minutes, ticks) = StrToDate(date, time, FormatDos, 0, 5, 6, 7);

        Assert.Equal(0U, result);
        Assert.Equal(5, days);
        Assert.Equal(6, minutes);
        Assert.Equal(7, ticks);
    }
}
