using Deque.AxeCore.Playwright;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace Spelos.Net.Web.BrowserTests;

public sealed class SiteTests : PageTest
{
    private static string BaseUrl => Environment.GetEnvironmentVariable("SPELOS_BASE_URL") ?? "http://127.0.0.1:5080";

    [Fact]
    public async Task Home_exposes_profile_links_without_accessibility_violations()
    {
        await Page.GotoAsync(BaseUrl);
        await Expect(Page.GetByLabel("Peter's programmer profile")).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Tools" })).ToHaveAttributeAsync("href", "/tools");
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "GitHub (opens in a new tab)" })).ToHaveAttributeAsync("target", "_blank");

        var accessibility = await Page.RunAxe();
        Assert.Empty(accessibility.Violations);
    }

    [Fact]
    public async Task Tools_route_survives_refresh_and_links_home()
    {
        await Page.GotoAsync($"{BaseUrl}/tools");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Tools" })).ToBeVisibleAsync();
        await Page.ReloadAsync();
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Back to home" })).ToHaveAttributeAsync("href", "/");
    }

    [Fact]
    public async Task Home_fits_a_common_mobile_viewport()
    {
        await Page.SetViewportSizeAsync(375, 667);
        await Page.GotoAsync(BaseUrl);
        var overflows = await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth > document.documentElement.clientWidth");
        Assert.False(overflows);
    }
}
