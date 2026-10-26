using System.Text.Json;

namespace RunCat365;

internal sealed record CodexUsageInfo(DateTimeOffset UpdatedAt, double? WeeklyPercent, DateTimeOffset? ResetsAt)
{
    internal bool IsStale(DateTimeOffset now) => now - UpdatedAt > TimeSpan.FromMinutes(15);
    internal bool IsExpired(DateTimeOffset now) => ResetsAt is { } reset && reset <= now;

    internal static CodexUsageInfo? Parse(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String || type.GetString() != "event_msg" ||
                !root.TryGetProperty("timestamp", out var timestamp) || timestamp.ValueKind != JsonValueKind.String ||
                !timestamp.TryGetDateTimeOffset(out var updatedAt) ||
                !root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object ||
                !payload.TryGetProperty("type", out var payloadType) || payloadType.ValueKind != JsonValueKind.String || payloadType.GetString() != "token_count" ||
                !payload.TryGetProperty("rate_limits", out var limits) || limits.ValueKind != JsonValueKind.Object)
                return null;

            if (limits.TryGetProperty("limit_id", out var limitID) && limitID.ValueKind != JsonValueKind.Null &&
                (limitID.ValueKind != JsonValueKind.String || limitID.GetString() != "codex"))
                return null;

            foreach (var key in new[] { "primary", "secondary" })
            {
                if (!limits.TryGetProperty(key, out var window) || window.ValueKind != JsonValueKind.Object ||
                    !window.TryGetProperty("window_minutes", out var minutes) || minutes.ValueKind != JsonValueKind.Number ||
                    !minutes.TryGetInt32(out var duration) || duration != 10080 ||
                    !window.TryGetProperty("used_percent", out var used) || used.ValueKind != JsonValueKind.Number ||
                    !used.TryGetDouble(out var percent) || !double.IsFinite(percent) || percent < 0 || percent > 100)
                    continue;

                DateTimeOffset? resetsAt = null;
                if (window.TryGetProperty("resets_at", out var reset) && reset.ValueKind == JsonValueKind.Number &&
                    reset.TryGetInt64(out var seconds) && seconds >= -62135596800 && seconds <= 253402300799)
                    resetsAt = DateTimeOffset.FromUnixTimeSeconds(seconds);
                return new CodexUsageInfo(updatedAt, percent, resetsAt);
            }
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
