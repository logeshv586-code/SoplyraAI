using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Text;
using SoplyraAI.Models;

namespace SoplyraAI.Services;

public sealed class ReliablePdfExportService
{
    // Render A4 pages at ~216 DPI so captured UI text stays sharp when the PDF is zoomed.
    private const int PagePixelWidth = 1800;
    private const int PagePixelHeight = 2546;
    private const float PdfPageWidth = 595f;
    private const float PdfPageHeight = 842f;

    public ReliablePdfExportService(ExportService exporter)
    {
        ArgumentNullException.ThrowIfNull(exporter);
    }

    public Task<string?> ExportAsync(
        GuideSession session,
        string folder,
        CancellationToken cancellationToken = default)
    {
        if (session.Steps.Count == 0)
            throw new InvalidOperationException("The guide does not contain any recorded steps to export.");

        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(folder);
        var pdf = Path.Combine(folder, "guide.pdf");
        if (File.Exists(pdf)) File.Delete(pdf);

        try
        {
            var document = BuildDocument(session);
            var pages = new List<RasterPage>(document.Steps.Count);

            for (var i = 0; i < document.Steps.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                pages.Add(RenderStepPage(document, document.Steps[i], i + 1, cancellationToken));
            }

            WriteRasterPdf(pdf, pages, cancellationToken);
            if (!IsCompletePdf(pdf))
                throw new InvalidOperationException("SoplyraAI generated an incomplete PDF file.");

            return Task.FromResult<string?>(pdf);
        }
        catch (OperationCanceledException)
        {
            try { if (File.Exists(pdf)) File.Delete(pdf); } catch { }
            throw;
        }
        catch (Exception ex)
        {
            try { if (File.Exists(pdf)) File.Delete(pdf); } catch { }
            throw new InvalidOperationException(
                "PDF generation failed inside SoplyraAI. Word, HTML and Markdown export remain available. " +
                PrivacySanitizer.Clean(ex.Message, 300), ex);
        }
    }

    private static PdfDocumentData BuildDocument(GuideSession session)
    {
        var title = PrivacySanitizer.Clean(session.Title, 200);
        if (string.IsNullOrWhiteSpace(title)) title = "Standard Operating Procedure";

        var steps = session.Steps.Select(step => BuildStep(step, session)).ToList();
        return new PdfDocumentData(
            title,
            session.DocumentationMode == "Detailed" ? "Detailed SOP" : "Quick Visual Guide",
            steps);
    }

    private static PdfStepData BuildStep(GuideStep step, GuideSession session)
    {
        var context = step.Context ?? new UiContext();
        var action = PrivacySanitizer.Clean(step.Action, 40);
        if (string.IsNullOrWhiteSpace(action)) action = "Click";

        var control = PrivacySanitizer.Clean(
            !string.IsNullOrWhiteSpace(context.LocalizedControlType)
                ? context.LocalizedControlType
                : context.ControlType,
            100);
        if (string.IsNullOrWhiteSpace(control)) control = "control";

        var element = PrivacySanitizer.Clean(context.ElementName, 180);
        if (string.IsNullOrWhiteSpace(element)) element = HumanizeControl(control);

        var title = PrivacySanitizer.Clean(step.Title, 240);
        if (string.IsNullOrWhiteSpace(title)) title = $"{action} {element}";

        var application = PrivacySanitizer.Clean(context.ProcessName, 120);
        if (string.IsNullOrWhiteSpace(application)) application = "Target application";
        var window = PrivacySanitizer.Clean(context.WindowTitle, 240);

        var instruction = BuildInstruction(action, element, control, application, window);
        var purpose = PrivacySanitizer.Clean(step.Description, 2500);
        if (string.IsNullOrWhiteSpace(purpose)) purpose = BuildPurpose(element, title, control);
        var expected = BuildExpectedResult(element, title, purpose, control);

        var imageBytes = Array.Empty<byte>();
        if (PathSecurity.TryGetTrustedPng(session.SessionFolder, step.ScreenshotPath, out var trusted, requireExists: true))
        {
            try { imageBytes = File.ReadAllBytes(trusted); }
            catch { imageBytes = Array.Empty<byte>(); }
        }

        return new PdfStepData(
            step.Number,
            title,
            instruction,
            purpose,
            expected,
            action,
            control,
            application,
            window,
            imageBytes);
    }

