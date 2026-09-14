using Deque.AxeCore.Playwright;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using Xunit.Sdk;

namespace Spelos.Net.Web.BrowserTests;

public sealed class SiteTests(PublishedSiteFixture site) : PageTest, IClassFixture<PublishedSiteFixture>
{
    private readonly List<string> _browserErrors = [];

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        Page.PageError += (_, error) => _browserErrors.Add($"Page error: {error}");
        Page.Console += (_, message) =>
        {
            if (message.Type == "error") _browserErrors.Add($"Console error: {message.Text}");
        };
    }

    [Fact]
    public async Task Home_exposes_external_and_internal_links_without_accessibility_violations()
    {
        await Page.GotoAsync(site.BaseUrl);
        await ExpectApplicationAsync(Page.GetByLabel("Peter's programmer profile"));
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Tools" })).ToHaveAttributeAsync("href", "/tools");
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "GitHub (opens in a new tab)" })).ToHaveAttributeAsync("target", "_blank");

        var accessibility = await Page.RunAxe();
        Assert.Empty(accessibility.Violations);

        await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.Locator(":focus")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Tools_route_survives_refresh_and_links_home()
    {
        await Page.GotoAsync($"{site.BaseUrl}/tools");
        await ExpectApplicationAsync(Page.GetByRole(AriaRole.Heading, new() { Name = "Tools" }));
        await Page.ReloadAsync();
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Back to home" })).ToHaveAttributeAsync("href", "/");
        Assert.Empty((await Page.RunAxe()).Violations);
    }

    [Fact]
    public async Task Home_fits_a_common_mobile_viewport()
    {
        await Page.SetViewportSizeAsync(375, 667);
        await Page.GotoAsync(site.BaseUrl);
        var overflows = await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth > document.documentElement.clientWidth");
        Assert.False(overflows);

        await Page.EvaluateAsync("document.documentElement.style.fontSize = '200%'");
        overflows = await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth > document.documentElement.clientWidth");
        Assert.False(overflows);
    }

    [Fact]
    public async Task Unknown_route_uses_branded_accessible_not_found_view()
    {
        await Page.GotoAsync($"{site.BaseUrl}/missing-page");
        await ExpectApplicationAsync(Page.GetByRole(AriaRole.Heading, new() { Name = "Page not found" }));
        Assert.Empty((await Page.RunAxe()).Violations);
    }

    private async Task ExpectApplicationAsync(ILocator locator)
    {
        try
        {
            await Expect(locator).ToBeVisibleAsync();
        }
        catch (PlaywrightException exception)
        {
            throw new XunitException($"{exception.Message}{Environment.NewLine}{string.Join(Environment.NewLine, _browserErrors)}");
        }
    }
}
