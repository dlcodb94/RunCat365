using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace RunCat365;

internal sealed class DashboardShadow : Form
{
    internal const int MarginSize = 16;
    private const int LayeredStyle = 0x00080000;
    private const int ClickThroughStyle = 0x00000020;
    private const int NoActivateStyle = 0x08000000;
    private const int ToolWindowStyle = 0x00000080;
    private int renderedDpi;

    internal DashboardShadow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        TopMost = true;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= LayeredStyle | ClickThroughStyle | NoActivateStyle | ToolWindowStyle;
            return parameters;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        renderedDpi = 0;
    }

    internal bool Follow(Form owner)
    {
        var margin = (int)Math.Ceiling(MarginSize * owner.DeviceDpi / 96f);
        var location = new Point(owner.Left - margin, owner.Top - margin);
        var size = new Size(owner.Width + margin * 2, owner.Height + margin * 2);
        if (renderedDpi == owner.DeviceDpi && Size == size)
        {
            Location = location;
            return true;
        }

        using var bitmap = Render(owner.Size, owner.DeviceDpi);
        var source = Point.Empty;
        var blend = new BlendFunction { SourceConstantAlpha = 255, AlphaFormat = 1 };
        var memoryDC = CreateCompatibleDC(IntPtr.Zero);
        if (memoryDC == IntPtr.Zero) return false;
        var bitmapHandle = IntPtr.Zero;
        var previous = IntPtr.Zero;
        try
        {
            bitmapHandle = bitmap.GetHbitmap(Color.FromArgb(0));
            previous = SelectObject(memoryDC, bitmapHandle);
            if (previous == IntPtr.Zero || previous == new IntPtr(-1)) return false;
            var success = UpdateLayeredWindow(Handle, IntPtr.Zero, ref location, ref size,
                memoryDC, ref source, 0, ref blend, 2);
            if (success) renderedDpi = owner.DeviceDpi;
            return success;
        }
        finally
        {
            if (previous != IntPtr.Zero && previous != new IntPtr(-1)) SelectObject(memoryDC, previous);
            if (bitmapHandle != IntPtr.Zero) DeleteObject(bitmapHandle);
            DeleteDC(memoryDC);
        }
    }

    internal static Bitmap Render(Size contentSize, int dpi)
    {
        var scale = dpi / 96f;
        var margin = (int)Math.Ceiling(MarginSize * scale);
        var bitmap = new Bitmap(contentSize.Width + margin * 2, contentSize.Height + margin * 2, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shade = new SolidBrush(Color.FromArgb(3, Color.Black));
        for (var spread = 12; spread >= 0; spread--)
        {
            var bounds = new RectangleF(margin - spread * scale, margin + (3 - spread) * scale,
                contentSize.Width + spread * scale * 2, contentSize.Height + spread * scale * 2);
            using var shape = DashboardForm.Rounded(bounds, (24 + spread) * scale);
            graphics.FillPath(shade, shape);
        }
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.SmoothingMode = SmoothingMode.None;
        using var center = DashboardForm.Rounded(new RectangleF(margin, margin, contentSize.Width, contentSize.Height), 24 * scale);
        using var clear = new SolidBrush(Color.Transparent);
        graphics.FillPath(clear, center);
        return bitmap;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateLayeredWindow(IntPtr window, IntPtr destinationDC, ref Point destination,
        ref Size size, IntPtr sourceDC, ref Point source, uint colorKey, ref BlendFunction blend, uint flags);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr deviceContext);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr deviceContext, IntPtr value);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr value);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr deviceContext);
}