    private static RasterPage RenderStepPage(
        PdfDocumentData document,
        PdfStepData step,
        int pageNumber,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var bitmap = NewPageBitmap();
        using var g = PrepareGraphics(bitmap);

        var pageBackground = Color.FromArgb(247, 249, 252);
        var inkColor = Color.FromArgb(24, 35, 58);
        var mutedColor = Color.FromArgb(94, 111, 139);
        var borderColor = Color.FromArgb(218, 225, 236);
        var blue = Color.FromArgb(37, 99, 235);
        var blueTint = Color.FromArgb(239, 246, 255);
        var orange = Color.FromArgb(230, 126, 0);
        var orangeTint = Color.FromArgb(255, 247, 230);
        var green = Color.FromArgb(5, 150, 105);
        var greenTint = Color.FromArgb(236, 253, 245);

        g.Clear(pageBackground);

        using var ink = new SolidBrush(inkColor);
        using var muted = new SolidBrush(mutedColor);
        using var blueBrush = new SolidBrush(blue);
        using var white = new SolidBrush(Color.White);
        using var border = new Pen(borderColor, 2.2f);
        using var guideFont = new Font("Segoe UI", 10.5f, FontStyle.Bold, GraphicsUnit.Point);
        using var guideMetaFont = new Font("Segoe UI", 9.2f, FontStyle.Regular, GraphicsUnit.Point);
        using var stepLabelFont = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point);
        using var bodyFont = new Font("Segoe UI", 11.25f, FontStyle.Regular, GraphicsUnit.Point);
        using var captionFont = new Font("Segoe UI", 8.8f, FontStyle.Regular, GraphicsUnit.Point);
        using var contextFont = new Font("Segoe UI", 8.9f, FontStyle.Regular, GraphicsUnit.Point);

        var outer = new RectangleF(58, 48, PagePixelWidth - 116, PagePixelHeight - 118);
        DrawShadow(g, outer, 24);
        FillRoundedRectangle(g, white, outer, 24);
        DrawRoundedRectangle(g, border, outer, 24);

        const float left = 112f;
        var width = PagePixelWidth - left * 2;

        // Premium document header.
        using (var brandFont = new Font("Segoe UI", 8.5f, FontStyle.Bold, GraphicsUnit.Point))
            g.DrawString("SOPLYRAAI  •  WORKFLOW DOCUMENTATION", brandFont, blueBrush, left, 78);

        var guideName = FitText(g, document.Title, guideFont, 1000, 38);
        g.DrawString(guideName, guideFont, ink, new RectangleF(left, 112, 1000, 38));
        g.DrawString(
            document.DocumentationType,
            guideMetaFont,
            muted,
            new RectangleF(1180, 112, 500, 36),
            new StringFormat { Alignment = StringAlignment.Far });

        using (var divider = new Pen(Color.FromArgb(229, 234, 243), 2))
            g.DrawLine(divider, left, 164, PagePixelWidth - left, 164);

        // Step identity.
        var numberBadge = new RectangleF(left, 192, 70, 70);
        using (var badgeBrush = new SolidBrush(blue))
            FillRoundedRectangle(g, badgeBrush, numberBadge, 18);
        using (var numberFont = new Font("Segoe UI", 14f, FontStyle.Bold, GraphicsUnit.Point))
            g.DrawString(step.Number.ToString(), numberFont, Brushes.White, numberBadge, CenterFormat());

        g.DrawString($"STEP {step.Number:00}", stepLabelFont, blueBrush, left + 92, 192);
        var titleBottom = DrawAdaptiveTitle(g, step.Title, ink, left + 92, 220, width - 92, 115);
        var screenTop = Math.Max(330f, titleBottom + 22f);

        // Measure the premium guidance cards before deciding how much room the screenshot gets.
        var sectionWidth = width;
        var instructionHeight = MeasureSectionHeight(g, step.Instruction, bodyFont, sectionWidth, 142, 230);
        var purposeHeight = MeasureSectionHeight(g, step.Purpose, bodyFont, sectionWidth, 155, 300);
        var expectedHeight = MeasureSectionHeight(g, step.ExpectedResult, bodyFont, sectionWidth, 142, 250);
        const float sectionGap = 18f;
        const float captionHeight = 48f;
        const float contextHeight = 64f;
        const float contentBottom = 2390f;

