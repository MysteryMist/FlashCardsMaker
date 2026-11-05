using System.IO.Compression;
using FlashCardsMaker.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;
using IContainer = QuestPDF.Infrastructure.IContainer;
using PdfColors = QuestPDF.Helpers.Colors;

namespace FlashCardsMaker.Export;

public sealed class FlashcardExporter : IFlashcardExporter
{
    private const float PageMarginPoints = 22;
    private const float CardWidthInches = 3.62f;
    private const float CardHeightInches = 2.48f;
    private const int RasterDpi = 300;
    private static readonly string PdfFontFamily = "Times New Roman";
    private static readonly string[] JapaneseFontFamilies = ["MS Gothic", "Yu Gothic UI", "Meiryo"];
    private static readonly string[] TitleFontFamilies = ["Times New Roman", "Georgia"];
    private static readonly string[] InformationFontFamilies = ["Times New Roman", "Georgia"];
    private static readonly float CardWidthPoints = CardWidthInches * 72f;
    private static readonly float CardHeightPoints = CardHeightInches * 72f;

    public Task<ExportArtifact> GenerateAsync(Deck deck, IReadOnlyList<Flashcard> cards,
        IReadOnlyDictionary<int, IReadOnlyList<CardMeaning>> meaningsByCard,
        PaperSize paperSize, GridLayout gridLayout, DuplexFlipEdge flipEdge,
        ExportFormat format, ExportLayout exportLayout,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Generate(deck, cards, meaningsByCard, paperSize, gridLayout, flipEdge, format, exportLayout, cancellationToken), cancellationToken);

