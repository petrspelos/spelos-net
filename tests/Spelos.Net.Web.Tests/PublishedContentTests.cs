using Spelos.Net.Web.Content;

namespace Spelos.Net.Web.Tests;

public sealed class PublishedContentTests
{
    [Fact]
    public async Task Committed_link_catalog_is_valid()
    {
        var webRoot = FindRepositoryRoot().GetDirectories("Spelos.Net.Web", SearchOption.AllDirectories)
            .Single(directory => directory.Parent?.Name == "src").GetDirectories("wwwroot").Single();
        await using var stream = File.OpenRead(Path.Combine(webRoot.FullName, "data", "links.json"));
        var links = await LinkCatalogJson.DeserializeAsync(stream);

        Assert.NotEmpty(links);
        var errors = LinkCatalogValidator.Validate(links, path => IconExistsWithin(webRoot, path));
        Assert.Empty(errors);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Spelos.Net.slnx"))) return directory;
        throw new DirectoryNotFoundException("Could not find the repository root.");
    }

    private static bool IconExistsWithin(DirectoryInfo webRoot, string path)
    {
        var candidate = Path.GetFullPath(Path.Combine(webRoot.FullName, path));
        return candidate.StartsWith(webRoot.FullName + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            File.Exists(candidate);
    }
}
