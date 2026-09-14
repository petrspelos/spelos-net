using Deque.AxeCore.Playwright;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using System.Text.RegularExpressions;
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
        Page.Response += (_, response) =>
        {
            if (response.Status >= 400) _browserErrors.Add($"HTTP {response.Status}: {response.Url}");
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
    public async Task Discord_timestamp_tool_is_discoverable_and_survives_direct_refresh()
    {
        await Page.AddInitScriptAsync("""
            const resolvedOptions = Intl.DateTimeFormat.prototype.resolvedOptions;
            Intl.DateTimeFormat.prototype.resolvedOptions = function () {
                return { ...resolvedOptions.call(this), timeZone: "UTC" };
            };
            """);
        await Page.GotoAsync($"{site.BaseUrl}/tools");
        await Page.GetByRole(AriaRole.Link, new() { Name = "Discord Timestamp Generator" }).ClickAsync();
        await ExpectApplicationAsync(Page.GetByRole(AriaRole.Heading, new() { Name = "Discord Timestamp Generator" }));
        await Page.ReloadAsync();

        await ExpectApplicationAsync(Page.GetByText("Generated Markdown", new() { Exact = true }));
        await Expect(Page.Locator("#discord-generated-output")).ToContainTextAsync(new Regex("^<t:-?\\d+:f>"));
        await Expect(Page.Locator(".live-reference span")).ToHaveTextAsync("UTC");
        await Page.EvaluateAsync("window.compareToggleCount = 0; document.querySelector('details.compare').addEventListener('toggle', () => window.compareToggleCount++)");
        await Page.GetByText("Compare all formats", new() { Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Short time") }).ClickAsync();
        await Expect(Page.Locator("#discord-generated-output")).ToBeFocusedAsync();
        await Expect(Page.Locator("details.compare")).Not.ToHaveAttributeAsync("open", "", new() { Timeout = 2000 });
        await Page.WaitForTimeoutAsync(500);
        Assert.InRange(await Page.EvaluateAsync<int>("window.compareToggleCount"), 1, 2);
        Assert.Empty((await Page.RunAxe()).Violations);
    }

    [Fact]
    public async Task Discord_timestamp_tool_supports_keyboard_editing_copy_and_persisted_preferences_only()
    {
        await Page.GotoAsync($"{site.BaseUrl}/tools/discord-timestamp");
        await ExpectApplicationAsync(Page.GetByLabel("Date", new() { Exact = true }));
        var initialDate = await Page.GetByLabel("Date", new() { Exact = true }).InputValueAsync();

        await Page.GetByLabel("Discord format").SelectOptionAsync("F");
        await Page.GetByLabel("Add a time-zone note").CheckAsync();
        await Page.GetByLabel("Time", new() { Exact = true }).FillAsync("03:17");
        await Page.GetByLabel("Time", new() { Exact = true }).PressAsync("Control+Enter");
        await Expect(Page.Locator(".status-message")).ToContainTextAsync(new Regex("copied|Ctrl\\+C", RegexOptions.IgnoreCase));
        await Expect(Page.Locator("#discord-generated-output")).ToContainTextAsync("\n\n> This time is shown in your local time zone");

        await Page.ReloadAsync();
        await Expect(Page.GetByLabel("Discord format")).ToHaveValueAsync("F");
        await Expect(Page.GetByLabel("Add a time-zone note")).ToBeCheckedAsync();
        Assert.Equal(initialDate, await Page.GetByLabel("Date", new() { Exact = true }).InputValueAsync());
        Assert.NotEqual("03:17", await Page.GetByLabel("Time", new() { Exact = true }).InputValueAsync());
        Assert.Equal(2, await Page.EvaluateAsync<int>("Object.keys(localStorage).filter(key => key.startsWith('spelos.discordTimestamp.')).length"));
    }

    [Fact]
    public async Task Discord_timestamp_copy_success_is_temporary_and_browser_context_has_honest_fallback()
    {
        await Page.Context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"], new() { Origin = site.BaseUrl });
        await Page.GotoAsync($"{site.BaseUrl}/tools/discord-timestamp");
        await ExpectApplicationAsync(Page.GetByRole(AriaRole.Button, new() { Name = "Copy" }));
        await Page.GetByRole(AriaRole.Button, new() { Name = "Copy" }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Copied!" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Copy" })).ToBeVisibleAsync(new() { Timeout = 4000 });

        var fallback = await Page.EvaluateAsync<BrowserZoneContext>("""
            async () => {
                const original = Intl.DateTimeFormat.prototype.resolvedOptions;
                Intl.DateTimeFormat.prototype.resolvedOptions = () => ({});
                const context = (await import('/js/discordTimestamp.js')).getContext();
                Intl.DateTimeFormat.prototype.resolvedOptions = original;
                return context;
            }
            """);
        Assert.Null(fallback.TimeZone);
        Assert.Matches("^UTC[+-]\\d{2}:\\d{2}$", fallback.OffsetLabel);
    }

    [Fact]
    public async Task Discord_timestamp_tool_has_no_mobile_or_two_hundred_percent_overflow()
    {
        await Page.SetViewportSizeAsync(375, 667);
        await Page.GotoAsync($"{site.BaseUrl}/tools/discord-timestamp");
        await ExpectApplicationAsync(Page.GetByRole(AriaRole.Heading, new() { Name = "Discord Timestamp Generator" }));
        Assert.False(await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth > document.documentElement.clientWidth"));
        await Page.EvaluateAsync("document.documentElement.style.fontSize = '200%'");
        Assert.False(await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth > document.documentElement.clientWidth"));
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
    public async Task Background_uses_intrinsic_image_size_and_tiles_in_both_directions()
    {
        await Page.GotoAsync(site.BaseUrl);
        await ExpectApplicationAsync(Page.GetByLabel("Peter's programmer profile"));

        var backgroundSize = await Page.Locator("html").EvaluateAsync<string>(
            "element => getComputedStyle(element).backgroundSize");
        var backgroundRepeat = await Page.Locator("html").EvaluateAsync<string>(
            "element => getComputedStyle(element).backgroundRepeat");

        Assert.Equal("auto", backgroundSize);
        Assert.Equal("repeat", backgroundRepeat);
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

    private sealed class BrowserZoneContext
    {
        public string? TimeZone { get; set; }
        public string OffsetLabel { get; set; } = "";
        public int OffsetMinutes { get; set; }
    }
}
