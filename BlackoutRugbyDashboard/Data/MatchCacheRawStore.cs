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

    /// <summary>
    /// The newest archived response for one API call, or null when none was archived.
    /// This is the D1 hedge made usable at read time: a parser bug (or a row the parsed
    /// tables never gained) re-parses from the immutable raw XML instead of forcing an
    /// API re-fetch. The Match Analysis team sheet reads its `lu` archive this way.
    /// </summary>
    public string? TryLoadLatest(string endpoint, string key)
    {
        if (!Directory.Exists(_rawDirectory))
        {
            return null;
        }

        var prefix = $"{endpoint}-{SanitizeKey(key)}-";
        var latest = Directory.EnumerateFiles(_rawDirectory, "*.xml")
            .Where(path => Path.GetFileName(path).StartsWith(prefix, StringComparison.Ordinal))
            .OrderByDescending(path => Path.GetFileName(path), StringComparer.Ordinal)
            .FirstOrDefault();
        return latest is null ? null : File.ReadAllText(latest);
    }

    private static string SanitizeKey(string key) =>
        string.Join('_', key.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// Removes every archived response. The Fixtures reset takes the raw archive
    /// with it (D1 §5) — raw XML is the hedge for fixture data, and a clean slate
    /// re-archives on the next append-only fill.
    /// </summary>
    public void Clear()
    {
        if (!Directory.Exists(_rawDirectory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(_rawDirectory))
        {
            File.Delete(file);
        }
    }
}
