using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spelos.Net.Web.Content;

public sealed class HttpLinkCatalog(HttpClient httpClient) : ILinkCatalog
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public async Task<IReadOnlyList<SiteLink>> GetLinksAsync(CancellationToken cancellationToken = default)
    {
        var links = await httpClient.GetFromJsonAsync<SiteLink[]>("data/links.json", SerializerOptions, cancellationToken)
            ?? throw new InvalidDataException("The link catalog is empty.");
        var errors = LinkCatalogValidator.Validate(links, _ => true);
        if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        return links;
    }
}