    private static ExportArtifact Generate(Deck deck, IReadOnlyList<Flashcard> cards,
        IReadOnlyDictionary<int, IReadOnlyList<CardMeaning>> meaningsByCard,
        PaperSize paperSize, GridLayout gridLayout, DuplexFlipEdge flipEdge, ExportFormat format,
        ExportLayout exportLayout, CancellationToken cancellationToken)
    {
        if (exportLayout == ExportLayout.BothSeparateFiles)
        {
            if (format == ExportFormat.PDF)
            {
                var output = new MemoryStream();
                using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
                {
                    AddZipFile(archive, $"{SafeName(deck.Name)}.pdf", CreatePdf(deck, cards, meaningsByCard, paperSize, gridLayout, flipEdge, cancellationToken, ExportLayout.FrontOnly));
                    AddZipFile(archive, $"{SafeName(deck.Name)} (Meaning).pdf", CreatePdf(deck, cards, meaningsByCard, paperSize, gridLayout, flipEdge, cancellationToken, ExportLayout.BackOnly));
                }

                return new ExportArtifact($"{SafeName(deck.Name)}-export.zip", output.ToArray());
            }

            return CreateRasterArchive(deck, cards, meaningsByCard, paperSize, gridLayout, flipEdge,
                format == ExportFormat.PNG ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg,
                format == ExportFormat.PNG ? "png" : "jpg", cancellationToken, ExportLayout.BothSeparateFiles);
        }

        return format switch
        {
            ExportFormat.PDF => new ExportArtifact($"{SafeName(deck.Name)}{(exportLayout == ExportLayout.BackOnly ? " (Meaning)" : string.Empty)}.pdf",
                CreatePdf(deck, cards, meaningsByCard, paperSize, gridLayout, flipEdge, cancellationToken, exportLayout)),
            ExportFormat.PNG => CreateRasterArchive(deck, cards, meaningsByCard, paperSize, gridLayout, flipEdge,
                SKEncodedImageFormat.Png, "png", cancellationToken, exportLayout),
            ExportFormat.JPEG => CreateRasterArchive(deck, cards, meaningsByCard, paperSize, gridLayout, flipEdge,
                SKEncodedImageFormat.Jpeg, "jpg", cancellationToken, exportLayout),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    private static void AddZipFile(ZipArchive archive, string fileName, byte[] content)
    {
        var entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(content, 0, content.Length);
    }

    private static byte[] CreatePdf(Deck deck, IReadOnlyList<Flashcard> cards,
        IReadOnlyDictionary<int, IReadOnlyList<CardMeaning>> meaningsByCard,
        PaperSize paperSize, GridLayout layout, DuplexFlipEdge flipEdge,
        CancellationToken cancellationToken, ExportLayout exportLayout)
    {
        var columns = (int)layout;
        var capacity = columns * columns;
        var size = paperSize == PaperSize.A4 ? PageSizes.A4 : PageSizes.Letter;
        var heightPoints = paperSize == PaperSize.A4 ? 841.89f : 792f;
        var rowHeight = (heightPoints - (2 * PageMarginPoints)) / columns;
        var selectedSides = exportLayout switch
        {
            ExportLayout.FrontOnly => new[] { true },
            ExportLayout.BackOnly => new[] { false },
            _ => new[] { true, false }
        };

        var document = Document.Create(documentBuilder =>
        {
            for (var batchStart = 0; batchStart < cards.Count; batchStart += capacity)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batch = cards.Skip(batchStart).Take(capacity).ToArray();
                foreach (var front in selectedSides)
                {
                    AddPdfPage(documentBuilder, size, rowHeight, deck, batch, meaningsByCard, columns, flipEdge, front);
                }
            }
        });
        return document.GeneratePdf();
    }

    private static void AddPdfPage(IDocumentContainer document, PageSize size, float rowHeight, Deck deck,
        IReadOnlyList<Flashcard> cards, IReadOnlyDictionary<int, IReadOnlyList<CardMeaning>> meaningsByCard,
        int columns, DuplexFlipEdge flipEdge, bool front)
    {
        var pageWidth = Math.Max(size.Width, (columns * CardWidthPoints) + (PageMarginPoints * 2));
        var pageHeight = Math.Max(size.Height, (columns * CardHeightPoints) + (PageMarginPoints * 2));

        document.Page(page =>
        {
            page.Size(new PageSize(pageWidth, pageHeight));
            page.Margin(PageMarginPoints);
            page.DefaultTextStyle(style => style.FontFamily(PdfFontFamily).FontSize(10));
            page.Content().Column(sheet =>
            {
                for (var rowIndex = 0; rowIndex < columns; rowIndex++)
                {
                    var capturedRow = rowIndex;
                    sheet.Item().Height(CardHeightPoints).Row(row =>
                    {
                        for (var columnIndex = 0; columnIndex < columns; columnIndex++)
                        {
                            var capturedColumn = columnIndex;
                            var cardIndex = GetCardIndex(capturedRow, capturedColumn, columns, flipEdge, front);
                            row.ConstantItem(CardWidthPoints).Padding(3).Element(cell =>
                            {
                                if (cardIndex < cards.Count)
                                {
                                    meaningsByCard.TryGetValue(cards[cardIndex].Id, out var meanings);
                                    DrawPdfCard(cell, deck, cards[cardIndex], meanings ?? Array.Empty<CardMeaning>(), front);
                                }
                                else
                                {
                                    cell.Border(0.7f).BorderColor(PdfColors.Grey.Lighten1);
                                }
                            });
                        }
                    });
                }
            });
        });
    }

    private static void DrawPdfCard(IContainer container, Deck deck, Flashcard card,
        IReadOnlyList<CardMeaning> meanings, bool front)
    {
        container.Border(1).BorderColor(PdfColors.Black).Background(PdfColors.White).Padding(12).Column(column =>
        {
            if (front)
            {
                column.Item().AlignLeft().Text($"{deck.Name.ToUpperInvariant()} {card.Number:000}")
                    .FontFamily(TitleFontFamilies)
                    .FontSize(10)
                    .Bold()
                    .FontColor(PdfColors.Red.Medium);
                column.Item().ExtendVertical().AlignMiddle().AlignCenter().Text(text =>
                {
                    foreach (var run in SplitByScript(card.FrontText))
                    {
                        text.Span(run.Text)
                            .FontFamily(run.IsJapanese ? JapaneseFontFamilies : TitleFontFamilies)
                            .FontSize(62)
                            .Bold();
                    }
                });
                return;
            }

            var kindColor = GetBackMetaColor(deck.CardType, card);
            column.Item().Text(GetBackMeta(deck.CardType, card))
                .FontFamily(InformationFontFamilies)
                .FontSize(10)
                .Bold()
                .FontColor(kindColor);
            column.Item().PaddingTop(4).Text($"{card.Number}.")
                .FontFamily(TitleFontFamilies)
                .FontSize(16)
                .Bold()
                .FontColor(PdfColors.Red.Medium);
            column.Item().Text(card.Hiragana)
                .FontFamily(JapaneseFontFamilies)
                .FontSize(16)
                .Bold()
                .FontColor(PdfColors.Red.Medium);
            foreach (var meaning in meanings.Where(item => !string.IsNullOrWhiteSpace(item.Text)))
            {
                column.Item().PaddingTop(3).Text(text =>
                {
                    var span = text.Span($"•  {meaning.Text}")
                        .FontFamily(InformationFontFamilies)
                        .FontSize(16);
                    if (meaning.Style is MeaningStyle.Bold or MeaningStyle.BoldUnderline)
                        span.Bold();
                    if (meaning.Style is MeaningStyle.Underline or MeaningStyle.BoldUnderline)
                        span.Underline();
                });
            }
        });
    }

    private static string GetBackMetaColor(CardType cardType, Flashcard card)
    {
        return cardType switch
        {
            CardType.Verb => card.VerbKind switch
            {
                VerbKind.Godan => PdfColors.Blue.Darken2,
                VerbKind.Ichidan => PdfColors.Green.Darken2,
                VerbKind.Irregular => PdfColors.Purple.Darken2,
                _ => PdfColors.Red.Medium
            },
            CardType.Adverb => card.AdverbClassification switch
            {
                AdverbClassification.Positive => PdfColors.Green.Darken2,
                AdverbClassification.Negative => PdfColors.Red.Medium,
                AdverbClassification.Neutral => PdfColors.Purple.Darken2,
                _ => PdfColors.Red.Medium
            },
            _ => PdfColors.Red.Medium
        };
    }

    private static ExportArtifact CreateRasterArchive(Deck deck, IReadOnlyList<Flashcard> cards,
        IReadOnlyDictionary<int, IReadOnlyList<CardMeaning>> meaningsByCard,
        PaperSize paperSize, GridLayout layout, DuplexFlipEdge flipEdge,
        SKEncodedImageFormat imageFormat, string extension, CancellationToken cancellationToken,
        ExportLayout exportLayout = ExportLayout.BothInOneFile)
    {
        var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            var columns = (int)layout;
            var capacity = columns * columns;
            var batchNumber = 0;
            var sides = exportLayout switch
            {
                ExportLayout.FrontOnly => new[] { true },
                ExportLayout.BackOnly => new[] { false },
                _ => new[] { true, false }
            };

            for (var batchStart = 0; batchStart < cards.Count; batchStart += capacity)
            {
                cancellationToken.ThrowIfCancellationRequested();
                batchNumber++;
                var batch = cards.Skip(batchStart).Take(capacity).ToArray();
                foreach (var front in sides)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var data = RenderRasterSheet(deck, batch, meaningsByCard, paperSize, columns, flipEdge, front, imageFormat);
                    var side = front ? "front" : "back";
                    var name = exportLayout switch
                    {
                        ExportLayout.BackOnly => $"{SafeName(deck.Name)} (Meaning).{extension}",
                        ExportLayout.FrontOnly => $"{SafeName(deck.Name)}.{extension}",
                        _ => $"{SafeName(deck.Name)}-{side}-{batchNumber:000}.{extension}"
                    };
                    var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    entryStream.Write(data);
                }
            }
        }

