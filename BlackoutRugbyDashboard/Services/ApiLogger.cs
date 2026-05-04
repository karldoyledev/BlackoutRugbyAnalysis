using System.Diagnostics;
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
        public string Type { get; set; } // "request", "response", "error"
        public string Method { get; set; }
        public string Url { get; set; }
        public int? Status { get; set; }
        public string Duration { get; set; }
        public string Body { get; set; }
    }

    private readonly List<ApiLog> _logs = new();
    private readonly int _maxLogs = 100;

    public IReadOnlyList<ApiLog> Logs => _logs.AsReadOnly();

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

    private void AddLog(ApiLog log)
    {
        _logs.Insert(0, log);
        if (_logs.Count > _maxLogs)
        {
            _logs.RemoveAt(_logs.Count - 1);
        }
    }

    public void Clear()
    {
        _logs.Clear();
    }

    public string GetLogsJson()
    {
        return JsonSerializer.Serialize(_logs, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }
}