        var belowScreen =
            captionHeight +
            instructionHeight + sectionGap +
            purposeHeight + sectionGap +
            expectedHeight + sectionGap +
            contextHeight;

        var screenHeight = Math.Clamp(contentBottom - screenTop - belowScreen, 360f, 790f);
        var screenRect = new RectangleF(left, screenTop, width, screenHeight);

        DrawShadow(g, screenRect, 18, 5, 10);
        FillRoundedRectangle(g, white, screenRect, 18);
        DrawRoundedRectangle(g, border, screenRect, 18);

        if (step.ImageBytes.Length > 0)
        {
            try
            {
                using var stream = new MemoryStream(step.ImageBytes, writable: false);
                using var image = Image.FromStream(stream, useEmbeddedColorManagement: true, validateImageData: true);
                var imageContainer = new RectangleF(screenRect.X + 14, screenRect.Y + 14, screenRect.Width - 28, screenRect.Height - 28);
                var target = FitImage(image.Width, image.Height, imageContainer, 0);
                g.DrawImage(image, target);
            }
            catch
            {
                DrawMissingScreenshot(g, screenRect, bodyFont, muted);
            }
        }
        else
        {
            DrawMissingScreenshot(g, screenRect, bodyFont, muted);
        }

        var y = screenRect.Bottom + 14;
        g.DrawString(
            $"Captured screen • Step {step.Number:00} • highlighted target shows the recorded interaction",
            captionFont,
            muted,
            new RectangleF(left + 4, y, width - 8, 34));
        y += captionHeight;

        y = DrawPremiumSection(
            g, "i", "HOW TO PERFORM", step.Instruction,
            left, y, sectionWidth, instructionHeight,
            blue, blueTint, inkColor, borderColor, bodyFont) + sectionGap;

        y = DrawPremiumSection(
            g, "→", "WHAT THIS DOES", step.Purpose,
            left, y, sectionWidth, purposeHeight,
            orange, orangeTint, inkColor, borderColor, bodyFont) + sectionGap;

        y = DrawPremiumSection(
            g, "✓", "EXPECTED RESULT", step.ExpectedResult,
            left, y, sectionWidth, expectedHeight,
            green, greenTint, inkColor, borderColor, bodyFont) + sectionGap;

        DrawContextPills(g, step, left, y, width, contextFont, inkColor, mutedColor, borderColor);

