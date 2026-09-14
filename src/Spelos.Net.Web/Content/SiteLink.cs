namespace Spelos.Net.Web.Content;

public sealed record SiteLink(string Name, string Destination, string IconPath, LinkKind Kind);

public enum LinkKind { External, Internal }
