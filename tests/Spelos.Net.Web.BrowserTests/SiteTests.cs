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

        await ExpectApplicationAsync(Page.GetByText("Ready to paste", new() { Exact = true }));
        await Expect(Page.Locator("#discord-generated-output")).ToContainTextAsync(new Regex("^<t:-?\\d+:f>"));
        await Expect(Page.Locator(".zone-hint strong")).ToHaveTextAsync("UTC");
        await Page.GetByLabel("Short time", new() { Exact = true }).CheckAsync();
        await Expect(Page.GetByLabel("Short time", new() { Exact = true })).ToBeCheckedAsync();
        await Expect(Page.Locator("#discord-generated-output")).ToContainTextAsync(new Regex("^<t:-?\\d+:t>"));
        Assert.Empty((await Page.RunAxe()).Violations);
    }

    [Fact]
    public async Task Discord_timestamp_tool_supports_keyboard_editing_copy_and_persisted_preferences_only()
    {
        await Page.GotoAsync($"{site.BaseUrl}/tools/discord-timestamp");
        await ExpectApplicationAsync(Page.GetByLabel("Date", new() { Exact = true }));
        var initialDate = await Page.GetByLabel("Date", new() { Exact = true }).InputValueAsync();

        await Page.GetByLabel("Full date and time", new() { Exact = true }).CheckAsync();
        await Page.GetByText("Options and help", new() { Exact = true }).ClickAsync();
        await Page.GetByLabel("Include a time-zone note when copying").CheckAsync();
        await Page.GetByLabel("Time", new() { Exact = true }).FillAsync("03:17");
        await Page.GetByLabel("Time", new() { Exact = true }).PressAsync("Control+Enter");
        await Expect(Page.Locator(".status-message")).ToContainTextAsync(new Regex("copied|Ctrl\\+C", RegexOptions.IgnoreCase));
        await Expect(Page.Locator("#discord-generated-output")).ToContainTextAsync("\n\n> This time is shown in your local time zone");

        await Page.ReloadAsync();
        await Expect(Page.GetByLabel("Full date and time", new() { Exact = true })).ToBeCheckedAsync();
        await Expect(Page.GetByLabel("Include a time-zone note when copying")).ToBeCheckedAsync();
        Assert.Equal(initialDate, await Page.GetByLabel("Date", new() { Exact = true }).InputValueAsync());
        Assert.NotEqual("03:17", await Page.GetByLabel("Time", new() { Exact = true }).InputValueAsync());
        Assert.Equal(2, await Page.EvaluateAsync<int>("Object.keys(localStorage).filter(key => key.startsWith('spelos.discordTimestamp.')).length"));
    }

    [Fact]
    public async Task Discord_timestamp_copy_success_is_temporary_and_browser_context_has_honest_fallback()
    {
        await Page.Context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"], new() { Origin = site.BaseUrl });
        await Page.GotoAsync($"{site.BaseUrl}/tools/discord-timestamp");
        await ExpectApplicationAsync(Page.GetByRole(AriaRole.Button, new() { Name = "Copy Discord timestamp" }));
        await Page.GetByRole(AriaRole.Button, new() { Name = "Copy Discord timestamp" }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Copied!" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Copy Discord timestamp" })).ToBeVisibleAsync(new() { Timeout = 4000 });

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
        await AssertNoHorizontalOverflowAsync();
        await Page.EvaluateAsync("document.documentElement.style.fontSize = '200%'");
        await AssertNoHorizontalOverflowAsync();
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

    [Fact]
    public async Task Appsettings_converter_supports_upload_copy_download_and_invalidates_stale_output()
    {
        await Page.Context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"], new() { Origin = site.BaseUrl });
        await Page.GotoAsync($"{site.BaseUrl}/tools");
        await Page.GetByRole(AriaRole.Link, new() { Name = "Appsettings Configuration Converter" }).ClickAsync();
        await ExpectApplicationAsync(Page.GetByRole(AriaRole.Heading, new() { Name = "Appsettings Configuration Converter" }));
        await Page.ReloadAsync();
        var requests = new List<IRequest>();
        Page.Request += (_, request) => requests.Add(request);
        var json = Page.GetByLabel("Appsettings JSON", new() { Exact = true });
        await ExpectApplicationAsync(json);
        await Page.GetByLabel("Upload JSON file").SetInputFilesAsync(new FilePayload { Name = "appsettings.json", MimeType = "application/json", Buffer = System.Text.Encoding.UTF8.GetBytes("{\"Options\":{\"Name\":\"Ada\"}}") });
        await Expect(json).ToHaveValueAsync("{\"Options\":{\"Name\":\"Ada\"}}");
        await Page.GetByLabel("Exact provider prefix").FillAsync("APP_");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Convert", Exact = true }).FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        var output = Page.GetByLabel("Generated output");
        await Expect(output).ToHaveValueAsync("APP_Options__Name='Ada'\n");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Copy", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Status)).ToHaveTextAsync("Output copied.");
        Assert.Equal("APP_Options__Name='Ada'\n", (await Page.EvaluateAsync<string>("navigator.clipboard.readText()")).Replace("\r\n", "\n", StringComparison.Ordinal));
        var download = await Page.RunAndWaitForDownloadAsync(() => Page.GetByRole(AriaRole.Button, new() { Name = "Download", Exact = true }).ClickAsync());
        Assert.Equal("appsettings.env", download.SuggestedFilename);
        Assert.Equal("APP_Options__Name='Ada'\n", await File.ReadAllTextAsync((await download.PathAsync())!));
        Assert.Empty((await Page.RunAxe()).Violations);
        await Page.GetByLabel("Output format").SelectOptionAsync("PowerShell");
        await Expect(output).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Copy", Exact = true })).ToBeDisabledAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Convert", Exact = true }).ClickAsync();
        Assert.Contains("'User'", await output.InputValueAsync());
        var script = await Page.RunAndWaitForDownloadAsync(() => Page.GetByRole(AriaRole.Button, new() { Name = "Download", Exact = true }).ClickAsync());
        Assert.Equal("appsettings.ps1", script.SuggestedFilename);
        await json.FillAsync("{\"Bad\":null}");
        await Expect(output).ToHaveCountAsync(0);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Convert", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("$[\"Bad\"]");
        Assert.Empty((await Page.RunAxe()).Violations);
        await Page.SetViewportSizeAsync(375, 667);
        await AssertNoHorizontalOverflowAsync();
        await Page.EvaluateAsync("document.documentElement.style.fontSize = '200%'");
        await AssertNoHorizontalOverflowAsync();
        Assert.DoesNotContain(requests, request => request.Method != "GET" || request.Url.Contains("Ada", StringComparison.Ordinal) || request.PostData is not null);
        Assert.Equal(0, await Page.EvaluateAsync<int>("localStorage.length + sessionStorage.length"));
        await Page.ReloadAsync();
        await Expect(json).ToHaveValueAsync("");
    }

    [Fact]
    public async Task Json_toolkit_is_discoverable_formats_live_and_explains_errors()
    {
        await Page.Context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"], new() { Origin = site.BaseUrl });
        await Page.GotoAsync($"{site.BaseUrl}/tools");
        await Page.GetByRole(AriaRole.Link, new() { Name = "JSON Toolkit" }).ClickAsync();
        await ExpectApplicationAsync(Page.GetByRole(AriaRole.Heading, new() { Name = "JSON Toolkit" }));
        await Page.ReloadAsync();
        var input = Page.GetByLabel("Input", new() { Exact = true });
        var output = Page.GetByLabel("Formatted JSON", new() { Exact = true });
        await ExpectApplicationAsync(input);
        await Expect(output).ToHaveValueAsync("");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Copy", Exact = true })).ToBeDisabledAsync();

        await input.PressSequentiallyAsync("""{"b":[1,2],"a":{}}""");
        await Expect(output).ToHaveValueAsync("{\n  \"b\": [\n    1,\n    2\n  ],\n  \"a\": {}\n}");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Copy", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Status)).ToHaveTextAsync("Formatted JSON copied.");
        Assert.Equal("{\n  \"b\": [\n    1,\n    2\n  ],\n  \"a\": {}\n}", (await Page.EvaluateAsync<string>("navigator.clipboard.readText()")).Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Empty((await Page.RunAxe()).Violations);

        await input.FillAsync("{\n  \"é\": 1,\n  x: 2\n}");
        await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("Line 3, column 3: Property names must be in double quotes.");
        await Expect(output).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Copy", Exact = true })).ToBeDisabledAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Go to line 3:3" }).ClickAsync();
        await Expect(input).ToBeFocusedAsync();
        Assert.Equal("x", await input.EvaluateAsync<string>("element => element.value[element.selectionStart]"));
        Assert.Empty((await Page.RunAxe()).Violations);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Clear", Exact = true }).ClickAsync();
        await Expect(input).ToHaveValueAsync("");
        await Expect(output).ToHaveValueAsync("");
        await Expect(Page.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Json_toolkit_persists_only_formatting_settings_and_never_sends_the_json()
    {
        const string marker = "privacy-marker-7f3a";
        var requests = new List<IRequest>();
        Page.Request += (_, request) => requests.Add(request);
        await Page.GotoAsync($"{site.BaseUrl}/tools/json-toolkit");
        var input = Page.GetByLabel("Input", new() { Exact = true });
        var output = Page.GetByLabel("Formatted JSON", new() { Exact = true });
        await ExpectApplicationAsync(input);
        await input.FillAsync($$"""{"secret":["{{marker}}"]}""");
        await Expect(output).ToHaveValueAsync($"{{\n  \"secret\": [\n    \"{marker}\"\n  ]\n}}");

        await Page.GetByLabel("Tabs", new() { Exact = true }).CheckAsync();
        await Expect(Page.GetByLabel("Indent size")).ToHaveCountAsync(0);
        await Expect(output).ToHaveValueAsync($"{{\n\t\"secret\": [\n\t\t\"{marker}\"\n\t]\n}}");
        await Page.GetByLabel("Spaces", new() { Exact = true }).CheckAsync();
        await Page.GetByLabel("Indent size").SelectOptionAsync("4");
        await Expect(output).ToHaveValueAsync($"{{\n    \"secret\": [\n        \"{marker}\"\n    ]\n}}");
        await Page.GetByLabel("Minified", new() { Exact = true }).CheckAsync();
        await Expect(Page.GetByLabel("Tabs", new() { Exact = true })).ToHaveCountAsync(0);
        await Expect(output).ToHaveValueAsync($$"""{"secret":["{{marker}}"]}""");

        var storedKeys = await Page.EvaluateAsync<string[]>("Object.keys(localStorage)");
        Assert.All(storedKeys, key => Assert.StartsWith("spelos.jsonFormatter.", key, StringComparison.Ordinal));
        Assert.DoesNotContain(marker, await Page.EvaluateAsync<string>("JSON.stringify(Object.entries(localStorage))"), StringComparison.Ordinal);
        Assert.Equal(0, await Page.EvaluateAsync<int>("sessionStorage.length"));

        await Page.ReloadAsync();
        await ExpectApplicationAsync(input);
        await Expect(Page.GetByLabel("Minified", new() { Exact = true })).ToBeCheckedAsync();
        await Expect(input).ToHaveValueAsync("");
        await Page.GetByLabel("Indented", new() { Exact = true }).CheckAsync();
        await Expect(Page.GetByLabel("Spaces", new() { Exact = true })).ToBeCheckedAsync();
        await Expect(Page.GetByLabel("Indent size")).ToHaveValueAsync("4");
        Assert.DoesNotContain(requests, request => request.Method != "GET" || request.Url.Contains(marker, StringComparison.Ordinal) || request.PostData is not null);
    }

    [Fact]
    public async Task Json_toolkit_has_no_mobile_or_two_hundred_percent_overflow()
    {
        await Page.SetViewportSizeAsync(375, 667);
        await Page.GotoAsync($"{site.BaseUrl}/tools/json-toolkit");
        var input = Page.GetByLabel("Input", new() { Exact = true });
        await ExpectApplicationAsync(input);
        await input.FillAsync($$"""{"long":"{{new string('x', 300)}}"}""");
        await Expect(Page.GetByLabel("Formatted JSON", new() { Exact = true })).ToHaveValueAsync(new Regex("long"));
        await AssertNoHorizontalOverflowAsync();
        await input.FillAsync($$"""{"long":"{{new string('x', 300)}}" x}""");
        await Expect(Page.GetByRole(AriaRole.Alert)).ToBeVisibleAsync();
        await AssertNoHorizontalOverflowAsync();
        await Page.EvaluateAsync("document.documentElement.style.fontSize = '200%'");
        await AssertNoHorizontalOverflowAsync();
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

    private async Task AssertNoHorizontalOverflowAsync()
    {
        var overflowingElements = await Page.EvaluateAsync<string[]>("""
            () => [...document.querySelectorAll('body *')]
                .filter(element => {
                    const bounds = element.getBoundingClientRect();
                    return bounds.left < 0 || bounds.right > document.documentElement.clientWidth || element.scrollWidth > element.clientWidth;
                })
                .map(element => `${element.tagName.toLowerCase()}.${element.className}: bounds ${Math.round(element.getBoundingClientRect().left)}..${Math.round(element.getBoundingClientRect().right)}, width ${element.clientWidth}/${element.scrollWidth}`)
            """);

        Assert.False(
            await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth > document.documentElement.clientWidth"),
            $"Horizontal overflow caused by: {string.Join(", ", overflowingElements)}");
    }

    private sealed class BrowserZoneContext
    {
        public string? TimeZone { get; set; }
        public string OffsetLabel { get; set; } = "";
        public int OffsetMinutes { get; set; }
    }
}
