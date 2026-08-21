using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Text;
using SoplyraAI.Models;

namespace SoplyraAI.Services;

public sealed class ReliablePdfExportService
{
    private const int PagePixelWidth = 1240;
    private const int PagePixelHeight = 1754;
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
            var pages = new List<RasterPage>
            {
                RenderOverviewPage(document, cancellationToken)
            };

            foreach (var step in document.Steps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                pages.Add(RenderStepPage(document, step, cancellationToken));
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

        var applications = session.Steps
            .Select(step => PrivacySanitizer.Clean(step.Context?.ProcessName, 120))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
        var apps = applications.Count == 0 ? "Windows desktop application" : string.Join(", ", applications);

        var steps = session.Steps.Select(step => BuildStep(step, session)).ToList();
        var purpose =
            $"This document provides a repeatable, screen-by-screen procedure for the captured workflow in {apps}. " +
            "Each recorded screen is paired with an instruction, an explanation of what the action does, and the expected result.";
        var scope =
            $"The guide covers {steps.Count} recorded interaction{(steps.Count == 1 ? "" : "s")} from this SoplyraAI session. " +
            "Passwords and protected credentials are not included.";
        var prerequisites =
            $"Open {apps}, sign in with the permissions required for the process, and navigate to the starting screen shown in Step 1.";
        var completion =
            $"The procedure is complete when all {steps.Count} recorded step{(steps.Count == 1 ? "" : "s")} have been performed in order " +
            "and the final expected result is visible.";

        return new PdfDocumentData(
            title,
            apps,
            session.DocumentationMode == "Detailed" ? "Detailed SOP" : "Quick Visual Guide",
            session.CreatedAt,
            purpose,
            scope,
            prerequisites,
            completion,
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

        var instruction = BuildInstruction(action, element, control);
        var purpose = PrivacySanitizer.Clean(step.Description, 2500);
        if (string.IsNullOrWhiteSpace(purpose)) purpose = BuildPurpose(element, title, control);
        var expected = BuildExpectedResult(element, title, purpose);

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

    private static RasterPage RenderOverviewPage(PdfDocumentData document, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var bitmap = NewPageBitmap();
        using var g = PrepareGraphics(bitmap);
        g.Clear(Color.FromArgb(248, 250, 252));

        using var ink = new SolidBrush(Color.FromArgb(15, 23, 42));
        using var muted = new SolidBrush(Color.FromArgb(100, 116, 139));
        using var primary = new SolidBrush(Color.FromArgb(79, 70, 229));
        using var softPrimary = new SolidBrush(Color.FromArgb(238, 242, 255));
        using var white = new SolidBrush(Color.White);
        using var border = new Pen(Color.FromArgb(226, 232, 240), 2);
        using var titleFont = new Font("Segoe UI", 30, FontStyle.Bold, GraphicsUnit.Point);
        using var subtitleFont = new Font("Segoe UI", 12, FontStyle.Regular, GraphicsUnit.Point);
        using var metaFont = new Font("Segoe UI", 10.5f, FontStyle.Regular, GraphicsUnit.Point);
        using var headingFont = new Font("Segoe UI", 15, FontStyle.Bold, GraphicsUnit.Point);
        using var sectionLabelFont = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point);
        using var bodyFont = new Font("Segoe UI", 11.5f, FontStyle.Regular, GraphicsUnit.Point);
        using var smallFont = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);

        var card = new RectangleF(65, 60, 1110, 1570);
        g.FillRectangle(white, card);
        g.DrawRectangle(border, card.X, card.Y, card.Width, card.Height);

        g.FillRectangle(softPrimary, 105, 105, 315, 44);
        g.DrawString("STANDARD OPERATING PROCEDURE", sectionLabelFont, primary, 122, 116);

        var y = 180f;
        y = DrawFitText(g, document.Title, titleFont, ink, 105, y, 1020, 145) + 18;
        g.DrawString("Generated from a captured Windows workflow by SoplyraAI", subtitleFont, muted, 105, y);
        y += 60;

        DrawMetaCard(g, "APPLICATIONS", document.Applications, 105, y, 490, 105, sectionLabelFont, metaFont, muted, ink, border);
        DrawMetaCard(g, "DOCUMENTATION TYPE", document.DocumentationType, 620, y, 490, 105, sectionLabelFont, metaFont, muted, ink, border);
        y += 125;
        DrawMetaCard(g, "TOTAL STEPS", document.Steps.Count.ToString(), 105, y, 490, 105, sectionLabelFont, metaFont, muted, ink, border);
        DrawMetaCard(g, "RECORDED", document.CreatedAt.ToString("dd MMM yyyy · HH:mm"), 620, y, 490, 105, sectionLabelFont, metaFont, muted, ink, border);
        y += 155;

        g.DrawString("Document overview", headingFont, ink, 105, y);
        y += 48;
        y = DrawOverviewBlock(g, "PURPOSE", document.Purpose, 105, y, 1020, 140, sectionLabelFont, bodyFont, primary, ink, border) + 16;
        y = DrawOverviewBlock(g, "SCOPE", document.Scope, 105, y, 1020, 120, sectionLabelFont, bodyFont, primary, ink, border) + 16;
        y = DrawOverviewBlock(g, "PREREQUISITES", document.Prerequisites, 105, y, 1020, 120, sectionLabelFont, bodyFont, primary, ink, border) + 16;
        y = DrawOverviewBlock(g, "COMPLETION CRITERIA", document.CompletionCriteria, 105, y, 1020, 120, sectionLabelFont, bodyFont, primary, ink, border) + 20;

        if (document.Steps.Count > 0 && y < 1420)
        {
            g.DrawString("Workflow step index", headingFont, ink, 105, y);
            y += 43;
            var take = Math.Min(document.Steps.Count, 8);
            for (var i = 0; i < take && y < 1530; i++)
            {
                var step = document.Steps[i];
                using var badge = new SolidBrush(Color.FromArgb(79, 70, 229));
                g.FillEllipse(badge, 108, y + 2, 30, 30);
                g.DrawString(step.Number.ToString(), smallFont, Brushes.White, new RectangleF(108, y + 5, 30, 22), CenterFormat());
                var line = FitText(g, step.Title, bodyFont, 930, 38);
                g.DrawString(line, bodyFont, ink, new RectangleF(155, y, 930, 40));
                y += 42;
            }
            if (document.Steps.Count > take && y < 1565)
                g.DrawString($"+ {document.Steps.Count - take} more recorded steps", smallFont, muted, 155, y);
        }

        DrawFooter(g, 1, document.Steps.Count + 1, smallFont, muted);
        return EncodePage(bitmap);
    }

