using Deque.AxeCore.Playwright;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace Spelos.Net.Web.BrowserTests;

public sealed class SiteTests(PublishedSiteFixture site) : PageTest, IClassFixture<PublishedSiteFixture>
{
    [Fact]
    public async Task Home_exposes_external_and_internal_links_without_accessibility_violations()
    {
        await Page.GotoAsync(site.BaseUrl);
        await Expect(Page.GetByLabel("Peter's programmer profile")).ToBeVisibleAsync();
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
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Tools" })).ToBeVisibleAsync();
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
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Page not found" })).ToBeVisibleAsync();
        Assert.Empty((await Page.RunAxe()).Violations);
    }
}
