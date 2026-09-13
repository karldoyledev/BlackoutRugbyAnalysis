using BlackoutRugbyDashboard.Services;

namespace BlackoutRugbyDashboard.Tests;

/// <summary>
/// Loads adapter regression fixtures (representative Blackout Rugby response shapes
/// with no credentials or live member data) and builds the adapter under test.
/// </summary>
public static class TestXml
{
    public static BlackoutRugbyResponseAdapter CreateAdapter() => new();

    public static string Load(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
}
