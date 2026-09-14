namespace Spelos.Net.Web.Content;

public interface ILinkCatalog
{
    Task<IReadOnlyList<SiteLink>> GetLinksAsync(CancellationToken cancellationToken = default);
}
