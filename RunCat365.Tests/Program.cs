using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using RunCat365;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        checks++;
        Console.WriteLine("PASS " + name);
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static string Event(double percent = 2, int minutes = 10080, string timestamp = "2026-10-26T02:31:54.137Z", string key = "primary", string? limitID = "codex") =>
        JsonSerializer.Serialize(new
        {
            timestamp,
            type = "event_msg",
            payload = new
            {
                type = "token_count",
                rate_limits = new Dictionary<string, object?>
                {
                    ["limit_id"] = limitID,
                    [key] = new { used_percent = percent, window_minutes = minutes, resets_at = 1793583218 },
                    [key == "primary" ? "secondary" : "primary"] = null
                }
            }
        });

    [STAThread]
    private static void Main(string[] args)
    {
        var sample = CodexUsageInfo.Parse(Event())!;
        Check(sample.WeeklyPercent == 2 && sample.ResetsAt?.ToUnixTimeSeconds() == 1793583218, "observed weekly-primary schema");
        Check(CodexUsageInfo.Parse(Event(key: "secondary"))?.WeeklyPercent == 2, "weekly-secondary schema");
        Check(CodexUsageInfo.Parse(Event(minutes: 300)) is null, "five-hour data is not weekly data");
        Check(CodexUsageInfo.Parse(Event(limitID: "codex_other")) is null, "ignore other quota buckets");
        Check(CodexUsageInfo.Parse(Event(limitID: null)) is not null, "legacy null limit ID");
        foreach (var bad in new[] { "{", "null", "[]", "{}", "{\"type\":2}", Event(percent: -1), Event(percent: 101), Event(timestamp: "bad"), Event().Replace("10080", "\"10080\""), Event().Replace("\"token_count\"", "\"user_message\"") })
            Check(CodexUsageInfo.Parse(bad) is null, "malformed or unsupported event rejected");
        Check(CodexUsageInfo.Parse(Event().Replace("1793583218", "9999999999999"))?.ResetsAt is null, "invalid reset is unavailable");
        Check(CodexUsageInfo.Parse(Event().Replace("1793583218", "null"))?.ResetsAt is null, "missing reset is unavailable");
        Check(!sample.IsStale(sample.UpdatedAt.AddMinutes(14)) && sample.IsStale(sample.UpdatedAt.AddMinutes(16)), "staleness uses event time");
        Check(sample.IsExpired(sample.ResetsAt!.Value.AddSeconds(1)) && sample.WeeklyPercent == 2, "expired quota is never estimated as zero");

        var folder = Path.Combine(Path.GetTempPath(), "runcat-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var repository = new CodexUsageRepository(folder);
            repository.Update();
            Check(repository.State.Status == CodexReadStatus.NoSessions, "missing sessions directory");
            Directory.CreateDirectory(Path.Combine(folder, "2026", "10"));
            repository.Update();
            Check(repository.State.Status == CodexReadStatus.NoUsage, "empty sessions directory");
            var path = Path.Combine(folder, "2026", "10", "session.jsonl");
            File.WriteAllText(path, "invalid\n" + Event() + "\n");
            repository.Update();
            Check(repository.State.Usage?.WeeklyPercent == 2, "nested sessions and malformed line recovery");
            var updated = repository.State.Usage!.UpdatedAt;
            repository.Update();
            Check(repository.State.Usage.UpdatedAt == updated, "polling does not fabricate a usage update");
            File.AppendAllText(path, Event(8, timestamp: "2026-10-26T03:00:00Z"));
            repository.Update();
            Check(repository.State.Usage.WeeklyPercent == 2, "unfinished final line deferred");
            File.AppendAllText(path, "\n");
            repository.Update();
            Check(repository.State.Usage?.WeeklyPercent == 8, "appended line is picked up");
            File.AppendAllText(path, Event(99) + "\n");
            repository.Update();
            Check(repository.State.Usage?.WeeklyPercent == 8, "select by event time not line order");
            var other = Path.Combine(folder, "other.jsonl");
            File.WriteAllText(other, Event(10, timestamp: "2026-10-26T04:00:00Z") + "\n");
            File.SetLastWriteTimeUtc(other, DateTime.UtcNow.AddDays(-1));
            repository.Update();
            Check(repository.State.Usage?.WeeklyPercent == 10, "multiple files selected by event time, not modification time");
            File.Delete(other);
            repository.Update();
            File.AppendAllText(path, " ");
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                repository.Update();
                Check(repository.State.Status == CodexReadStatus.ReadError && repository.State.Usage?.WeeklyPercent == 8, "locked file retains last value with error status");
            }
            File.WriteAllText(path, Event(3) + "\n");
            repository.Update();
            Check(repository.State.Usage?.WeeklyPercent == 3, "truncated file is reread");
            File.WriteAllText(path, new string('x', CodexUsageRepository.TailBytes + 100) + "\n" + Event(4) + "\n");
            repository.Update();
            Check(repository.State.Usage?.WeeklyPercent == 4, "bounded tail skips partial first line");
            File.Delete(path);
            repository.Update();
            Check(repository.State.Usage is null, "deleted records removed from cache");
        }
        finally { Directory.Delete(folder, true); }

        ApplicationConfiguration.Initialize();
        foreach (var dpi in new[] { 96, 144 })
        {
            var contentSize = new Size(352 * dpi / 96, 472 * dpi / 96);
            using var shadow = DashboardShadow.Render(contentSize, dpi);
            var maxAlpha = 0;
            for (var y = 0; y < shadow.Height; y++)
            for (var x = 0; x < shadow.Width; x++) maxAlpha = Math.Max(maxAlpha, shadow.GetPixel(x, y).A);
            var margin = DashboardShadow.MarginSize * dpi / 96;
            var center = shadow.Width / 2;
            var bottom = margin + contentSize.Height;
            Check(shadow.GetPixel(center, shadow.Height / 2).A == 0 && shadow.GetPixel(0, 0).A == 0,
                $"shadow leaves content and outside corners transparent at {dpi} DPI");
            Check(maxAlpha is > 0 and <= 40 && shadow.GetPixel(center, bottom + 2 * dpi / 96).A > shadow.GetPixel(center, bottom + 10 * dpi / 96).A,
                $"shadow stays faint and fades outward at {dpi} DPI");
            if (args.Length > 0) shadow.Save(Path.Combine(args[0], $"shadow-{dpi}.png"));
        }
        var history = new SystemUsageHistory();
        var historyStart = DateTimeOffset.UtcNow.AddSeconds(-70);
        for (var index = 0; index <= 70; index++) history.Add(historyStart.AddSeconds(index), index, 90);
        Check(history.Samples.Count == 61 && history.Samples.First().CPU == 10, "history retains at most one minute of samples");
        history.Add(historyStart.AddSeconds(70), 99, 99);
        Check(history.Samples.Last().CPU == 70, "duplicate timestamps do not fabricate history");
        history.Add(historyStart.AddMinutes(5), float.NaN, 101);
        Check(history.Samples.Count == 1 && history.Samples.Single().CPU is null && history.Samples.Single().Memory is null, "resume removes old history and invalid samples remain unavailable");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture;
        using var dashboard = new DashboardForm();
        var graphStart = DateTimeOffset.UtcNow.AddSeconds(-60);
        for (var index = 0; index <= 60; index++) dashboard.RecordSample(graphStart.AddSeconds(index), 12 + index % 9 * 4, 65 + index % 6);
        var sampleCPU = new CPUInfo { Total = 23, Kernel = 8, User = 15, Idle = 77 };
        var memory = new MemoryRepository();
        memory.Update();
        Check(memory.Get().TotalMemory > 0 && memory.Get().MemoryLoad <= 100, "Windows physical RAM measurement");
        var cpu = new CPURepository();
        try { cpu.Update(); Check(!cpu.IsAvailable || cpu.Get().Total is >= 0 and <= 100, "Windows CPU counter smoke check"); }
        finally { cpu.Close(); }
        foreach (var theme in new[] { Theme.Dark, Theme.Light })
        {
            dashboard.UpdateMetrics(sampleCPU, memory.Get(), new(sample with { UpdatedAt = DateTimeOffset.UtcNow }, CodexReadStatus.Ready, DateTimeOffset.UtcNow), theme);
            dashboard.Show();
            Application.DoEvents();
            dashboard.PerformLayout();
            var paintWait = Stopwatch.StartNew();
            while (paintWait.ElapsedMilliseconds < 1000) { Application.DoEvents(); Thread.Sleep(10); }
            dashboard.Show();
            dashboard.Activate();
            dashboard.Refresh();
            Check(dashboard.OwnedForms.OfType<DashboardShadow>().Any(shadow => shadow.Visible && !shadow.ShowInTaskbar && shadow.Bounds.Contains(dashboard.Bounds)),
                $"native shadow follows visible {theme} dashboard");
            var activeShadow = dashboard.OwnedForms.OfType<DashboardShadow>().Single();
            var previousShadowLocation = activeShadow.Location;
            dashboard.Location = new Point(dashboard.Left + 20, dashboard.Top + 20);
            Check(activeShadow.Location == new Point(previousShadowLocation.X + 20, previousShadowLocation.Y + 20), $"shadow follows visible {theme} popup movement");
            using var bitmap = new Bitmap(dashboard.Width, dashboard.Height);
            dashboard.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            if (args.Length > 0) bitmap.Save(Path.Combine(args[0], "dashboard-" + theme + ".png"));
            if (args.Length > 0)
            {
                using var shadow = DashboardShadow.Render(bitmap.Size, dashboard.DeviceDpi);
                using var floating = new Bitmap(shadow.Width, shadow.Height);
                using var graphics = Graphics.FromImage(floating);
                graphics.Clear(Color.FromArgb(222, 226, 228));
                graphics.DrawImageUnscaled(shadow, 0, 0);
                var margin = (int)Math.Ceiling(DashboardShadow.MarginSize * dashboard.DeviceDpi / 96f);
                using var outline = DashboardForm.Rounded(new RectangleF(margin, margin, bitmap.Width, bitmap.Height), 24 * dashboard.DeviceDpi / 96f);
                graphics.SetClip(outline);
                graphics.DrawImageUnscaled(bitmap, margin, margin);
                floating.Save(Path.Combine(args[0], "dashboard-shadow-" + theme + ".png"));
            }
            var headingsVisible = true;
            foreach (var text in new[] { "RunCat", "Codex", "CPU", "Memory" })
            {
                var label = Descendants(dashboard).OfType<Label>().Single(control => control.Text == text);
                var bounds = new Rectangle(dashboard.PointToClient(label.PointToScreen(Point.Empty)), label.Size);
                bounds.Intersect(new Rectangle(Point.Empty, bitmap.Size));
                var textPixels = 0;
                for (var y = bounds.Top; y < bounds.Bottom; y++)
                for (var x = bounds.Left; x < bounds.Right; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    if (pixel.R > 200 && pixel.G > 200 && pixel.B > 200) textPixels++;
                }
                headingsVisible &= textPixels > 10;
            }
            Check(headingsVisible, $"all headings render after switching to {theme}");
        }
        dashboard.UpdateMetrics(null, default, new(null, CodexReadStatus.NoSessions, null), Theme.Dark);
        Application.DoEvents();
        dashboard.Show();
        dashboard.Activate();
        dashboard.Refresh();
        using (var bitmap = new Bitmap(dashboard.Width, dashboard.Height))
        {
            dashboard.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            if (args.Length > 0) bitmap.Save(Path.Combine(args[0], "dashboard-empty.png"));
        }
        dashboard.Scale(new SizeF(1.5f, 1.5f));
        dashboard.UpdateMetrics(sampleCPU, memory.Get(), new(sample with { UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1) }, CodexReadStatus.Ready, DateTimeOffset.UtcNow), Theme.Light);
        Application.DoEvents();
        dashboard.Show();
        dashboard.Activate();
        dashboard.Refresh();
        using (var bitmap = new Bitmap(dashboard.Width, dashboard.Height))
        {
            dashboard.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            if (args.Length > 0) bitmap.Save(Path.Combine(args[0], "dashboard-150.png"));
        }
        dashboard.Hide();
        Check(dashboard.OwnedForms.All(form => !form.Visible), "hiding dashboard also hides its shadow");
        dashboard.Show();
        dashboard.Close();
        Check(!dashboard.Visible && !dashboard.IsDisposed, "closing the borderless popup hides it without exiting");
        dashboard.UpdateMetrics(sampleCPU, memory.Get(), new(sample with { WeeklyPercent = 100 }, CodexReadStatus.Ready, DateTimeOffset.UtcNow), Theme.Dark);
        dashboard.Show();
        Application.DoEvents();
        using (var bitmap = new Bitmap(dashboard.Width, dashboard.Height))
        {
            dashboard.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            Check(bitmap.Width == dashboard.Width, "full usage bar renders after reopening popup");
        }
        dashboard.Hide();
        Check(DashboardForm.TextFor("NoSessions") == "Codex sessions folder not found", "English resources and empty dashboard render");
        var stableState = new CodexUsageState(sample, CodexReadStatus.Ready, DateTimeOffset.UtcNow);
        dashboard.UpdateMetrics(sampleCPU, memory.Get(), stableState, Theme.Dark);
        var wholeWindowInvalidations = 0;
        dashboard.Invalidated += (_, _) => wholeWindowInvalidations++;
        dashboard.UpdateMetrics(sampleCPU, memory.Get(), stableState, Theme.Dark);
        Check(wholeWindowInvalidations == 0, "unchanged metrics do not repaint the whole window");
        using (var menu = new ContextMenuManager(
            () => Runner.Cat, _ => { }, new CustomRunnerRepository(), () => null, _ => { }, _ => { },
            () => Theme.Dark, () => Theme.System, _ => { }, () => SpeedSource.CPU, _ => { }, _ => true,
            () => FPSMaxLimit.FPS40, _ => { }, () => TemperatureUnit.System, _ => { },
            () => false, _ => false, () => { }, () => { }, () => { }))
        {
            var iconField = typeof(ContextMenuManager).GetField("icons", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            foreach (var systemTheme in Enum.GetValues<Theme>())
            foreach (var manualTheme in Enum.GetValues<Theme>())
            {
                menu.SetIcons(systemTheme, manualTheme, Runner.Cat);
                var frames = (List<Icon>)iconField.GetValue(menu)!;
                var allBlack = frames.Count == Runner.Cat.GetFrameNumber();
                var visiblePixels = 0;
                foreach (var frame in frames)
                {
                    using var bitmap = frame.ToBitmap();
                    for (var y = 0; y < bitmap.Height; y++)
                    for (var x = 0; x < bitmap.Width; x++)
                    {
                        var pixel = bitmap.GetPixel(x, y);
                        if (pixel.A == 0) continue;
                        visiblePixels++;
                        allBlack &= pixel.R == 0 && pixel.G == 0 && pixel.B == 0;
                    }
                }
                Check(allBlack && visiblePixels > 0, $"all cat frames stay black: system={systemTheme}, manual={manualTheme}");
            }
        }
        Check(Theme.System.GetLocalizedString() == "System" && Theme.Light.GetLocalizedString() == "Light" && Theme.Dark.GetLocalizedString() == "Dark", "theme menu labels remain distinct");
        var ownedShadow = dashboard.OwnedForms.OfType<DashboardShadow>().Single();
        dashboard.Dispose();
        Check(ownedShadow.IsDisposed, "disposing dashboard releases shadow window");
        if (args.Contains("--local"))
        {
            var watch = Stopwatch.StartNew();
            var local = new CodexUsageRepository();
            local.Update();
            Console.WriteLine($"LOCAL status={local.State.Status}, weekly={local.State.Usage?.WeeklyPercent}, updated={local.State.Usage?.UpdatedAt:O}, scanMs={watch.ElapsedMilliseconds}");
            watch.Restart();
            local.Update();
            Console.WriteLine($"LOCAL cachedScanMs={watch.ElapsedMilliseconds}");
        }
        Console.WriteLine($"{checks} checks passed.");
    }
}
