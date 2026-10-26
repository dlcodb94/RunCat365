using System.Drawing.Drawing2D;
using RunCat365.Properties;

namespace RunCat365;

internal sealed class DashboardForm : Form
{
    private DashboardShadow? shadow;
    private readonly SystemUsageHistory history = new();
    private readonly MetricCard cpuCard = new();
    private readonly MetricCard memoryCard = new();
    private readonly MetricCard ai = new();
    private readonly MetricCard headerCard = new();
    private readonly Label cpuTitle = Heading();
    private readonly Label cpuValue = Heading();
    private readonly Label cpuDetails = Detail();
    private readonly Label memoryTitle = Heading();
    private readonly Label memoryValue = Heading();
    private readonly Label memoryDetails = Detail();
    private readonly Label weekly = Heading();
    private readonly Label reset = Detail();
    private readonly Label updated = Detail();
    private readonly Label note = Detail();
    private readonly Label title = new() { Text = "RunCat", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty, UseCompatibleTextRendering = true };
    private readonly Button menu = new() { Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Margin = Padding.Empty };
    private readonly MiniGraph cpuGraph;
    private readonly MiniGraph memoryGraph;
    private readonly MiniGraph usageBar = new(null, false);
    private readonly Font bodyFont = new("Segoe UI", 8.5f);
    private readonly Font headingFont = new("Segoe UI Semibold", 10);
    private readonly Font valueFont = new("Segoe UI", 20);
    private readonly Font brandFont = new("Segoe UI", 14);
    private readonly ToolTip toolTip = new();
    private readonly List<MetricIcon> icons = [];
    private Color muted;
    private (Theme Theme, bool HighContrast)? appliedPalette;
    internal event Action<Point>? MenuRequested;

    internal static string TextFor(string key) => Strings.ResourceManager.GetString("Dashboard_" + key) ?? key;
    private static Label Heading() => new() { Dock = DockStyle.Fill, Margin = Padding.Empty, TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent, UseCompatibleTextRendering = true };
    private static Label Detail() => new() { Dock = DockStyle.Fill, Margin = Padding.Empty, TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent, AutoEllipsis = true, UseCompatibleTextRendering = true };