    private static RasterPage RenderStepPage(
        PdfDocumentData document,
        PdfStepData step,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var bitmap = NewPageBitmap();
        using var g = PrepareGraphics(bitmap);
        g.Clear(Color.FromArgb(248, 250, 252));

        using var ink = new SolidBrush(Color.FromArgb(15, 23, 42));
        using var muted = new SolidBrush(Color.FromArgb(100, 116, 139));
        using var primary = new SolidBrush(Color.FromArgb(79, 70, 229));
        using var soft = new SolidBrush(Color.FromArgb(248, 250, 252));
        using var white = new SolidBrush(Color.White);
        using var border = new Pen(Color.FromArgb(226, 232, 240), 2);
        using var titleFont = new Font("Segoe UI", 20, FontStyle.Bold, GraphicsUnit.Point);
        using var labelFont = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point);
        using var bodyFont = new Font("Segoe UI", 11.25f, FontStyle.Regular, GraphicsUnit.Point);
        using var captionFont = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
        using var metaFont = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);

        var card = new RectangleF(65, 55, 1110, 1595);
        g.FillRectangle(white, card);
        g.DrawRectangle(border, card.X, card.Y, card.Width, card.Height);

        g.FillEllipse(primary, 105, 100, 58, 58);
        g.DrawString(step.Number.ToString(), bodyFont, Brushes.White, new RectangleF(105, 115, 58, 30), CenterFormat());
        g.DrawString($"STEP {step.Number:00}", labelFont, primary, 185, 101);
        var y = DrawFitText(g, step.Title, titleFont, ink, 185, 125, 925, 92) + 20;

        var screenRect = new RectangleF(105, y, 1020, 610);
        g.FillRectangle(soft, screenRect);
        g.DrawRectangle(border, screenRect.X, screenRect.Y, screenRect.Width, screenRect.Height);

        if (step.ImageBytes.Length > 0)
        {
            try
            {
                using var stream = new MemoryStream(step.ImageBytes, writable: false);
                using var image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
                var target = FitImage(image.Width, image.Height, screenRect, 18);
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

        y += 635;
        g.DrawString(
            $"Captured screen for Step {step.Number}. The action and result are explained below.",
            captionFont,
            muted,
            new RectangleF(105, y, 1020, 34));
        y += 50;

        y = DrawTextSection(g, "HOW TO PERFORM", step.Instruction, 105, y, 1020, 135, labelFont, bodyFont, primary, ink, border) + 12;
        y = DrawTextSection(g, "WHAT THIS DOES", step.Purpose, 105, y, 1020, 190, labelFont, bodyFont, primary, ink, border) + 12;
        y = DrawTextSection(g, "EXPECTED RESULT", step.ExpectedResult, 105, y, 1020, 145, labelFont, bodyFont, primary, ink, border) + 14;

        var context = $"Action: {step.Action}   ·   Control: {step.Control}   ·   Application: {step.Application}" +
                      (string.IsNullOrWhiteSpace(step.Window) ? "" : $"   ·   Window: {step.Window}");
        g.FillRectangle(soft, 105, y, 1020, 62);
        g.DrawRectangle(border, 105, y, 1020, 62);
        g.DrawString(FitText(g, context, metaFont, 980, 42), metaFont, muted, new RectangleF(125, y + 16, 980, 36));

        DrawFooter(g, step.Number + 1, document.Steps.Count + 1, captionFont, muted);
        return EncodePage(bitmap);
    }

    private static Bitmap NewPageBitmap()
    {
        var bitmap = new Bitmap(PagePixelWidth, PagePixelHeight, PixelFormat.Format24bppRgb);
        bitmap.SetResolution(150f, 150f);
        return bitmap;
    }

    private static Graphics PrepareGraphics(Bitmap bitmap)
    {
        var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
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
            parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 90L);
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

    private static float DrawOverviewBlock(
        Graphics g,
        string label,
        string text,
        float x,
        float y,
        float width,
        float height,
        Font labelFont,
        Font bodyFont,
        Brush labelBrush,
        Brush bodyBrush,
        Pen border)
    {
        g.DrawRectangle(border, x, y, width, height);
        g.DrawString(label, labelFont, labelBrush, x + 18, y + 14);
        var fitted = FitText(g, text, bodyFont, width - 36, height - 46);
        g.DrawString(fitted, bodyFont, bodyBrush, new RectangleF(x + 18, y + 40, width - 36, height - 48));
        return y + height;
    }

    private static float DrawTextSection(
        Graphics g,
        string label,
        string text,
        float x,
        float y,
        float width,
        float height,
        Font labelFont,
        Font bodyFont,
        Brush labelBrush,
        Brush bodyBrush,
        Pen border)
    {
        g.DrawRectangle(border, x, y, width, height);
        g.DrawString(label, labelFont, labelBrush, x + 18, y + 13);
        var fitted = FitText(g, text, bodyFont, width - 36, height - 45);
        g.DrawString(fitted, bodyFont, bodyBrush, new RectangleF(x + 18, y + 39, width - 36, height - 45));
        return y + height;
    }

    private static void DrawMetaCard(
        Graphics g,
        string label,
        string value,
        float x,
        float y,
        float width,
        float height,
        Font labelFont,
        Font valueFont,
        Brush labelBrush,
        Brush valueBrush,
        Pen border)
    {
        g.DrawRectangle(border, x, y, width, height);
        g.DrawString(label, labelFont, labelBrush, x + 18, y + 15);
        g.DrawString(FitText(g, value, valueFont, width - 36, 48), valueFont, valueBrush, new RectangleF(x + 18, y + 46, width - 36, 46));
    }

    private static float DrawFitText(
        Graphics g,
        string text,
        Font font,
        Brush brush,
        float x,
        float y,
        float width,
        float maxHeight)
    {
        var fitted = FitText(g, text, font, width, maxHeight);
        var measured = g.MeasureString(fitted, font, new SizeF(width, maxHeight));
        g.DrawString(fitted, font, brush, new RectangleF(x, y, width, maxHeight));
        return y + Math.Min(maxHeight, measured.Height);
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

    private static StringFormat CenterFormat() => new()
    {
        Alignment = StringAlignment.Center,
        LineAlignment = StringAlignment.Center
    };

    private static void DrawFooter(Graphics g, int page, int total, Font font, Brush muted)
    {
        g.DrawString("Generated locally with SoplyraAI", font, muted, 105, 1665);
        g.DrawString($"Page {page} of {total}", font, muted, new RectangleF(900, 1665, 225, 30), new StringFormat { Alignment = StringAlignment.Far });
    }

    private static string BuildInstruction(string action, string element, string control)
    {
        var target = control.Contains("button", StringComparison.OrdinalIgnoreCase)
            ? $"the “{element}” button"
            : control.Contains("tab", StringComparison.OrdinalIgnoreCase)
                ? $"the “{element}” tab"
                : control.Contains("menu", StringComparison.OrdinalIgnoreCase)
                    ? $"the “{element}” menu item"
                    : $"“{element}”";

        if (action.Equals("Right-click", StringComparison.OrdinalIgnoreCase)) return $"Right-click {target} to open its available context actions.";
        if (action.Equals("Middle-click", StringComparison.OrdinalIgnoreCase)) return $"Middle-click {target}.";
        if (action.Equals("Select", StringComparison.OrdinalIgnoreCase)) return $"Select {target}.";
        return $"Click {target}.";
    }

    private static string BuildPurpose(string element, string title, string control)
    {
        var text = $"{element} {title}".ToLowerInvariant();
        if (ContainsAny(text, "save", "apply")) return "This stores the current changes so the workflow can continue without losing the entered information.";
        if (ContainsAny(text, "submit", "send")) return "This sends the current information to the application for processing.";
        if (ContainsAny(text, "add", "create", "new")) return "This starts or creates a new item in the current workflow.";
        if (ContainsAny(text, "delete", "remove")) return "This removes the selected item from the current workflow, subject to any confirmation shown by the application.";
        if (ContainsAny(text, "search", "find")) return "This runs the current search or filter so matching results can be reviewed.";
        if (ContainsAny(text, "next", "continue")) return "This advances the workflow to the next stage or screen.";
        if (ContainsAny(text, "back", "previous")) return "This returns the workflow to the previous stage or screen.";
        if (ContainsAny(text, "login", "sign in", "log in")) return "This starts the sign-in action so the authorized workflow can continue.";
        if (control.Contains("tab", StringComparison.OrdinalIgnoreCase)) return "This switches the application to the selected section so its controls and information become available.";
        return "This activates the selected control and updates the application for the next recorded step.";
    }

    private static string BuildExpectedResult(string element, string title, string purpose)
    {
        var text = $"{element} {title} {purpose}".ToLowerInvariant();
        if (ContainsAny(text, "save", "apply")) return "The application should confirm or retain the saved changes, and the updated information should remain available.";
        if (ContainsAny(text, "submit", "send")) return "The application should submit the information and display the next stage, a confirmation, or a status update.";
        if (ContainsAny(text, "add", "create", "new")) return "A new item or entry should become available for the next part of the workflow.";
        if (ContainsAny(text, "delete", "remove")) return "The selected item should no longer appear after any required confirmation is completed.";
        if (ContainsAny(text, "search", "find")) return "The visible results should refresh to match the current search or filter criteria.";
        if (ContainsAny(text, "next", "continue")) return "The next screen or workflow stage should become visible.";
        if (ContainsAny(text, "back", "previous")) return "The previous screen or workflow stage should become visible.";
        if (ContainsAny(text, "login", "sign in", "log in")) return "The authorized application screen should become available if the sign-in is successful.";
        return "The interface should respond to the selected control and leave the application ready for the next recorded step.";
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

    private sealed record PdfDocumentData(
        string Title,
        string Applications,
        string DocumentationType,
        DateTimeOffset CreatedAt,
        string Purpose,
        string Scope,
        string Prerequisites,
        string CompletionCriteria,
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
