namespace RunCat365;

internal sealed record SystemUsageSample(DateTimeOffset Time, float? CPU, float? Memory);

internal sealed class SystemUsageHistory
{
    private readonly Queue<SystemUsageSample> samples = new();
    internal IReadOnlyCollection<SystemUsageSample> Samples => samples;

    internal void Add(DateTimeOffset time, float? cpu, float? memory)
    {
        if (samples.Count > 0 && time <= samples.Last().Time) return;
        samples.Enqueue(new(time, Normalize(cpu), Normalize(memory)));
        while (samples.Count > 61 || samples.Peek().Time < time.AddSeconds(-60)) samples.Dequeue();
    }

    private static float? Normalize(float? value) => value is { } number && float.IsFinite(number) && number is >= 0 and <= 100 ? number : null;
}
