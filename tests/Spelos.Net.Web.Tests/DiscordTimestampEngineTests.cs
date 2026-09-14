using Spelos.Net.Web.Tools;

namespace Spelos.Net.Web.Tests;

public sealed class DiscordTimestampEngineTests
{
    public static TheoryData<DiscordTimestampStyle, string> Styles => new()
    {
        { DiscordTimestampStyle.ShortTime, "t" }, { DiscordTimestampStyle.LongTime, "T" },
        { DiscordTimestampStyle.ShortDate, "d" }, { DiscordTimestampStyle.LongDate, "D" },
        { DiscordTimestampStyle.LongDateShortTime, "f" }, { DiscordTimestampStyle.FullDateTime, "F" },
        { DiscordTimestampStyle.ShortDateTime, "s" }, { DiscordTimestampStyle.ShortDateTimeSeconds, "S" },
        { DiscordTimestampStyle.Relative, "R" },
    };

    [Theory, MemberData(nameof(Styles))]
    public void Every_style_produces_explicit_Discord_timestamp_syntax(DiscordTimestampStyle style, string code)
    {
        Assert.Equal($"<t:1789401600:{code}>", DiscordTimestampEngine.Markup(1789401600, style));
    }

    [Fact]
    public void Default_style_is_explicit_long_date_with_short_time()
    {
        Assert.Equal("<t:0:f>", DiscordTimestampEngine.Markup(0));
    }

    [Fact]
    public void Local_minute_conversion_has_zero_seconds_and_supports_pre_epoch_instants()
    {
        var utc = TimeZoneInfo.Utc;
        var result = DiscordTimestampEngine.ResolveLocal(new(1969, 12, 31), new(23, 59), utc);

        Assert.True(result.IsValid);
        Assert.Equal(-60, result.UnixSeconds);
        Assert.Equal(0, result.Instant!.Value.Second);
    }

    [Fact]
    public void Presets_preserve_the_parts_promised_by_their_kind()
    {
        var monday = new DateTimeOffset(2026, 9, 14, 8, 20, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 9, 15), DiscordTimestampEngine.Tomorrow(monday));
        Assert.Equal(new DateOnly(2026, 9, 21), DiscordTimestampEngine.NextMonday(monday));
        Assert.Equal(new TimeOnly(9, 0), DiscordTimestampEngine.ClockTime(9));
        Assert.Equal(monday.AddMinutes(15), DiscordTimestampEngine.AddElapsed(monday, TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void Time_zone_note_has_exact_blank_line_placement()
    {
        Assert.Equal("<t:0:F>\n\n> This time is shown in your local time zone, based on your Discord and device settings.",
            DiscordTimestampEngine.Output(0, DiscordTimestampStyle.FullDateTime, includeTimeZoneNote: true));
    }

    [Fact]
    public void Gap_is_invalid_and_overlap_defaults_earlier_with_later_available()
    {
        var zone = FindPrague();
        var gap = DiscordTimestampEngine.ResolveLocal(new(2026, 3, 29), new(2, 30), zone);
        var overlapEarly = DiscordTimestampEngine.ResolveLocal(new(2026, 10, 25), new(2, 30), zone);
        var overlapLate = DiscordTimestampEngine.ResolveLocal(new(2026, 10, 25), new(2, 30), zone, useLaterOccurrence: true);

        Assert.False(gap.IsValid);
        Assert.NotNull(gap.NearestValidLocalTime);
        Assert.True(overlapEarly.IsAmbiguous);
        Assert.Equal(3600, overlapLate.UnixSeconds - overlapEarly.UnixSeconds);
    }

    [Fact]
    public void Gap_offer_is_the_nearest_valid_local_minute()
    {
        var result = DiscordTimestampEngine.ResolveLocal(new(2026, 3, 29), new(2, 5), FindPrague());

        Assert.False(result.IsValid);
        Assert.Equal(new DateTime(2026, 3, 29, 1, 59, 0), result.NearestValidLocalTime);
    }

    [Fact]
    public void Past_status_and_unrepresentable_instants_are_deterministic()
    {
        Assert.True(DiscordTimestampEngine.IsPast(DateTimeOffset.UnixEpoch.AddSeconds(-1), DateTimeOffset.UnixEpoch));
        var farEast = TimeZoneInfo.CreateCustomTimeZone("UTC+14", TimeSpan.FromHours(14), "UTC+14", "UTC+14");
        var invalid = DiscordTimestampEngine.ResolveLocal(DateOnly.MinValue, TimeOnly.MinValue, farEast);

        Assert.False(invalid.IsValid);
        Assert.NotNull(invalid.Error);
    }

    private static TimeZoneInfo FindPrague()
    {
        foreach (var id in new[] { "Europe/Prague", "Central Europe Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch (TimeZoneNotFoundException) { }
        }

        throw new InvalidOperationException("Prague time zone unavailable.");
    }
}
