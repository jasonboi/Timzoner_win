using Timzoner.Core;
using Xunit;

namespace Timzoner.Tests;

public sealed class ZoneTests
{
    [Theory]
    [InlineData(540, "(UTC+09:00) Tokyo Standard Time")]
    [InlineData(-600, "(UTC-10:00) Tokyo Standard Time")]
    [InlineData(345, "(UTC+05:45) Tokyo Standard Time")]
    [InlineData(-210, "(UTC-03:30) Tokyo Standard Time")]
    [InlineData(0, "(UTC) Tokyo Standard Time")]
    public void EnglishLabelRetainsUtcOffset(int offsetMinutes, string expected)
    {
        var zone = new Zone("Tokyo Standard Time", "Localized name", TimeSpan.FromMinutes(offsetMinutes));
        Assert.Equal(expected, zone.EnglishName);
    }
}
