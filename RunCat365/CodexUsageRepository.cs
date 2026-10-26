using System.Text;

namespace RunCat365;

internal enum CodexReadStatus { Ready, NoSessions, NoUsage, ReadError }

internal sealed record CodexUsageState(CodexUsageInfo? Usage, CodexReadStatus Status, DateTimeOffset? CheckedAt);

internal sealed class CodexUsageRepository
{
    internal const int TailBytes = 256 * 1024;
    private readonly string sessionsDirectory;
    private readonly Dictionary<string, (long Length, DateTime Modified, CodexUsageInfo? Usage)> cache = new(StringComparer.OrdinalIgnoreCase);
    internal CodexUsageState State { get; private set; } = new(null, CodexReadStatus.NoUsage, null);

    internal CodexUsageRepository(string? sessionsDirectory = null)
    {
        var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        this.sessionsDirectory = sessionsDirectory ?? Path.Combine(
            string.IsNullOrWhiteSpace(codexHome) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex") : codexHome,
            "sessions");
    }

    internal void Update()
    {
        var now = DateTimeOffset.UtcNow;
        var failed = false;
        try
        {
            var files = new DirectoryInfo(sessionsDirectory).GetFiles("*.jsonl", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            });
            var existing = files.Select(file => file.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var path in cache.Keys.Where(path => !existing.Contains(path)).ToArray()) cache.Remove(path);
            foreach (var file in files)
            {
                if (cache.TryGetValue(file.FullName, out var previous) && previous.Length == file.Length && previous.Modified == file.LastWriteTimeUtc)
                    continue;
                try
                {
                    var usage = ReadTail(file.FullName);
                    if (file.Length >= previous.Length && previous.Usage is { } old && (usage is null || old.UpdatedAt > usage.UpdatedAt))
                        usage = old;
                    cache[file.FullName] = (file.Length, file.LastWriteTimeUtc, usage);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    failed = true;
                }
            }
            var latest = cache.Values.Select(value => value.Usage).Where(usage => usage is not null)
                .OrderByDescending(usage => usage!.UpdatedAt).FirstOrDefault();
            State = new(latest, failed ? CodexReadStatus.ReadError : latest is null ? CodexReadStatus.NoUsage : CodexReadStatus.Ready, now);
        }
        catch (DirectoryNotFoundException)
        {
            cache.Clear();
            State = new(null, CodexReadStatus.NoSessions, now);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            State = new(State.Usage, CodexReadStatus.ReadError, now);
        }
    }

    private static CodexUsageInfo? ReadTail(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var length = stream.Length;
        var start = Math.Max(0, length - TailBytes);
        stream.Position = start;
        var buffer = new byte[(int)(length - start)];
        var count = 0;
        while (count < buffer.Length)
        {
            var read = stream.Read(buffer, count, buffer.Length - count);
            if (read == 0) break;
            count += read;
        }
        // shortcut: bounded tails can miss older events on first launch; add backwards paging if needed.
        var lineStart = start == 0 ? 0 : Array.IndexOf(buffer, (byte)'\n', 0, count) + 1;
        if (start > 0 && lineStart == 0) return null;
        CodexUsageInfo? latest = null;
        for (var index = lineStart; index < count; index++)
        {
            if (buffer[index] != '\n') continue;
            var line = Encoding.UTF8.GetString(buffer, lineStart, index - lineStart).TrimStart('\uFEFF');
            lineStart = index + 1;
            if (!line.Contains("rate_limits", StringComparison.Ordinal)) continue;
            var usage = CodexUsageInfo.Parse(line);
            if (usage is not null && (latest is null || usage.UpdatedAt >= latest.UpdatedAt)) latest = usage;
        }
        return latest;
    }
}
