namespace Spelos.Net.Web.Content;

public sealed class HttpLinkCatalog(HttpClient httpClient) : ILinkCatalog
{
    public async Task<IReadOnlyList<SiteLink>> GetLinksAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = await httpClient.GetStreamAsync("data/links.json", cancellationToken);
        var links = await LinkCatalogJson.DeserializeAsync(stream, cancellationToken);
        var errors = LinkCatalogValidator.Validate(links, _ => true);
        if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        return links;
    }
}
