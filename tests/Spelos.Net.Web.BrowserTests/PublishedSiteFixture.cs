using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

namespace Spelos.Net.Web.BrowserTests;

public sealed class PublishedSiteFixture : IAsyncLifetime
{
    private WebApplication? _application;

    public string BaseUrl { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var webRoot = FindRepositoryRoot().GetDirectories("publish", SearchOption.AllDirectories)
            .Single(directory => directory.FullName.EndsWith(
                Path.Combine("Spelos.Net.Web", "bin", "Release", "net10.0", "publish"),
                StringComparison.OrdinalIgnoreCase))
            .GetDirectories("wwwroot").Single();

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        _application = builder.Build();
        var fileProvider = new PhysicalFileProvider(webRoot.FullName);
        _application.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
        _application.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = fileProvider,
            ServeUnknownFileTypes = true,
            DefaultContentType = "application/octet-stream",
        });
        _application.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync(Path.Combine(webRoot.FullName, "404.html"));
        });

        await _application.StartAsync();
        var address = _application.Urls.Single();
        var port = new Uri(address).Port;
        BaseUrl = $"http://spelos.localhost:{port}";
    }

    public async Task DisposeAsync()
    {
        if (_application is not null) await _application.DisposeAsync();
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Spelos.Net.slnx"))) return directory;
        throw new DirectoryNotFoundException("Could not find the repository root.");
    }
}
