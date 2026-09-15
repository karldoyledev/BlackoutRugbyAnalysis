namespace BlackoutRugbyDashboard.Data;

/// <summary>
/// The immutable raw-XML layer of the Match Cache (D1 §1): one file per API
/// call under Data/MatchCache/raw, named per D1 §2. The batch is transport —
/// files are archived whole per call. A parser bug re-parses from here; it
/// never forces an API re-fetch.
/// </summary>
public class MatchCacheRawStore
{
    private readonly string _rawDirectory;

    public MatchCacheRawStore(string rootDirectory)
    {
        _rawDirectory = Path.Combine(rootDirectory, "raw");
    }

    /// <summary>Archives one API response and returns the file path written.</summary>
    public string Save(string endpoint, string key, string xml)
    {
        Directory.CreateDirectory(_rawDirectory);
        var fileName = $"{endpoint}-{SanitizeKey(key)}-{DateTime.UtcNow:yyyyMMddHHmmssffff}.xml";
        var path = Path.Combine(_rawDirectory, fileName);
        File.WriteAllText(path, xml);
        return path;
    }

    private static string SanitizeKey(string key) =>
        string.Join('_', key.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
}
