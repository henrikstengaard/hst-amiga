namespace Hst.Amiga.Tests;

using System;
using Xunit;

public class GivenDateHelper
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(19, 0)]
    [InlineData(20, 1)]
    [InlineData(500, 25)]
    [InlineData(980, 49)]
    [InlineData(990, 49)]
    [InlineData(999, 49)]
    public void WhenConvertDateAtEndOfMinuteThenTicksAreWithinMinute(int milliseconds, int expectedMillisecondTicks)
    {
        // arrange - date at 23:59:59 with milliseconds
        var date = new DateTime(2024, 1, 1, 23, 59, 59, milliseconds);

        // act - convert to amiga date
        var amigaDate = DateHelper.ConvertToAmigaDate(date);

        // assert - minutes and ticks are within day and minute
        Assert.Equal(23 * 60 + 59, amigaDate.Minutes);
        Assert.Equal(59 * 50 + expectedMillisecondTicks, amigaDate.Ticks);
        Assert.True(amigaDate.Ticks < 60 * 50);
    }

    [Fact]
    public void WhenConvertDateWithWholeTicksThenDateIsUnchangedAfterConvertingBack()
    {
        // arrange - date with milliseconds being a whole number of ticks (20 ms per tick)
        var date = new DateTime(2024, 6, 15, 12, 34, 56, 980);

        // act - convert to amiga date and back
        var amigaDate = DateHelper.ConvertToAmigaDate(date);
        var convertedDate = DateHelper.ConvertToDate(amigaDate.Days, amigaDate.Minutes, amigaDate.Ticks);

        // assert - date is unchanged
        Assert.Equal(date, convertedDate);
    }
}
