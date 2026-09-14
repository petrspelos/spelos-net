# Spelos .NET

The source for [spelos.net](https://spelos.net), Peter Spelos' personal website and collection of client-side C# tools.

The site is a standalone .NET 10 Blazor WebAssembly application deployed to GitHub Pages. Its home page retains the original code-inspired business-card design, while `/tools` is the home for future interactive tools.

## Run locally

```powershell
dotnet run --project src/Spelos.Net.Web
```

Use the URL printed by the development server. The project requires the .NET SDK version selected in `global.json`.

## Edit home-page links

Edit `src/Spelos.Net.Web/wwwroot/data/links.json`. Entries appear in array order and contain:

- `name`: visible link text
- `destination`: an absolute HTTPS URL for external links or a root-relative path for internal links
- `iconPath`: a path beneath `wwwroot`
- `kind`: `external` or `internal`

The test suite rejects missing fields, unsafe destinations, duplicate names or destinations, and missing icons.

## Test

```powershell
dotnet test tests/Spelos.Net.Web.Tests
dotnet build tests/Spelos.Net.Web.BrowserTests -c Release
pwsh tests/Spelos.Net.Web.BrowserTests/bin/Release/net10.0/playwright.ps1 install chromium
dotnet run --project src/Spelos.Net.Web -c Release --no-build --urls http://127.0.0.1:5080
```

With the site running, execute the browser tests in another terminal:

```powershell
$env:SPELOS_BASE_URL = 'http://127.0.0.1:5080'
dotnet test tests/Spelos.Net.Web.BrowserTests -c Release --no-build
```

GitHub Actions runs all build, content, browser, and accessibility checks for branches and pull requests. Only `master` publishes to GitHub Pages.
