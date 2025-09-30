// Copyright (c) Microsoft Corporation. All rights reserved.

using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Presentation;
using HtmlAgilityPack;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor; // For content-order text fallback
using Docnet.Core; // PDFium rendering for OCR fallback
using Docnet.Core.Models;
using SixLabors.ImageSharp; // Image encoding for OCR
using SixLabors.ImageSharp.PixelFormats;
using Tesseract; // OCR engine

namespace Microsoft.Greenlight.Shared.Services.Search;

/// <summary>
/// Enhanced text extraction service with support for PDF and Office documents.
/// Extends BasicTextExtractionService with additional file format support.
/// </summary>
public class EnhancedTextExtractionService : ITextExtractionService
{
    private readonly ILogger<EnhancedTextExtractionService> _logger;
    private readonly IOptionsSnapshot<ServiceConfigurationOptions> _options;
    private readonly TesseractLanguageManager _languageManager;

    public EnhancedTextExtractionService(
        ILogger<EnhancedTextExtractionService> logger,
        IOptionsSnapshot<ServiceConfigurationOptions> options,
        TesseractLanguageManager languageManager)
    {
        _logger = logger;
        _options = options;
        _languageManager = languageManager;
    }

    /// <inheritdoc />
    public async Task<string> ExtractTextAsync(Stream fileStream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        switch (extension)
        {
            case ".txt":
            case ".md":
            case ".csv":
            case ".metadata":
                return await ExtractPlainTextAsync(fileStream);

            case ".html":
            case ".htm":
                return await ExtractHtmlTextAsync(fileStream);

            case ".pdf":
                return await ExtractPdfTextAsync(fileStream, fileName);

            case ".docx":
                return await ExtractDocxTextAsync(fileStream, fileName);

            case ".xlsx":
                return await ExtractExcelTextAsync(fileStream, fileName);

            case ".pptx":
                return await ExtractPowerPointTextAsync(fileStream, fileName);

            default:
                _logger.LogWarning("Unsupported file type {Extension} for file {FileName}", extension, fileName);
                return string.Empty;
        }
    }

