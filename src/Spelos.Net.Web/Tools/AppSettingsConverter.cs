using System.Text.Json;
using System.Text.RegularExpressions;

namespace Spelos.Net.Web.Tools;

public enum ConfigurationOutputFormat { Compose, PowerShell }
public sealed record ConfigurationError(string Path, string Message);
public sealed record ConfigurationConversion(string? Output, IReadOnlyList<ConfigurationError> Errors);

public static class AppSettingsConverter
{
    private static readonly string[] ConnectionPrefixes =
    [
        "SQLCONNSTR_", "SQLAZURECONNSTR_", "MYSQLCONNSTR_", "CUSTOMCONNSTR_", "POSTGRESQLCONNSTR_",
        "APIHUBCONNSTR_", "DOCDBCONNSTR_", "EVENTHUBCONNSTR_", "NOTIFICATIONHUBCONNSTR_", "REDISCACHECONNSTR_", "SERVICEBUSCONNSTR_"
    ];

    public static ConfigurationConversion Convert(string json, string prefix = "", ConfigurationOutputFormat format = ConfigurationOutputFormat.Compose)
    {
        var errors = new List<ConfigurationError>();
        var entries = new List<Entry>();
        try
        {
            using var document = JsonDocument.Parse(json, new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                errors.Add(new("$", "The root must be a JSON object."));
            else
                Flatten(document.RootElement, "$", [], entries, errors, new(StringComparer.OrdinalIgnoreCase));
        }
        catch (JsonException exception)
        {
            errors.Add(new(exception.Path ?? "$", $"Invalid JSON at line {exception.LineNumber + 1}, column {exception.BytePositionInLine + 1}."));
        }
        catch (InvalidOperationException)
        {
            errors.Add(new("$", "Invalid Unicode in a JSON property name."));
        }
        if (!Enum.IsDefined(format)) errors.Add(new("$", "Select a supported output format."));
        foreach (var entry in entries)
        {
            var name = prefix + entry.Name;
            if (name.Contains('\0') || name.Contains('=') || name.Length == 0)
                errors.Add(new(entry.Path, "Environment names must be nonempty and cannot contain NUL or '='."));
            if (!Normalize(name).StartsWith(Normalize(prefix), StringComparison.OrdinalIgnoreCase) ||
                Normalize(name)[Normalize(prefix).Length..] != entry.ConfigurationPath)
                errors.Add(new(entry.Path, "The prefix or underscores at a path boundary change the effective .NET configuration path."));
            if (ConnectionPrefixes.Any(special => name.StartsWith(special, StringComparison.OrdinalIgnoreCase)))
                errors.Add(new(entry.Path, "This name uses a special .NET connection-string prefix. Use a ConnectionStrings section or a different provider prefix."));
            if (entry.Value.Contains('\0')) errors.Add(new(entry.Path, "NUL cannot be preserved in an environment value."));
            if (format == ConfigurationOutputFormat.Compose && !Regex.IsMatch(name, @"\A[\p{L}\p{N}_.\-\[\]]+\z"))
                errors.Add(new(entry.Path, "Compose names may contain letters, numbers, underscores, dots, hyphens and brackets only. Check the exact prefix too."));
            if (format == ConfigurationOutputFormat.PowerShell)
            {
                if (name.Length > 255) errors.Add(new(entry.Path, "Windows User environment names must be at most 255 characters."));
                if (entry.Value.Length == 0) errors.Add(new(entry.Path, "An empty string would delete a Windows User variable. Select Compose to preserve it."));
                if (entry.Value.Length >= 32767) errors.Add(new(entry.Path, "Windows environment values must be shorter than 32767 characters."));
            }
        }
        if (errors.Count > 0) return new(null, errors);
        var output = format == ConfigurationOutputFormat.Compose
            ? string.Concat(entries.Select(entry => $"{prefix}{entry.Name}={ComposeQuote(entry.Value)}\n"))
            : "#requires -Version 7.5\nif (-not $IsWindows) { throw 'This script requires Windows.' }\n" +
              string.Concat(entries.Select(entry => $"[System.Environment]::SetEnvironmentVariable({PowerShellQuote(prefix + entry.Name)}, {PowerShellQuote(entry.Value)}, 'User')\n[System.Environment]::SetEnvironmentVariable({PowerShellQuote(prefix + entry.Name)}, {PowerShellQuote(entry.Value)}, 'Process')\n"));
        return new(output, errors);
    }

    private static string Normalize(string name) => name.Replace("__", ":", StringComparison.Ordinal);
    private static string PowerShellQuote(string value) => value.IndexOfAny(['\r', '\u2018', '\u2019', '\u201a', '\u201b']) >= 0
        ? "[System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String('" + System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value)) + "'))"
        : "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    // Single quotes avoid interpolation. Backslashes adjacent to a quote cannot use the
    // single quote in Compose, so use its double-quoted escape syntax for that case.
    private static string ComposeQuote(string value) => (value.Contains('\\') || value.Contains('\r'))
        ? "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("$", "$$", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal) + "\""
        : "'" + value.Replace("'", "\\'", StringComparison.Ordinal) + "'";

    private static void Flatten(JsonElement element, string path, string[] segments, List<Entry> entries, List<ConfigurationError> errors, Dictionary<string, string> seen)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (!element.EnumerateObject().Any()) errors.Add(new(path, "Empty objects cannot be represented as environment variables."));
                foreach (var property in element.EnumerateObject())
                {
                    var childPath = path + "[" + JsonSerializer.Serialize(property.Name) + "]";
                    if (property.Name.Length == 0 || property.Name.Contains(':') || property.Name.Contains("__", StringComparison.Ordinal))
                        errors.Add(new(childPath, "Property names must be nonempty and cannot contain ':' or '__', which change .NET paths."));
                    var children = segments.Append(property.Name).ToArray();
                    var key = string.Join(':', children);
                    if (seen.TryGetValue(key, out var previous)) errors.Add(new(childPath, $"Duplicate configuration path; conflicts with {previous} (case-insensitive)."));
                    else seen.Add(key, childPath);
                    Flatten(property.Value, childPath, children, entries, errors, seen);
                }
                break;
            case JsonValueKind.Array:
                if (element.GetArrayLength() == 0) errors.Add(new(path, "Empty arrays cannot be represented as environment variables."));
                var index = 0;
                foreach (var child in element.EnumerateArray())
                {
                    var childIndex = index++.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    Flatten(child, $"{path}[{childIndex}]", [.. segments, childIndex], entries, errors, seen);
                }
                break;
            case JsonValueKind.Null:
                errors.Add(new(path, "Null cannot be represented as an environment variable."));
                break;
            default:
                try
                {
                    entries.Add(new(string.Join("__", segments), string.Join(':', segments), path,
                        element.ValueKind == JsonValueKind.String ? element.GetString()! : element.GetRawText()));
                }
                catch (InvalidOperationException) { errors.Add(new(path, "Invalid Unicode: a string contains an unpaired surrogate.")); }
                break;
        }
    }

    private sealed record Entry(string Name, string ConfigurationPath, string Path, string Value);
}
