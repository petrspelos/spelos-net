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
dotnet publish src/Spelos.Net.Web -c Release
pwsh tests/Spelos.Net.Web.BrowserTests/bin/Release/net10.0/playwright.ps1 install chromium
dotnet test tests/Spelos.Net.Web.BrowserTests -c Release --no-build
```

The conversion consumer tests require Docker Compose v2+ (`docker compose`) and PowerShell 7.5+ (`pwsh`). They only parse Compose configuration (no Docker daemon needed), and intercept persistent environment writes in an isolated PowerShell process. Set `SPELOS_COMPOSE_PATH` to use a standalone Compose executable instead of `docker compose`.

GitHub Actions runs all build, content, browser, and accessibility checks for branches and pull requests. Only `master` publishes to GitHub Pages.

## Appsettings configuration converter

Visit `/tools/appsettings-converter` to paste or upload appsettings JSON, enter an optional exact provider prefix, and explicitly convert to Docker Compose `.env` or a Windows PowerShell 7.5+ `.ps1`. Copy and Download become available after successful conversion and clear when inputs change. Processing stays in browser memory without configuration storage or uploads.

The output targets .NET 8+ applications: nested paths use `__` and array indices start at zero. Add the environment provider after JSON and bind the desired section with `Configure<TOptions>(configuration.GetSection("Options"))`. An exact prefix such as `APP_` is stripped by `AddEnvironmentVariables(prefix: "APP_")`; it is not an options section. Environment overrides merge individual keys, so a shorter array does not remove underlying entries.

For Compose, attach the downloaded file to your service with `env_file: ./appsettings.env`. A project `.env` primarily provides interpolation and is not automatically passed to the application; .NET itself does not read `.env`. This export targets Compose syntax rather than universal dotenv compatibility.

On Windows, execute the `.ps1` in PowerShell 7.5+ or paste it into your session. It overwrites matching User variables and updates its own Process scope, leaving unrelated variables untouched. A child shell cannot update its parent's environment, and existing applications do not automatically refresh. Empty strings are rejected for this format because persistent User assignments would delete the variable; Compose preserves them. Strings containing carriage returns use UTF-8 Base64 expressions to avoid PowerShell's source-line normalization.

## JSON Toolkit

Visit `/tools/json-toolkit` to format JSON in the browser. Paste JSON into the input and the read-only output updates shortly after you stop typing. Choose Indented (spaces or tabs, 1–8 spaces, default 2) or Minified output; only these settings are saved in local storage, and the JSON itself is never uploaded or stored.

Formatting changes whitespace only: key order, duplicate keys, number literals and string escapes are copied exactly as written. Comments, trailing commas and a leading byte order mark are accepted and removed; any other non-strict syntax, such as single quotes or unquoted property names, produces a line-and-column error with a "go to" action instead of partial output.
