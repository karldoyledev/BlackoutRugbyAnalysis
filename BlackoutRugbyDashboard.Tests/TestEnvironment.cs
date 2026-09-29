using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace BlackoutRugbyDashboard.Tests;

/// <summary>
/// A host environment rooted at a temp folder, for the stores that write to disk
/// (SnapshotStore's snapshot folder). Shared by the store and page-read tests.
/// </summary>
public sealed class TestEnvironment(string contentRoot) : IWebHostEnvironment
{
    public string WebRootPath { get; set; } = string.Empty;

    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

    public string ApplicationName { get; set; } = "tests";

    public string EnvironmentName { get; set; } = "Development";

    public string ContentRootPath { get; set; } = contentRoot;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
