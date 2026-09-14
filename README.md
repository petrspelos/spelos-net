# Spelos .NET

This is a repository containing the source code of my personal website [spelos.net](https://spelos.net).

## Current focus

The current implementation uses HUGO static website generator to implement a very simple page with links.

The implementation isn't actually using any of the article functionalities so we are not locked to HUGO.

Next up, we would like to get rid of HUGO altogether and convert the project into Blazor WASM.

Most of the HUGO page is made with custom CSS and HTML so the transition should be relatively painless.

Part of the port should also be the GitHub CI/CD pipeline change in order to properly deploy to GitHub Pages.

The current project already deploys to GitHub pages as seen by the pipeline files in this repository.

Microsoft has an official deployment on GitHub pages guide for WASM: https://learn.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/webassembly/github-pages?view=aspnetcore-10.0

That's basically our target.

Given that our repo is already set up to deploy on GH Pages, we might be able to switch completely without leaving the repo.