        return new ExportArtifact(exportLayout switch
        {
            ExportLayout.BackOnly => $"{SafeName(deck.Name)} (Meaning).{extension}-sheets.zip",
            ExportLayout.FrontOnly => $"{SafeName(deck.Name)}.{extension}-sheets.zip",
            _ => $"{SafeName(deck.Name)}-{extension}-sheets.zip"
        }, output.ToArray());
    }

    private static byte[] RenderRasterSheet(Deck deck, IReadOnlyList<Flashcard> cards,
        IReadOnlyDictionary<int, IReadOnlyList<CardMeaning>> meaningsByCard,
        PaperSize paperSize, int columns, DuplexFlipEdge flipEdge, bool front, SKEncodedImageFormat imageFormat)
    {
        var widthPoints = paperSize == PaperSize.A4 ? 595.28f : 612f;
        var heightPoints = paperSize == PaperSize.A4 ? 841.89f : 792f;
        var scale = RasterDpi / 72f;
        var width = (int)Math.Round(widthPoints * scale);
        var height = (int)Math.Round(heightPoints * scale);
        var margin = (int)Math.Round(PageMarginPoints * scale);
        var gap = 12;
        var cellWidth = (width - 2 * margin - (columns - 1) * gap) / (float)columns;
        var cellHeight = (height - 2 * margin - (columns - 1) * gap) / (float)columns;

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var borderPaint = new SKPaint { Color = SKColors.Black, StrokeWidth = 3, Style = SKPaintStyle.Stroke, IsAntialias = true };
        using var japaneseTypeface = SKTypeface.FromFamilyName("MS Gothic") ?? SKTypeface.Default;
        using var titleTypeface = SKTypeface.FromFamilyName("Times New Roman") ?? SKTypeface.Default;
        using var textFont = new SKFont(japaneseTypeface, 1);
        using var accentFont = new SKFont(titleTypeface, 1);
        using var textPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        using var accentPaint = new SKPaint { Color = new SKColor(31, 111, 80), IsAntialias = true };
        using var underlinePaint = new SKPaint { Color = SKColors.Black, StrokeWidth = 2, Style = SKPaintStyle.Stroke, IsAntialias = true };

        for (var row = 0; row < columns; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var index = GetCardIndex(row, column, columns, flipEdge, front);
                var left = margin + column * (cellWidth + gap);
                var top = margin + row * (cellHeight + gap);
                var rect = new SKRect(left, top, left + cellWidth, top + cellHeight);
                canvas.DrawRect(rect, borderPaint);
                if (index >= cards.Count)
                    continue;

                meaningsByCard.TryGetValue(cards[index].Id, out var meanings);
                DrawRasterCard(canvas, rect, deck, cards[index], meanings ?? Array.Empty<CardMeaning>(), front,
                    textFont, textPaint, accentFont, accentPaint, underlinePaint, japaneseTypeface, titleTypeface);
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(imageFormat, 95);
        return encoded.ToArray();
    }

    private static void DrawRasterCard(SKCanvas canvas, SKRect rect, Deck deck, Flashcard card,
        IReadOnlyList<CardMeaning> meanings, bool front, SKFont textFont, SKPaint textPaint,
        SKFont accentFont, SKPaint accentPaint, SKPaint underlinePaint,
        SKTypeface japaneseTypeface, SKTypeface titleTypeface)
    {
        var padding = rect.Width * 0.06f;
        var left = rect.Left + padding;
        var right = rect.Right - padding;
        if (front)
        {
            accentFont.Size = rect.Width * 0.035f;
            accentFont.Embolden = true;
            accentPaint.Color = new SKColor(198, 40, 40);
            canvas.DrawText($"{deck.Name.ToUpperInvariant()} {card.Number:000}", right,
                rect.Top + padding + accentFont.Size, SKTextAlign.Right, accentFont, accentPaint);
            accentFont.Embolden = false;
            accentPaint.Color = new SKColor(31, 111, 80);
            textFont.Size = Math.Clamp(rect.Width * 0.15f, 44, 110);
            textFont.Typeface = japaneseTypeface;
            accentFont.Size = textFont.Size;
            accentFont.Typeface = titleTypeface;
            DrawCenteredMixed(canvas, card.FrontText, (left + right) / 2,
                (rect.Top + rect.Bottom) / 2, textFont, accentFont, textPaint);
            return;
        }

        accentFont.Size = rect.Width * 0.04f;
        accentPaint.Color = GetRasterBackMetaColor(deck.CardType, card);
        canvas.DrawText(GetBackMeta(deck.CardType, card), left, rect.Top + padding + accentFont.Size,
            SKTextAlign.Left, accentFont, accentPaint);
        accentPaint.Color = new SKColor(31, 111, 80);
        textFont.Typeface = japaneseTypeface;
        textFont.Size = rect.Width * 0.085f;
        accentFont.Typeface = titleTypeface;
        accentFont.Size = textFont.Size;
        var baseline = rect.Top + padding + textFont.Size * 2.8f;
        textPaint.Color = new SKColor(198, 40, 40);
        var cardNumber = $"{card.Number}.  ";
        canvas.DrawText(cardNumber, left, baseline, SKTextAlign.Left, accentFont, textPaint);
        canvas.DrawText(card.Hiragana, left + accentFont.MeasureText(cardNumber), baseline,
            SKTextAlign.Left, textFont, textPaint);
        textPaint.Color = SKColors.Black;
        textFont.Typeface = titleTypeface;
        textFont.Size = rect.Width * 0.045f;
        baseline += textFont.Size * 2.2f;
        foreach (var meaning in meanings.Where(item => !string.IsNullOrWhiteSpace(item.Text)))
        {
            textFont.Embolden = meaning.Style is MeaningStyle.Bold or MeaningStyle.BoldUnderline;
            var text = $"•  {meaning.Text}";
            canvas.DrawText(text, left, baseline, SKTextAlign.Left, textFont, textPaint);
            if (meaning.Style is MeaningStyle.Underline or MeaningStyle.BoldUnderline)
                canvas.DrawLine(left, baseline + 4, left + textFont.MeasureText(text), baseline + 4, underlinePaint);
            baseline += textFont.Size * 1.65f;
            if (baseline > rect.Bottom - padding)
                break;
        }
        textFont.Embolden = false;
    }

    private static void DrawCentered(SKCanvas canvas, string text, float centerX, float baseline, SKFont font, SKPaint paint)
    {
        canvas.DrawText(text, centerX, baseline + font.Size / 3, SKTextAlign.Center, font, paint);
    }

    private static void DrawCenteredMixed(SKCanvas canvas, string text, float centerX, float baseline,
        SKFont japaneseFont, SKFont titleFont, SKPaint paint)
    {
        var runs = SplitByScript(text).ToArray();
        var width = runs.Sum(run => (run.IsJapanese ? japaneseFont : titleFont).MeasureText(run.Text));
        var x = centerX - width / 2;

        foreach (var run in runs)
        {
            var font = run.IsJapanese ? japaneseFont : titleFont;
            canvas.DrawText(run.Text, x, baseline + font.Size / 3, SKTextAlign.Left, font, paint);
            x += font.MeasureText(run.Text);
        }
    }

    private static IEnumerable<(string Text, bool IsJapanese)> SplitByScript(string text)
    {
        if (string.IsNullOrEmpty(text))
            yield break;

        var start = 0;
        var isJapanese = IsJapaneseCharacter(text[0]);
        for (var index = 1; index < text.Length; index++)
        {
            var nextIsJapanese = IsJapaneseCharacter(text[index]);
            if (nextIsJapanese == isJapanese)
                continue;

            yield return (text[start..index], isJapanese);
            start = index;
            isJapanese = nextIsJapanese;
        }

        yield return (text[start..], isJapanese);
    }

    private static bool IsJapaneseCharacter(char character) =>
        character is >= '\u2E80' and <= '\u9FFF' or >= '\uF900' and <= '\uFAFF' or >= '\u3040' and <= '\u31FF';

    private static int GetCardIndex(int row, int column, int columns, DuplexFlipEdge flipEdge, bool front)
    {
        // Both front and back pages follow the same left-to-right, top-to-bottom ordering.
        // This keeps sheet layouts consistent when the cards are printed or exported as a stand-alone deck.
        return row * columns + column;
    }

    private static string GetBackMeta(CardType cardType, Flashcard card) => cardType switch
    {
        CardType.Verb => $"{card.VerbKind?.ToString().ToLowerInvariant() ?? "verb"} verb, {card.Transitivity?.ToString().ToLowerInvariant() ?? ""} verb".Trim(' ', ','),
        CardType.Adverb => $"{card.AdverbClassification?.ToString().ToLowerInvariant() ?? ""} adverb".Trim(),
        CardType.Noun => $"{card.NounCategory?.ToString().ToLowerInvariant() ?? ""} noun".Trim(),
        CardType.IAdjective => "i-adjective",
        CardType.NaAdjective => "na-adjective",
        _ => cardType.ToString().ToLowerInvariant()
    };

    private static SKColor GetRasterBackMetaColor(CardType cardType, Flashcard card) => cardType switch
    {
        CardType.Verb => card.VerbKind switch
        {
            VerbKind.Godan => new SKColor(21, 101, 192),
            VerbKind.Ichidan => new SKColor(46, 125, 50),
            VerbKind.Irregular => new SKColor(123, 31, 162),
            _ => new SKColor(198, 40, 40)
        },
        CardType.Adverb => card.AdverbClassification switch
        {
            AdverbClassification.Positive => new SKColor(46, 125, 50),
            AdverbClassification.Negative => new SKColor(198, 40, 40),
            AdverbClassification.Neutral => new SKColor(123, 31, 162),
            _ => new SKColor(198, 40, 40)
        },
        _ => new SKColor(198, 40, 40)
    };

    private static string SafeName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "draconia-cards" : safe;
    }
}
