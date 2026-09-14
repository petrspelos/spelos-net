namespace Spelos.Net.Web.Tools;

public enum DiscordTimestampStyle
{
    ShortTime, LongTime, ShortDate, LongDate, LongDateShortTime,
    FullDateTime, ShortDateTime, ShortDateTimeSeconds, Relative,
}

public sealed record LocalTimeResolution(
    DateTimeOffset? Instant,
    bool IsAmbiguous = false,
    DateTime? NearestValidLocalTime = null,
    string? Error = null)
{
    public bool IsValid => Instant.HasValue;
    public long UnixSeconds => Instant?.ToUnixTimeSeconds() ?? 0;
}

public static class DiscordTimestampEngine
{
    public const string TimeZoneNote = "> This time is shown in your local time zone, based on your Discord and device settings.";

    public static string Markup(long unixSeconds, DiscordTimestampStyle style = DiscordTimestampStyle.LongDateShortTime) =>
        $"<t:{unixSeconds}:{Code(style)}>";

    public static string Output(long unixSeconds, DiscordTimestampStyle style, bool includeTimeZoneNote) =>
        includeTimeZoneNote ? $"{Markup(unixSeconds, style)}\n\n{TimeZoneNote}" : Markup(unixSeconds, style);

    public static LocalTimeResolution ResolveLocal(DateOnly date, TimeOnly time, TimeZoneInfo zone, bool useLaterOccurrence = false)
    {
        DateTime local;
        try { local = date.ToDateTime(time, DateTimeKind.Unspecified); }
        catch (ArgumentOutOfRangeException) { return new(null, Error: "Enter a representable local date and time."); }

        if (zone.IsInvalidTime(local))
        {
            DateTime? nearest = null;
            for (var minutes = 1; minutes < 24 * 60 && nearest is null; minutes++)
            {
                var before = local.AddMinutes(-minutes);
                var after = local.AddMinutes(minutes);
                if (!zone.IsInvalidTime(after)) nearest = after;
                if (!zone.IsInvalidTime(before)) nearest = before;
            }
            return new(null, NearestValidLocalTime: nearest, Error: "That local time does not exist because the clock moves forward.");
        }

        var ambiguous = zone.IsAmbiguousTime(local);
        var offset = ambiguous
            ? zone.GetAmbiguousTimeOffsets(local).OrderDescending().ElementAt(useLaterOccurrence ? 1 : 0)
            : zone.GetUtcOffset(local);

        try { return new(new DateTimeOffset(local, offset), ambiguous); }
        catch (ArgumentOutOfRangeException) { return new(null, Error: "That date and time cannot be represented as a Unix instant."); }
    }

    public static DateTimeOffset AddElapsed(DateTimeOffset instant, TimeSpan duration) => instant.Add(duration);
    public static DateOnly Tomorrow(DateTimeOffset localNow) => DateOnly.FromDateTime(localNow.Date.AddDays(1));
    public static DateOnly NextMonday(DateTimeOffset localNow)
    {
        var days = ((int)DayOfWeek.Monday - (int)localNow.DayOfWeek + 7) % 7;
        return DateOnly.FromDateTime(localNow.Date.AddDays(days == 0 ? 7 : days));
    }

    public static TimeOnly ClockTime(int hour) => new(hour, 0);
    public static bool IsPast(DateTimeOffset instant, DateTimeOffset now) => instant < now;

    public static string Code(DiscordTimestampStyle style) => style switch
    {
        DiscordTimestampStyle.ShortTime => "t", DiscordTimestampStyle.LongTime => "T",
        DiscordTimestampStyle.ShortDate => "d", DiscordTimestampStyle.LongDate => "D",
        DiscordTimestampStyle.LongDateShortTime => "f", DiscordTimestampStyle.FullDateTime => "F",
        DiscordTimestampStyle.ShortDateTime => "s", DiscordTimestampStyle.ShortDateTimeSeconds => "S",
        DiscordTimestampStyle.Relative => "R", _ => throw new ArgumentOutOfRangeException(nameof(style)),
    };
}
