namespace Spelos.Net.Web.Content;

public static class LinkCatalogValidator
{
    public static IReadOnlyList<string> Validate(IReadOnlyList<SiteLink> links, Func<string, bool> iconExists)
    {
        var errors = new List<string>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var link in links)
        {
            if (!Enum.IsDefined(link.Kind) || link.Kind is LinkKind.Unspecified)
                errors.Add($"{link.Name}: unknown kind '{link.Kind}'.");
            if (string.IsNullOrWhiteSpace(link.Name)) errors.Add("A link name is required.");
            else if (!names.Add(link.Name)) errors.Add($"Duplicate name: {link.Name}.");
            if (string.IsNullOrWhiteSpace(link.Destination)) errors.Add($"{link.Name}: a destination is required.");
            else if (!destinations.Add(link.Destination)) errors.Add($"Duplicate destination: {link.Destination}.");
            if (link.Kind is LinkKind.External && (!Uri.TryCreate(link.Destination, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
                errors.Add($"{link.Name}: external destinations must use absolute HTTPS URLs.");
            if (link.Kind is LinkKind.Internal && (!link.Destination.StartsWith('/') || link.Destination.StartsWith("//", StringComparison.Ordinal)))
                errors.Add($"{link.Name}: internal destinations must be root-relative paths.");
            if (string.IsNullOrWhiteSpace(link.IconPath)) errors.Add($"{link.Name}: an icon path is required.");
            else if (!IsSafeIconPath(link.IconPath)) errors.Add($"{link.Name}: icon paths must remain beneath wwwroot/img.");
            else if (!iconExists(link.IconPath)) errors.Add($"{link.Name}: icon '{link.IconPath}' does not exist.");
        }
        return errors;
    }

    private static bool IsSafeIconPath(string path) =>
        path.StartsWith("img/", StringComparison.Ordinal) &&
        !path.Contains('\\') &&
        !path.Contains('?') &&
        !path.Contains('#') &&
        path.Split('/').All(segment => segment is not "" and not "." and not "..");
}
