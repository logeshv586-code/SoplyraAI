using System.Drawing;
using System.Drawing.Imaging;
using SoplyraAI.Models;

namespace SoplyraAI.Services;

public sealed class ScreenshotService
{
    public string Capture(string outputPath, UiContext context, string mode)
    {
        var bounds = mode.Equals("ActiveWindow", StringComparison.OrdinalIgnoreCase)
            ? GetForegroundBounds()
            : GetVirtualScreenBounds();

        if (bounds.Width < 1 || bounds.Height < 1)
            bounds = GetVirtualScreenBounds();

        using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var clickX = context.ClickX - bounds.Left;
            var clickY = context.ClickY - bounds.Top;
            using var ringPen = new Pen(Color.FromArgb(230, 91, 91, 214), 5);
            g.DrawEllipse(ringPen, clickX - 18, clickY - 18, 36, 36);
            using var dotBrush = new SolidBrush(Color.FromArgb(170, 91, 91, 214));
            g.FillEllipse(dotBrush, clickX - 5, clickY - 5, 10, 10);

            if (context.Width > 2 && context.Height > 2)
            {
                var rect = new RectangleF(
                    (float)(context.Left - bounds.Left),
                    (float)(context.Top - bounds.Top),
                    (float)context.Width,
                    (float)context.Height);

                if (context.IsPassword)
                {
                    using var mask = new SolidBrush(Color.FromArgb(245, 235, 235, 240));
                    g.FillRectangle(mask, rect);
                    using var labelBrush = new SolidBrush(Color.FromArgb(255, 100, 100, 110));
                    using var font = new Font("Segoe UI", 10, FontStyle.Bold);
                    g.DrawString("Sensitive field hidden", font, labelBrush, rect.Left + 8, rect.Top + 8);
                }
                else
                {
                    using var elementPen = new Pen(Color.FromArgb(220, 91, 91, 214), 3);
                    g.DrawRectangle(elementPen, rect.X, rect.Y, rect.Width, rect.Height);
                }
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        bitmap.Save(outputPath, ImageFormat.Png);
        return outputPath;
    }

    private static Rectangle GetForegroundBounds()
    {
        try
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd != IntPtr.Zero && NativeMethods.GetWindowRect(hwnd, out var rect))
            {
                var width = rect.Right - rect.Left;
                var height = rect.Bottom - rect.Top;
                if (width > 100 && height > 100) return new Rectangle(rect.Left, rect.Top, width, height);
            }
        }
        catch { }
        return GetVirtualScreenBounds();
    }

    private static Rectangle GetVirtualScreenBounds() => new(
        NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN),
        NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN),
        NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN),
        NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN));
}
