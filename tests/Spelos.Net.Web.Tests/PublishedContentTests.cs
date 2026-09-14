using System.Text.Json;
using System.Text.Json.Serialization;
using Spelos.Net.Web.Content;

namespace Spelos.Net.Web.Tests;

public sealed class PublishedContentTests
{
    [Fact]
    public async Task Committed_link_catalog_is_valid()
    {
        var webRoot = FindRepositoryRoot().GetDirectories("Spelos.Net.Web", SearchOption.AllDirectories)
            .Single(directory => directory.Parent?.Name == "src").GetDirectories("wwwroot").Single();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        await using var stream = File.OpenRead(Path.Combine(webRoot.FullName, "data", "links.json"));
        var links = await JsonSerializer.DeserializeAsync<SiteLink[]>(stream, options);

        Assert.NotNull(links);
        Assert.NotEmpty(links);
        var errors = LinkCatalogValidator.Validate(links, path => File.Exists(Path.Combine(webRoot.FullName, path)));
        Assert.Empty(errors);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Spelos.Net.slnx"))) return directory;
        throw new DirectoryNotFoundException("Could not find the repository root.");
    }
}
