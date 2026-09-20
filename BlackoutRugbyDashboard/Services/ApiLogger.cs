using System.Text.Json;

namespace BlackoutRugbyDashboard.Services;

/// <summary>
/// Tracks and logs API requests and responses for debugging purposes
/// </summary>
public class ApiLogger
{
    public class ApiLog
    {
        public string Timestamp { get; set; }
        public string Type { get; set; } // "request", "response", "error", "cache"
        public string Method { get; set; }
        public string Url { get; set; }
        public int? Status { get; set; }
        public string Duration { get; set; }
        public string Body { get; set; }
    }

    private readonly List<ApiLog> _logs = new();
    private readonly int _maxLogs = 100;
    // Cache-first phases log concurrently (throttled parallel fixture fills and
    // season reads); every access to the shared list happens under this lock.
    private readonly object _gate = new();

    public IReadOnlyList<ApiLog> Logs
    {
        get { lock (_gate) { return _logs.AsReadOnly(); } }
    }

    public void LogRequest(string method, string url, string? body = null)
    {
        var log = new ApiLog
        {
            Timestamp = DateTime.Now.ToString("HH:mm:ss.fff"),
            Type = "request",
            Method = method,
            Url = url,
            Body = body
        };

        AddLog(log);
    }

    public void LogResponse(string url, int status, string? body = null, long durationMs = 0)
    {
        var log = new ApiLog
        {
            Timestamp = DateTime.Now.ToString("HH:mm:ss.fff"),
            Type = "response",
            Method = "Response",
            Url = url,
            Status = status,
            Duration = $"{durationMs}ms",
            Body = body
        };

        AddLog(log);
    }

    public void LogError(string url, string error, long durationMs = 0)
    {
        var log = new ApiLog
        {
            Timestamp = DateTime.Now.ToString("HH:mm:ss.fff"),
            Type = "error",
            Method = "Error",
            Url = url,
            Duration = $"{durationMs}ms",
            Body = error
        };

        AddLog(log);
    }

    /// <summary>
    /// Records a cache-served read so the debug panel distinguishes Match Cache
    /// hits from live API calls (cache-first surfacing, D-Squad).
    /// </summary>
    public void LogCache(string url, string body)
    {
        AddLog(new ApiLog
        {
            Timestamp = DateTime.Now.ToString("HH:mm:ss.fff"),
            Type = "cache",
            Method = "Cache",
            Url = url,
            Body = body
        });
    }

    private void AddLog(ApiLog log)
    {
        lock (_gate)
        {
            _logs.Insert(0, log);
            if (_logs.Count > _maxLogs)
            {
                _logs.RemoveAt(_logs.Count - 1);
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _logs.Clear();
        }
    }

    public string GetLogsJson()
    {
        lock (_gate)
        {
            return JsonSerializer.Serialize(_logs, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }
    }
}
