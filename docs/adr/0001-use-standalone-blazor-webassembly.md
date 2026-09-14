# Use standalone Blazor WebAssembly

Replace Hugo with a standalone .NET 10 Blazor WebAssembly application deployed as static files to GitHub Pages. Although Blazor WebAssembly has a larger initial download than the current static page, it keeps the existing hosting model while providing a C# runtime for future client-side tools; release linking and GitHub Pages-compatible compression will mitigate the payload cost, and WebAssembly ahead-of-time compilation and PWA support are excluded until a concrete need justifies their complexity.
