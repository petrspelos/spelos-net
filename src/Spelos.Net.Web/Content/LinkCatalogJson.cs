using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spelos.Net.Web.Content;

public static class LinkCatalogJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static async Task<SiteLink[]> DeserializeAsync(Stream json, CancellationToken cancellationToken = default) =>
        await JsonSerializer.DeserializeAsync<SiteLink[]>(json, Options, cancellationToken)
        ?? throw new InvalidDataException("The link catalog is empty.");
}
