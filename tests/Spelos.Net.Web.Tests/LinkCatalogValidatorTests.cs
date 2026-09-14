using Spelos.Net.Web.Content;

namespace Spelos.Net.Web.Tests;

public sealed class LinkCatalogValidatorTests
{
    [Fact]
    public void Valid_catalog_is_accepted()
    {
        var links = new[]
        {
            new SiteLink("GitHub", "https://github.com/petrspelos", "img/logo-github.webp", LinkKind.External),
            new SiteLink("Tools", "/tools", "img/tools.svg", LinkKind.Internal),
        };

        Assert.Empty(LinkCatalogValidator.Validate(links, _ => true));
    }

    [Fact]
    public void Unsafe_and_duplicate_entries_are_rejected()
    {
        var links = new[]
        {
            new SiteLink("GitHub", "http://github.com/petrspelos", "img/missing.webp", LinkKind.External),
            new SiteLink("GitHub", "tools", "img/missing.webp", LinkKind.Internal),
            new SiteLink("Mystery", "/mystery", "img/missing.webp", (LinkKind)99),
            new SiteLink("Escape", "/escape", "../secret.txt", LinkKind.Internal),
            new SiteLink("Remote", "/remote", "https://example.com/icon.svg", LinkKind.Internal),
        };

        var errors = LinkCatalogValidator.Validate(links, _ => false);

        Assert.Contains(errors, error => error.Contains("HTTPS", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("root-relative", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Duplicate name", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("does not exist", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("unknown kind", StringComparison.Ordinal));
        Assert.Equal(2, errors.Count(error => error.Contains("beneath wwwroot", StringComparison.Ordinal)));
    }

    [Fact]
    public void Missing_kind_is_rejected()
    {
        var link = new SiteLink("Example", "https://example.com", "img/example.svg", LinkKind.Unspecified);

        var errors = LinkCatalogValidator.Validate([link], _ => true);

        Assert.Contains(errors, error => error.Contains("unknown kind", StringComparison.Ordinal));
    }
}