        DrawFooter(g, pageNumber, document.Steps.Count, captionFont, muted);
        return EncodePage(bitmap);
    }

    private static float DrawPremiumSection(
        Graphics g,
        string icon,
        string label,
        string text,
        float x,
        float y,
        float width,
        float height,
        Color accent,
        Color tint,
        Color bodyColor,
        Color borderColor,
        Font bodyFont)
    {
        var rect = new RectangleF(x, y, width, height);
        using var white = new SolidBrush(Color.White);
        using var border = new Pen(borderColor, 2f);
        FillRoundedRectangle(g, white, rect, 18);
        DrawRoundedRectangle(g, border, rect, 18);

        var iconRect = new RectangleF(x + 22, y + 20, 38, 38);
        using var tintBrush = new SolidBrush(tint);
        using var accentBrush = new SolidBrush(accent);
        FillRoundedRectangle(g, tintBrush, iconRect, 10);
        using (var iconFont = new Font("Segoe UI", 9.8f, FontStyle.Bold, GraphicsUnit.Point))
            g.DrawString(icon, iconFont, accentBrush, iconRect, CenterFormat());

        using (var labelFont = new Font("Segoe UI", 9.4f, FontStyle.Bold, GraphicsUnit.Point))
        {
            var labelFormat = new StringFormat(StringFormat.GenericTypographic);
            g.DrawString(label, labelFont, accentBrush, new RectangleF(x + 76, y + 23, width - 102, 34), labelFormat);
        }

        var bodyTop = y + 72;
        var bodyHeight = height - 92;
        var fitted = FitText(g, text, bodyFont, width - 48, bodyHeight);
        using var bodyBrush = new SolidBrush(bodyColor);
        using var bodyFormat = new StringFormat(StringFormat.GenericTypographic)
        {
            Trimming = StringTrimming.EllipsisWord,
            FormatFlags = StringFormatFlags.LineLimit
        };
        g.DrawString(
            fitted,
            bodyFont,
            bodyBrush,
            new RectangleF(x + 24, bodyTop, width - 48, bodyHeight),
            bodyFormat);

        return y + height;
    }

    private static void DrawContextPills(
        Graphics g,
        PdfStepData step,
        float x,
        float y,
        float maxWidth,
        Font font,
        Color inkColor,
        Color mutedColor,
        Color borderColor)
    {
        var cursor = x;
        cursor = DrawPill(g, $"Action: {step.Action}", cursor, y, font, inkColor, borderColor);
        cursor += 14;
        cursor = DrawPill(g, $"Control: {step.Control}", cursor, y, font, inkColor, borderColor);
        cursor += 14;

        var application = $"Application: {step.Application}";
        var applicationWidth = MeasurePillWidth(g, application, font);
        if (cursor + applicationWidth <= x + maxWidth)
            _ = DrawPill(g, application, cursor, y, font, mutedColor, borderColor);
    }

    private static float DrawPill(
        Graphics g,
        string text,
        float x,
        float y,
        Font font,
        Color textColor,
        Color borderColor)
    {
        var width = MeasurePillWidth(g, text, font);
        var rect = new RectangleF(x, y, width, 44);
        using var fill = new SolidBrush(Color.FromArgb(250, 252, 255));
        using var border = new Pen(borderColor, 1.6f);
        using var brush = new SolidBrush(textColor);
        FillRoundedRectangle(g, fill, rect, 11);
        DrawRoundedRectangle(g, border, rect, 11);
        g.DrawString(text, font, brush, new RectangleF(x + 14, y + 9, width - 28, 28));
        return x + width;
    }

    private static float MeasurePillWidth(Graphics g, string text, Font font) =>
        Math.Clamp(g.MeasureString(text, font).Width + 30f, 112f, 430f);

    private static float DrawAdaptiveTitle(
        Graphics g,
        string? value,
        Brush brush,
        float x,
        float y,
        float width,
        float maxHeight)
    {
        var text = PrivacySanitizer.Clean(value, 500);
        if (string.IsNullOrWhiteSpace(text)) text = "Recorded workflow step";

        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            FormatFlags = StringFormatFlags.LineLimit,
            Trimming = StringTrimming.EllipsisWord
        };

        float[] sizes = { 20f, 19f, 18f, 17f, 16f, 15f };
        foreach (var size in sizes)
        {
            using var font = new Font("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Point);
            var measured = g.MeasureString(text, font, (int)width, format);
            if (measured.Height <= maxHeight)
            {
                g.DrawString(text, font, brush, new RectangleF(x, y, width, maxHeight), format);
                return y + measured.Height;
            }
        }

        using var fallback = new Font("Segoe UI", 14f, FontStyle.Bold, GraphicsUnit.Point);
        var fitted = FitText(g, text, fallback, width, maxHeight);
        var fallbackSize = g.MeasureString(fitted, fallback, (int)width, format);
        g.DrawString(fitted, fallback, brush, new RectangleF(x, y, width, maxHeight), format);
        return y + Math.Min(maxHeight, fallbackSize.Height);
    }

    private static float MeasureSectionHeight(
        Graphics g,
        string? text,
        Font bodyFont,
        float width,
        float minHeight,
        float maxHeight)
    {
        var clean = PrivacySanitizer.Clean(text, 4000);
        if (string.IsNullOrWhiteSpace(clean)) clean = "Not available.";
        var availableWidth = Math.Max(1f, width - 48f);
        var measured = g.MeasureString(clean, bodyFont, new SizeF(availableWidth, maxHeight - 92f)).Height;
        return Math.Clamp(measured + 102f, minHeight, maxHeight);
    }

    private static Bitmap NewPageBitmap()
    {
        var bitmap = new Bitmap(PagePixelWidth, PagePixelHeight, PixelFormat.Format24bppRgb);
        bitmap.SetResolution(216f, 216f);
        return bitmap;
    }

    private static Graphics PrepareGraphics(Bitmap bitmap)
    {
        var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        return g;
    }

    private static RasterPage EncodePage(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        var codec = ImageCodecInfo.GetImageEncoders().FirstOrDefault(item => item.FormatID == ImageFormat.Jpeg.Guid);
        if (codec is null)
        {
            bitmap.Save(stream, ImageFormat.Jpeg);
        }
        else
        {
            using var parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 96L);
            bitmap.Save(stream, codec, parameters);
        }

        return new RasterPage(PagePixelWidth, PagePixelHeight, stream.ToArray());
    }

    private static void WriteRasterPdf(
        string path,
        IReadOnlyList<RasterPage> pages,
        CancellationToken cancellationToken)
    {
        if (pages.Count == 0) throw new InvalidOperationException("No PDF pages were generated.");

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        var objectCount = 2 + pages.Count * 3;
        var offsets = new long[objectCount + 1];

        WriteAscii(stream, "%PDF-1.4\n%SoplyraAI\n");

        WriteObject(stream, offsets, 1, "<< /Type /Catalog /Pages 2 0 R >>");
        var kids = string.Join(" ", Enumerable.Range(0, pages.Count).Select(i => $"{3 + i * 3} 0 R"));
        WriteObject(stream, offsets, 2, $"<< /Type /Pages /Kids [{kids}] /Count {pages.Count} >>");

        for (var i = 0; i < pages.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = pages[i];
            var pageObject = 3 + i * 3;
            var contentObject = pageObject + 1;
            var imageObject = pageObject + 2;

            WriteObject(
                stream,
                offsets,
                pageObject,
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PdfPageWidth:0} {PdfPageHeight:0}] " +
                $"/Resources << /XObject << /Im1 {imageObject} 0 R >> >> /Contents {contentObject} 0 R >>");

            var content = Encoding.ASCII.GetBytes($"q\n{PdfPageWidth:0} 0 0 {PdfPageHeight:0} 0 0 cm\n/Im1 Do\nQ\n");
            WriteStreamObject(stream, offsets, contentObject, $"<< /Length {content.Length} >>", content);

            var imageHeader =
                $"<< /Type /XObject /Subtype /Image /Width {page.Width} /Height {page.Height} " +
                "/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Interpolate true " +
                $"/Length {page.JpegBytes.Length} >>";
            WriteStreamObject(stream, offsets, imageObject, imageHeader, page.JpegBytes);
        }

        var xref = stream.Position;
        WriteAscii(stream, $"xref\n0 {objectCount + 1}\n");
        WriteAscii(stream, "0000000000 65535 f \n");
        for (var i = 1; i <= objectCount; i++)
            WriteAscii(stream, $"{offsets[i]:0000000000} 00000 n \n");

        WriteAscii(
            stream,
            $"trailer\n<< /Size {objectCount + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        stream.Flush(flushToDisk: true);
    }

    private static void WriteObject(FileStream stream, long[] offsets, int id, string body)
    {
        offsets[id] = stream.Position;
        WriteAscii(stream, $"{id} 0 obj\n{body}\nendobj\n");
    }

    private static void WriteStreamObject(
        FileStream stream,
        long[] offsets,
        int id,
        string dictionary,
        byte[] data)
    {
        offsets[id] = stream.Position;
        WriteAscii(stream, $"{id} 0 obj\n{dictionary}\nstream\n");
        stream.Write(data, 0, data.Length);
        WriteAscii(stream, "\nendstream\nendobj\n");
    }

    private static void WriteAscii(Stream stream, string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }

    internal static bool IsCompletePdf(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length < 4096) return false;

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Span<byte> header = stackalloc byte[5];
            if (stream.Read(header) != header.Length || Encoding.ASCII.GetString(header) != "%PDF-")
                return false;

            var tailLength = (int)Math.Min(4096, stream.Length);
            stream.Seek(-tailLength, SeekOrigin.End);
            var tail = new byte[tailLength];
            var read = stream.Read(tail, 0, tail.Length);
            return Encoding.ASCII.GetString(tail, 0, read).Contains("%EOF", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static string FitText(Graphics g, string? value, Font font, float width, float maxHeight)
    {
        var text = PrivacySanitizer.Clean(value, 4000);
        if (string.IsNullOrWhiteSpace(text)) return "Not available.";
        if (g.MeasureString(text, font, new SizeF(width, maxHeight)).Height <= maxHeight) return text;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return text;

        var builder = new StringBuilder();
        foreach (var word in words)
        {
            var candidate = builder.Length == 0 ? word : builder + " " + word;
            var withEllipsis = candidate + "…";
            if (g.MeasureString(withEllipsis, font, new SizeF(width, maxHeight)).Height > maxHeight)
                break;
            if (builder.Length > 0) builder.Append(' ');
            builder.Append(word);
        }

        return builder.Length == 0 ? "…" : builder + "…";
    }

    private static RectangleF FitImage(int width, int height, RectangleF container, float padding)
    {
        if (width <= 0 || height <= 0) return container;
        var availableWidth = container.Width - padding * 2;
        var availableHeight = container.Height - padding * 2;
        var scale = Math.Min(availableWidth / width, availableHeight / height);
        var targetWidth = width * scale;
        var targetHeight = height * scale;
        return new RectangleF(
            container.X + (container.Width - targetWidth) / 2,
            container.Y + (container.Height - targetHeight) / 2,
            targetWidth,
            targetHeight);
    }

    private static void DrawMissingScreenshot(Graphics g, RectangleF rect, Font font, Brush muted)
    {
        g.DrawString(
            "Screenshot not available for this recorded step.\nThe captured UI context and instructions are documented below.",
            font,
            muted,
            rect,
            CenterFormat());
    }

    private static void DrawFooter(Graphics g, int page, int total, Font font, Brush muted)
    {
        g.DrawString("Generated locally with SoplyraAI • Private workflow documentation", font, muted, 112, 2468);
        g.DrawString(
            $"Page {page} of {total}",
            font,
            muted,
            new RectangleF(1400, 2468, 280, 34),
            new StringFormat { Alignment = StringAlignment.Far });
    }

    private static string BuildInstruction(
        string action,
        string element,
        string control,
        string application,
        string window)
    {
        var normalized = control.ToLowerInvariant();
        var target = $"“{element}”";
        var location = normalized.Contains("toolbar")
            ? " from the toolbar area"
            : normalized.Contains("tab")
                ? " tab"
                : normalized.Contains("menu")
                    ? " menu item"
                    : normalized.Contains("button")
                        ? " button"
                        : normalized.Contains("combo") || normalized.Contains("drop")
                            ? " drop-down"
                            : normalized.Contains("edit") || normalized.Contains("text")
                                ? " field"
                                : normalized.Contains("link") || normalized.Contains("hyperlink")
                                    ? " link"
                                    : " control";

        if (element.Equals("highlighted area", StringComparison.OrdinalIgnoreCase))
            return $"{action} the highlighted area shown in the captured screen. Use the screenshot as the authoritative visual reference because Windows did not expose a more specific control name.";

        var verb = action.Equals("Right-click", StringComparison.OrdinalIgnoreCase)
            ? "Right-click"
            : action.Equals("Middle-click", StringComparison.OrdinalIgnoreCase)
                ? "Middle-click"
                : action.Equals("Select", StringComparison.OrdinalIgnoreCase)
                    ? "Select"
                    : "Click";

        var context = string.IsNullOrWhiteSpace(window)
            ? $" in {application}"
            : $" in {window}";

        return $"{verb} {target}{location}{context}. Use the highlighted target in the captured screen to confirm the correct control before continuing.";
    }

    private static string BuildPurpose(string element, string title, string control)
    {
        var text = $"{element} {title}".ToLowerInvariant();
        if (ContainsAny(text, "save", "apply")) return "Saves the current changes so the workflow can continue without losing the information already entered.";
        if (ContainsAny(text, "submit", "send")) return "Submits the current information to the application for processing and advances the workflow when accepted.";
        if (ContainsAny(text, "add", "create", "new")) return "Starts a new item or entry so the next part of the workflow can be completed.";
        if (ContainsAny(text, "delete", "remove")) return "Removes the selected item, subject to any confirmation or permission check shown by the application.";
        if (ContainsAny(text, "search", "find")) return "Runs the current search or filter so matching information can be reviewed.";
        if (ContainsAny(text, "next", "continue")) return "Advances the workflow to the next stage or screen.";
        if (ContainsAny(text, "back", "previous")) return "Returns the workflow to the previous stage or screen.";
        if (ContainsAny(text, "login", "sign in", "log in")) return "Starts the sign-in action so the authorized workflow can continue.";
        if (control.Contains("tab", StringComparison.OrdinalIgnoreCase)) return "Switches the application to the selected section so its controls and information become available.";
        if (control.Contains("toolbar", StringComparison.OrdinalIgnoreCase)) return "Opens or activates the selected toolbar action so its related commands or options can be used.";
        if (control.Contains("menu", StringComparison.OrdinalIgnoreCase)) return "Executes or opens the selected menu action so the relevant options become available.";
        return "Activates the selected control and moves the application into the state required for the next recorded step.";
    }

    private static string BuildExpectedResult(string element, string title, string purpose, string control)
    {
        var text = $"{element} {title} {purpose}".ToLowerInvariant();
        if (ContainsAny(text, "save", "apply")) return "The application should retain the saved changes and show the updated information without requiring the user to repeat the entry.";
        if (ContainsAny(text, "submit", "send")) return "The application should accept the information and display the next stage, confirmation, or processing status.";
        if (ContainsAny(text, "add", "create", "new")) return "A new item or entry should become available and be ready for the next required action.";
        if (ContainsAny(text, "delete", "remove")) return "The selected item should no longer appear after any required confirmation is completed.";
        if (ContainsAny(text, "search", "find")) return "The visible results should refresh to match the current search or filter criteria.";
        if (ContainsAny(text, "next", "continue")) return "The next workflow screen or stage should become visible and ready for input.";
        if (ContainsAny(text, "back", "previous")) return "The previous workflow screen or stage should become visible.";
        if (ContainsAny(text, "login", "sign in", "log in")) return "The authorized application screen should become available when authentication succeeds.";

        var normalized = control.ToLowerInvariant();
        if (normalized.Contains("toolbar")) return "The related toolbar menu, command, or panel should become available, leaving the interface ready for the next recorded step.";
        if (normalized.Contains("tab")) return "The selected section should become active and its associated content should be visible.";
        if (normalized.Contains("menu")) return "The selected menu action should open or execute, and the resulting interface state should be visible.";
        if (normalized.Contains("combo") || normalized.Contains("drop")) return "The available choices should appear so the required option can be selected.";
        if (normalized.Contains("check")) return "The setting should change to the selected state and remain visible in the interface.";
        if (normalized.Contains("edit") || normalized.Contains("text")) return "The field should receive focus and be ready for the next allowed input.";
        if (normalized.Contains("link") || normalized.Contains("hyperlink")) return "The linked destination or related view should open successfully.";

        return "The interface should respond to the selected control and remain in a stable state ready for the next recorded step.";
    }

    private static bool ContainsAny(string value, params string[] tokens) => tokens.Any(value.Contains);

    private static string HumanizeControl(string control)
    {
        var value = control
            .Replace("ControlType.", "", StringComparison.OrdinalIgnoreCase)
            .Replace("_", " ")
            .Trim();
        return string.IsNullOrWhiteSpace(value) ? "selected control" : value;
    }

    private static void DrawShadow(Graphics g, RectangleF rect, float radius, float offsetX = 5, float offsetY = 8)
    {
        var shadow = new RectangleF(rect.X + offsetX, rect.Y + offsetY, rect.Width, rect.Height);
        using var brush = new SolidBrush(Color.FromArgb(18, 30, 50, 80));
        FillRoundedRectangle(g, brush, shadow, radius);
    }

    private static void FillRoundedRectangle(Graphics g, Brush brush, RectangleF rect, float radius)
    {
        using var path = RoundedRectangle(rect, radius);
        g.FillPath(brush, path);
    }

    private static void DrawRoundedRectangle(Graphics g, Pen pen, RectangleF rect, float radius)
    {
        using var path = RoundedRectangle(rect, radius);
        g.DrawPath(pen, path);
    }

    private static GraphicsPath RoundedRectangle(RectangleF rect, float radius)
    {
        var diameter = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
        var arc = new RectangleF(rect.X, rect.Y, diameter, diameter);
        var path = new GraphicsPath();

        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static StringFormat CenterFormat() => new()
    {
        Alignment = StringAlignment.Center,
        LineAlignment = StringAlignment.Center
    };

    private sealed record PdfDocumentData(
        string Title,
        string DocumentationType,
        IReadOnlyList<PdfStepData> Steps);

    private sealed record PdfStepData(
        int Number,
        string Title,
        string Instruction,
        string Purpose,
        string ExpectedResult,
        string Action,
        string Control,
        string Application,
        string Window,
        byte[] ImageBytes);

    private sealed record RasterPage(int Width, int Height, byte[] JpegBytes);
}