    /// <inheritdoc />
    public bool SupportsFileType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".txt" or ".md" or ".csv" or ".metadata" or ".html" or ".htm" or ".pdf" or ".docx" or ".xlsx" or ".pptx" => true,
            _ => false
        };
    }

    private async Task<string> ExtractPlainTextAsync(Stream fileStream)
    {
        using var reader = new StreamReader(fileStream, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private async Task<string> ExtractHtmlTextAsync(Stream fileStream)
    {
        var html = await ExtractPlainTextAsync(fileStream);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var sb = new StringBuilder();
        foreach (var node in doc.DocumentNode.SelectNodes("//text()") ?? Enumerable.Empty<HtmlNode>())
        {
            var inner = node.InnerText;
            if (!string.IsNullOrWhiteSpace(inner)) { sb.AppendLine(inner.Trim()); }
        }
        var text = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ");
        return text.Trim();
    }

    private async Task<string> ExtractPdfTextAsync(Stream fileStream, string fileName)
    {
        try
        {
            // Ensure seekable stream and capture bytes for OCR fallback
            MemoryStream workingStream;
            if (!fileStream.CanSeek)
            {
                workingStream = new MemoryStream();
                await fileStream.CopyToAsync(workingStream);
                workingStream.Position = 0;
            }
            else if (fileStream is MemoryStream ms)
            {
                workingStream = ms;
                workingStream.Position = 0;
            }
            else
            {
                workingStream = new MemoryStream();
                fileStream.Position = 0;
                await fileStream.CopyToAsync(workingStream);
                workingStream.Position = 0;
            }

            var pdfBytes = workingStream.ToArray();

            var parsingOptions = new ParsingOptions
            {
                ClipPaths = true,
                UseLenientParsing = true
            };

            var sb = new StringBuilder();

            using (var document = PdfDocument.Open(new MemoryStream(pdfBytes, writable: false), parsingOptions))
            {
                // Iterate pages one-by-one so a single bad page doesn't break the whole document
                for (int pageNumber = 1; pageNumber <= document.NumberOfPages; pageNumber++)
                {
                    string? pageText = null;
                    try
                    {
                        var page = document.GetPage(pageNumber);

                        // Primary text
                        pageText = page.Text;

                        // Fallback: content-order extractor
                        if (string.IsNullOrWhiteSpace(pageText))
                        {
                            try
                            {
                                var extracted = ContentOrderTextExtractor.GetText(page);
                                if (!string.IsNullOrWhiteSpace(extracted))
                                {
                                    pageText = extracted;
                                }
                            }
                            catch (Exception inner)
                            {
                                _logger.LogDebug(inner, "ContentOrderTextExtractor failed on page {PageNumber} for PDF {FileName}", pageNumber, fileName);
                            }
                        }
                    }
                    catch (Exception pageEx)
                    {
                        _logger.LogWarning(pageEx, "Skipping direct parse of page {PageNumber} while extracting PDF {FileName} due to parsing error.", pageNumber, fileName);
                    }

                    // OCR fallback: render with PDFium (Docnet.Core) and OCR with Tesseract if nothing found
                    if (string.IsNullOrWhiteSpace(pageText))
                    {
                        try
                        {
                            // Collect languages from configuration
                            var langs = _options.Value.GreenlightServices.DocumentIngestion.Ocr.DefaultLanguages;
                            await _languageManager.EnsureLanguagesAvailableAsync(langs);
                            var languageCombined = string.Join('+', langs);

                            var ocrText = TryOcrPdfPage(pdfBytes, pageNumber, _languageManager.GetTessdataPath(), languageCombined);
                            if (!string.IsNullOrWhiteSpace(ocrText))
                            {
                                pageText = ocrText;
                            }
                        }
                        catch (Exception ocrEx)
                        {
                            _logger.LogDebug(ocrEx, "OCR fallback failed on page {PageNumber} for PDF {FileName}", pageNumber, fileName);
                        }
                    }

                    // Append page text (if any)
                    if (!string.IsNullOrWhiteSpace(pageText))
                    {
                        sb.AppendLine(pageText);
                    }

                    // Insert an explicit page break delimiter so downstream ingestion can detect page boundaries reliably.
                    // Use form-feed (\f) which is searched by the vector store ingestion to compute SourceDocumentSourcePage.
                    if (pageNumber < document.NumberOfPages)
                    {
                        sb.Append('\f');
                        sb.AppendLine();
                    }
                }
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from PDF file {FileName}", fileName);
            return string.Empty;
        }
    }

    // OCR fallback implementation using Docnet.Core + ImageSharp + Tesseract
    private static string? TryOcrPdfPage(byte[] pdfBytes, int pageNumber, string tessdataPath, string languages)
    {
        try
        {
            using var docReader = DocLib.Instance.GetDocReader(pdfBytes, new PageDimensions(1700, 2200));
            using var pageReader = docReader.GetPageReader(pageNumber - 1);

            var rawBytes = pageReader.GetImage();
            int width = pageReader.GetPageWidth();
            int height = pageReader.GetPageHeight();

            using var image = Image.LoadPixelData<Bgra32>(rawBytes, width, height);
            using var msPng = new MemoryStream();
            image.SaveAsPng(msPng);
            var pngBytes = msPng.ToArray();

            using var engine = new TesseractEngine(tessdataPath, languages, EngineMode.LstmOnly);
            using var pix = Pix.LoadFromMemory(pngBytes);
            using var page = engine.Process(pix);
            var text = page.GetText();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string> ExtractDocxTextAsync(Stream fileStream, string fileName)
    {
        try
        {
            if (!fileStream.CanSeek)
            {
                using var ms = new MemoryStream();
                await fileStream.CopyToAsync(ms);
                ms.Position = 0;
                fileStream = ms;
            }
            fileStream.Position = 0;
            using var wordDoc = WordprocessingDocument.Open(fileStream, false);
            var body = wordDoc.MainDocumentPart?.Document?.Body;
            return body?.InnerText ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from DOCX file {FileName}", fileName);
            return string.Empty;
        }
    }

    private async Task<string> ExtractExcelTextAsync(Stream fileStream, string fileName)
    {
        try
        {
            if (!fileStream.CanSeek)
            {
                using var ms = new MemoryStream();
                await fileStream.CopyToAsync(ms);
                ms.Position = 0;
                fileStream = ms;
            }
            fileStream.Position = 0;
            using var spreadsheet = SpreadsheetDocument.Open(fileStream, false);
            var sb = new StringBuilder();
            foreach (var sheet in spreadsheet.WorkbookPart!.Workbook.Sheets!.OfType<Sheet>())
            {
                var worksheetPart = (WorksheetPart)spreadsheet.WorkbookPart.GetPartById(sheet.Id!);
                var rows = worksheetPart.Worksheet.Descendants<Row>();
                foreach (var row in rows)
                {
                    var cells = row.Elements<Cell>();
                    foreach (var cell in cells)
                    {
                        var value = GetCellValue(spreadsheet, cell);
                        if (!string.IsNullOrWhiteSpace(value)) { sb.Append(value).Append('\t'); }
                    }
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from Excel file {FileName}", fileName);
            return string.Empty;
        }
    }

    private async Task<string> ExtractPowerPointTextAsync(Stream fileStream, string fileName)
    {
        try
        {
            if (!fileStream.CanSeek)
            {
                using var ms = new MemoryStream();
                await fileStream.CopyToAsync(ms);
                ms.Position = 0;
                fileStream = ms;
            }
            fileStream.Position = 0;
            using var ppt = PresentationDocument.Open(fileStream, false);
            var sb = new StringBuilder();
            var presentation = ppt.PresentationPart?.Presentation;
            if (presentation?.SlideIdList != null)
            {
                foreach (var slideId in presentation.SlideIdList.OfType<SlideId>())
                {
                    var relId = slideId.RelationshipId?.Value;
                    if (string.IsNullOrEmpty(relId)) { continue; }
                    var slidePart = (SlidePart)ppt.PresentationPart!.GetPartById(relId);
                    var texts = slidePart.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Text>();
                    foreach (var t in texts)
                    {
                        if (!string.IsNullOrWhiteSpace(t.Text)) { sb.AppendLine(t.Text.Trim()); }
                    }
                }
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from PowerPoint file {FileName}", fileName);
            return string.Empty;
        }
    }

    private static string GetCellValue(SpreadsheetDocument doc, Cell cell)
    {
        var value = cell.CellValue?.InnerText ?? string.Empty;
        if (cell.DataType == null) { return value; }
        if (cell.DataType.Value == CellValues.SharedString)
        {
            var stringTable = doc.WorkbookPart?.SharedStringTablePart?.SharedStringTable;
            if (stringTable != null && int.TryParse(value, out var idx) && idx < stringTable.ChildElements.Count)
            {
                return stringTable.ChildElements[idx].InnerText;
            }
        }
        return value;
    }
}