    internal DashboardForm()
    {
        cpuGraph = new(history, true);
        memoryGraph = new(history, false);
        Text = "RunCat AI Monitor";
        Icon = Resources.AppIcon;
        Font = bodyFont;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        ClientSize = new Size(352, 472);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        KeyPreview = true;
        DoubleBuffered = true;

        var root = new BufferedLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12, 20, 12, 12), ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent };
        root.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var unit = DeviceDpi / 96f;
            using var outer = Rounded(new RectangleF(.5f, .5f, root.Width - 1, root.Height - 1), 24 * unit);
            using var edge = new Pen(Color.FromArgb(40, ForeColor));
            e.Graphics.DrawPath(edge, outer);
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var height in new[] { 56, 188, 196 }) root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        var header = new BufferedLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty, BackColor = Color.Transparent };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28));
        title.Font = brandFont;
        menu.FlatAppearance.BorderSize = 0;
        menu.AccessibleName = TextFor("Menu");
        menu.Paint += (_, e) =>
        {
            var unit = menu.DeviceDpi / 96f;
            using var dot = new SolidBrush(menu.ForeColor);
            foreach (var offset in new[] { -6, 0, 6 })
            {
                e.Graphics.FillEllipse(dot, menu.Width / 2f + offset * unit - unit, menu.Height / 2f - unit, 2 * unit, 2 * unit);
            }
        };
        menu.Click += (_, _) => MenuRequested?.Invoke(menu.PointToScreen(new Point(menu.Width, menu.Height)));
        toolTip.SetToolTip(menu, TextFor("Menu"));
        header.Controls.Add(title, 0, 0);
        header.Controls.Add(menu, 1, 0);
        headerCard.Padding = new Padding(18, 4, 12, 4);
        headerCard.Controls.Add(header);

        var systems = new BufferedLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, BackColor = Color.Transparent };
        systems.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        systems.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        systems.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        cpuCard.Margin = new Padding(0, 0, 4, 0);
        memoryCard.Margin = new Padding(4, 0, 0, 0);
        cpuTitle.Text = "CPU";
        memoryTitle.Text = Strings.SystemInfo_Memory;
        cpuCard.Controls.Add(MetricRow(0, cpuTitle, cpuValue, cpuDetails, cpuGraph));
        memoryCard.Controls.Add(MetricRow(1, memoryTitle, memoryValue, memoryDetails, memoryGraph));
        systems.Controls.Add(cpuCard, 0, 0);
        systems.Controls.Add(memoryCard, 1, 0);

        var aiText = new BufferedLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Margin = Padding.Empty, BackColor = Color.Transparent };
        aiText.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var height in new[] { 25, 42, 26, 20, 20, 20 }) aiText.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        var aiHeader = new BufferedLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = Padding.Empty, BackColor = Color.Transparent };
        aiHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
        aiHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        aiHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        var aiTitle = Heading();
        aiTitle.Text = "Codex";
        aiTitle.Font = headingFont;
        var period = Detail();
        period.Text = TextFor("WeeklyPeriod");
        period.TextAlign = ContentAlignment.MiddleRight;
        aiHeader.Controls.Add(CreateIcon(2), 0, 0);
        aiHeader.Controls.Add(aiTitle, 1, 0);
        aiHeader.Controls.Add(period, 2, 0);
        weekly.Font = valueFont;
        aiText.Controls.Add(aiHeader, 0, 0);
        aiText.Controls.Add(weekly, 0, 1);
        aiText.Controls.Add(usageBar, 0, 2);
        aiText.Controls.Add(reset, 0, 3);
        aiText.Controls.Add(updated, 0, 4);
        aiText.Controls.Add(note, 0, 5);
        ai.Controls.Add(aiText);

        root.Controls.Add(headerCard, 0, 0);
        root.Controls.Add(ai, 0, 1);
        root.Controls.Add(systems, 0, 2);
        Controls.Add(root);
        Deactivate += (_, _) => Hide();
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Hide(); };
        FormClosing += (_, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
    }

    private Control MetricRow(int icon, Label heading, Label value, Label details, MiniGraph graph)
    {
        heading.Font = headingFont;
        value.Font = valueFont;
        var text = new BufferedLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty, BackColor = Color.Transparent };
        text.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        text.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        text.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        text.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        text.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var header = new BufferedLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty, BackColor = Color.Transparent };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 22));
        header.Controls.Add(heading, 0, 0);
        header.Controls.Add(CreateIcon(icon), 1, 0);
        text.Controls.Add(header, 0, 0);
        text.Controls.Add(value, 0, 1);
        text.Controls.Add(details, 0, 2);
        text.Controls.Add(graph, 0, 3);
        toolTip.SetToolTip(graph, TextFor("History"));
        return text;
    }

    private MetricIcon CreateIcon(int kind)
    {
        var icon = new MetricIcon(kind) { Dock = DockStyle.Fill, Margin = new Padding(0, 2, 4, 2) };
        icons.Add(icon);
        return icon;
    }

    internal void RecordSample(DateTimeOffset time, float? cpu, float? memory)
    {
        history.Add(time, cpu, memory);
        if (Visible) { cpuGraph.Invalidate(); memoryGraph.Invalidate(); }
    }

    internal void ShowNearCursor()
    {
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(Math.Clamp(Cursor.Position.X - Width, area.Left, Math.Max(area.Left, area.Right - Width)),
            Math.Clamp(Cursor.Position.Y - Height - 8, area.Top, Math.Max(area.Top, area.Bottom - Height)));
        Show();
        Activate();
    }

    private void ApplyTheme(Theme theme)
    {
        var contrast = SystemInformation.HighContrast;
        if (appliedPalette == (theme, contrast)) return;
        appliedPalette = (theme, contrast);
        var dark = theme == Theme.Dark;
        BackColor = contrast ? SystemColors.Window : dark ? Color.FromArgb(29, 47, 49) : Color.FromArgb(242, 242, 237);
        ForeColor = contrast ? SystemColors.WindowText : Color.FromArgb(242, 242, 237);
        muted = contrast ? ForeColor : Color.FromArgb(177, 194, 194);
        var accent = contrast ? SystemColors.Highlight : Color.FromArgb(202, 198, 255);
        foreach (var card in new[] { headerCard, cpuCard, memoryCard, ai })
        {
            card.BackColor = contrast ? BackColor : Color.FromArgb(0, 29, 33);
            card.ForeColor = ForeColor;
            foreach (Control content in card.Controls) SetContentBackground(content, card.BackColor);
            card.Invalidate();
        }
        title.ForeColor = ForeColor;
        menu.ForeColor = ForeColor;
        menu.FlatAppearance.MouseOverBackColor = contrast ? SystemColors.Highlight : Color.FromArgb(53, 65, 82);
        cpuDetails.ForeColor = memoryDetails.ForeColor = muted;
        cpuValue.ForeColor = memoryValue.ForeColor = weekly.ForeColor = contrast ? ForeColor : accent;
        foreach (var icon in icons) { icon.ForeColor = ForeColor; icon.Invalidate(); }
        foreach (var graph in new[] { cpuGraph, memoryGraph, usageBar })
        {
            graph.ForeColor = accent;
            graph.TrackColor = contrast ? SystemColors.GrayText : Color.FromArgb(45, 65, 72);
            graph.Invalidate();
        }
        Invalidate(true);
        UpdateShadow();
    }

    private static void SetContentBackground(Control control, Color color)
    {
        control.BackColor = color;
        foreach (Control child in control.Controls) SetContentBackground(child, color);
        control.Invalidate();
    }

    internal void UpdateMetrics(CPUInfo? cpu, MemoryInfo memory, CodexUsageState state, Theme theme)
    {
        ApplyTheme(theme);
        cpuValue.Text = cpu is { } value ? value.Total.ToString("0.0") + "%" : "N/A";
        cpuDetails.Text = cpu is { } info ? $"{Strings.SystemInfo_Kernel}: {info.Kernel:0.0}%\n{Strings.SystemInfo_User}: {info.User:0.0}%\n{TextFor("Idle")}: {info.Idle:0.0}%" : "N/A";
        memoryValue.Text = memory.TotalMemory > 0 ? memory.MemoryLoad + "%" : "N/A";
        memoryDetails.Text = memory.TotalMemory > 0 ? $"{Strings.SystemInfo_Total}: {memory.TotalMemory.ToByteFormatted()}\n{Strings.SystemInfo_Used}: {memory.UsedMemory.ToByteFormatted()}\n{Strings.SystemInfo_Available}: {memory.AvailableMemory.ToByteFormatted()}" : "N/A";
        var usage = state.Usage;
        weekly.Text = usage?.WeeklyPercent is { } percent ? percent.ToString("0.#") + "%" : "N/A";
        var usagePercent = usage?.WeeklyPercent ?? 0;
        if (usageBar.Percent != usagePercent)
        {
            usageBar.Percent = usagePercent;
            usageBar.Invalidate();
        }
        usageBar.AccessibleName = $"{TextFor("WeeklyPeriod")}: {weekly.Text}";
        reset.Text = $"{TextFor("Reset")}: {(usage?.ResetsAt is { } resetTime ? resetTime.ToLocalTime().ToString("MM/dd HH:mm") : "N/A")}";
        updated.Text = $"{TextFor("Updated")}: {(usage is null ? "N/A" : usage.UpdatedAt.ToLocalTime().ToString("MM/dd HH:mm"))}";
        toolTip.SetToolTip(reset, usage?.ResetsAt?.ToLocalTime().ToString("F") ?? "N/A");
        toolTip.SetToolTip(updated, usage?.UpdatedAt.ToLocalTime().ToString("F") ?? "N/A");
        note.Text = state.Status == CodexReadStatus.ReadError ? TextFor("ReadError") : usage is null ? TextFor(state.Status.ToString()) :
            usage.IsExpired(DateTimeOffset.UtcNow) ? TextFor("Expired") : usage.IsStale(DateTimeOffset.UtcNow) ? TextFor("Stale") : "";
        toolTip.SetToolTip(note, note.Text);
        note.ForeColor = updated.ForeColor = muted;
    }

    private void UpdateShadow()
    {
        if (!Visible || Disposing || IsDisposed || SystemInformation.HighContrast)
        {
            shadow?.Hide();
            return;
        }
        shadow ??= new DashboardShadow();
        if (shadow.Follow(this))
        {
            if (!shadow.Visible) shadow.Show(this);
        }
        else shadow.Hide();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        UpdateShadow();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        UpdateShadow();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (Width <= 0 || Height <= 0) return;
        using var shape = Rounded(new RectangleF(0, 0, Width, Height), 24 * DeviceDpi / 96f);
        var oldRegion = Region;
        Region = new Region(shape);
        oldRegion?.Dispose();
        UpdateShadow();
    }

    internal static GraphicsPath Rounded(RectangleF bounds, float radius)
    {
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { shadow?.Dispose(); toolTip.Dispose(); }
        base.Dispose(disposing);
        if (disposing) { bodyFont.Dispose(); headingFont.Dispose(); valueFont.Dispose(); brandFont.Dispose(); }
    }

    private sealed class BufferedLayoutPanel : TableLayoutPanel
    {
        internal BufferedLayoutPanel() { DoubleBuffered = true; }
    }

    private sealed class MetricCard : Panel
    {
        internal MetricCard() { Dock = DockStyle.Fill; Margin = new Padding(0, 0, 0, 8); Padding = new Padding(16, 12, 16, 12); DoubleBuffered = true; }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var state = e.Graphics.Save();
            e.Graphics.SetClip(e.ClipRectangle, CombineMode.Intersect);
            using var background = new SolidBrush(FindForm()?.BackColor ?? BackColor);
            e.Graphics.FillRectangle(background, ClientRectangle);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var shape = Rounded(new RectangleF(.5f, .5f, Width - 1, Height - 1), 24 * DeviceDpi / 96f);
            using var fill = new SolidBrush(BackColor);
            e.Graphics.FillPath(fill, shape);
            if (SystemInformation.HighContrast)
            {
                using var outline = new Pen(ForeColor);
                e.Graphics.DrawPath(outline, shape);
            }
            e.Graphics.Restore(state);
        }
    }

    private sealed class MiniGraph : Control
    {
        private readonly SystemUsageHistory? history;
        private readonly bool cpu;
        internal double Percent;
        internal Color TrackColor;
        protected override Size DefaultSize => new(120, 16);
        internal MiniGraph(SystemUsageHistory? history, bool cpu)
        {
            this.history = history;
            this.cpu = cpu;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Margin = history is null ? new Padding(0, 3, 0, 5) : new Padding(0, 4, 0, 0);
            Dock = DockStyle.Fill;
            TabStop = false;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 0 || Height <= 0) return;
            using var brush = new SolidBrush(ForeColor);
            if (history is null)
            {
                using var track = new SolidBrush(TrackColor);
                var gap = 2 * DeviceDpi / 96f;
                var step = Width / 30f;
                var fillWidth = (float)(Width * Math.Clamp(Percent, 0, 100) / 100);
                for (var i = 0; i < 30; i++)
                {
                    var x = i * step;
                    var width = Math.Max(1, step - gap);
                    e.Graphics.FillRectangle(track, x, 0, width, Height);
                    if (fillWidth > x) e.Graphics.FillRectangle(brush, x, 0, Math.Min(width, fillWidth - x), Height);
                }
                return;
            }
            var now = DateTimeOffset.UtcNow;
            var values = history.Samples.Where(sample => sample.Time >= now.AddSeconds(-60) && sample.Time <= now).ToArray();
            for (var i = 1; i < values.Length; i++)
            {
                var previous = values[i - 1];
                var current = values[i];
                var first = cpu ? previous.CPU : previous.Memory;
                var second = cpu ? current.CPU : current.Memory;
                if (first is null || second is null || (current.Time - previous.Time).TotalSeconds > 2.5) continue;
                var x1 = (float)(Width * (1 - (now - previous.Time).TotalSeconds / 60));
                var x2 = (float)(Width * (1 - (now - current.Time).TotalSeconds / 60));
                var y1 = Height * (1 - first.Value / 100);
                var y2 = Height * (1 - second.Value / 100);
                e.Graphics.FillPolygon(brush, new PointF[] { new(x1, Height), new(x1, y1), new(x2, y2), new(x2, Height) });
            }
        }
    }

    private sealed class MetricIcon : Control
    {
        private readonly int kind;
        private readonly Bitmap? codexIcon;
        internal MetricIcon(int kind)
        {
            this.kind = kind;
            if (kind == 2)
            {
                using var stream = typeof(DashboardForm).Assembly.GetManifestResourceStream("RunCat365.CodexIcon")!;
                using var image = new Bitmap(stream);
                codexIcon = new Bitmap(image);
                AccessibleName = "Codex";
            }
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            TabStop = false;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var state = e.Graphics.Save();
            var size = Math.Min(Width, Height);
            e.Graphics.TranslateTransform((Width - size) / 2f, (Height - size) / 2f);
            e.Graphics.ScaleTransform(size / 28f, size / 28f);
            using var pen = new Pen(ForeColor, 1.7f) { LineJoin = LineJoin.Round };
            if (codexIcon is not null)
            {
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                if (ForeColor.GetBrightness() < .5f)
                {
                    using var plate = Rounded(new RectangleF(0, 0, 28, 28), 5);
                    using var background = new SolidBrush(Color.FromArgb(37, 55, 71));
                    e.Graphics.FillPath(background, plate);
                }
                e.Graphics.DrawImage(codexIcon, new RectangleF(1, 1, 26, 26));
            }
            else
            {
                e.Graphics.DrawRectangle(pen, 5, 5, 18, 18);
                if (kind == 0) e.Graphics.DrawRectangle(pen, 9, 9, 10, 10);
                for (var offset = 8; offset <= 20; offset += 4)
                {
                    e.Graphics.DrawLine(pen, offset, 2, offset, 5);
                    e.Graphics.DrawLine(pen, offset, 23, offset, 26);
                    if (kind == 0)
                    {
                        e.Graphics.DrawLine(pen, 2, offset, 5, offset);
                        e.Graphics.DrawLine(pen, 23, offset, 26, offset);
                    }
                }
            }
            e.Graphics.Restore(state);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) codexIcon?.Dispose();
            base.Dispose(disposing);
        }
    }
}
