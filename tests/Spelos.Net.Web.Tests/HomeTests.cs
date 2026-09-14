using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Spelos.Net.Web.Content;
using Spelos.Net.Web.Pages;

namespace Spelos.Net.Web.Tests;

public sealed class HomeTests : BunitContext
{
    [Fact]
    public void Home_renders_external_and_internal_links_with_distinct_navigation_behavior()
    {
        Services.AddSingleton<ILinkCatalog>(new StubLinkCatalog(
        [
            new("GitHub", "https://github.com/petrspelos", "img/logo-github.webp", LinkKind.External),
            new("Tools", "/tools", "img/tools.svg", LinkKind.Internal),
        ]));

        var cut = Render<Home>();
        cut.WaitForAssertion(() =>
        {
            var github = cut.Find("a[href='https://github.com/petrspelos']");
            Assert.Equal("_blank", github.GetAttribute("target"));
            Assert.Equal("noopener noreferrer", github.GetAttribute("rel"));
            Assert.Contains("opens in a new tab", github.GetAttribute("aria-label"));

            var tools = cut.Find("a[href='/tools']");
            Assert.Null(tools.GetAttribute("target"));
            Assert.Null(tools.GetAttribute("rel"));
        });
    }

    private sealed class StubLinkCatalog(IReadOnlyList<SiteLink> links) : ILinkCatalog
    {
        public Task<IReadOnlyList<SiteLink>> GetLinksAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(links);
    }
}
