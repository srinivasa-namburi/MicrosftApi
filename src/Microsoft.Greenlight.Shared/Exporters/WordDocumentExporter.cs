using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HtmlAgilityPack;
using Markdig;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Web.Shared.Helpers;
using System.Text.RegularExpressions;
using System.Web;
using Table = DocumentFormat.OpenXml.Wordprocessing.Table;
using TableCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;

namespace Microsoft.Greenlight.Shared.Exporters;

public class WordDocumentExporter : IDocumentExporter
{
    private DocGenerationDbContext _dbContext { get; }
    private int _numberingIdCounter = 3; // Starts from 3 as 1 is reserved for bullets and 2 for headers
    private Dictionary<int, int> _numberingLevelCounters = new Dictionary<int, int>();
    private bool _documentHeaderHasNumbering = false;

    public WordDocumentExporter(DocGenerationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Stream?> ExportDocumentAsync(Guid generatedDocumentId)
    {
        var document = await _dbContext.GeneratedDocuments
            .Include(gd => gd.Metadata)
            .Include(w => w.ContentNodes)
            .ThenInclude(r => r.Children)
            .ThenInclude(s => s.Children)
            .ThenInclude(t => t.Children)
            .ThenInclude(u => u.Children)
            .ThenInclude(v => v.Children)
            .ThenInclude(w => w.Children)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(d => d.Id == generatedDocumentId);

        if (document == null)
        {
            return null;
        }

        var titleNumberingRegex = new Regex(IDocumentExporter.TitleNumberingRegex);

        var documentHasNumbering = _dbContext.ContentNodes
            .AsNoTracking()
            .AsSplitQuery()
            .Where(cn => cn.GeneratedDocumentId == generatedDocumentId && cn.Type != Shared.Enums.ContentNodeType.BodyText)
            .Join(_dbContext.ContentNodes.Where(cn => cn.Type != Shared.Enums.ContentNodeType.BodyText), cn1 => cn1.Id, cn2 => cn2.ParentId, (cn1, cn2) => cn2)
            .ToList()
            .All(cn => titleNumberingRegex.IsMatch(cn.Text));

        return await ExportDocumentAsync(document, documentHasNumbering);
    }

    public async Task<Stream> ExportDocumentAsync(GeneratedDocument generatedDocument, bool documentHeaderHasNumbering)
    {
        _documentHeaderHasNumbering = documentHeaderHasNumbering;

        var stream = new MemoryStream();
        using (var wordDocument = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = wordDocument.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            AddStylesToDocument(wordDocument);
            AddNumberingDefinitions(wordDocument);

            // Todo: content of header and footer should be dynamic. It is semi-static now.
            AddHeaderAndFooterToDocument(mainPart, generatedDocument.Title, "Revision 1");

            ContentNodeSorter.SortContentNodes(generatedDocument.ContentNodes);

            foreach (var contentNode in generatedDocument.ContentNodes)
            {
                AppendContentNode(body, contentNode, 0);
            }

            mainPart.Document.Save();

            AddTableOfContents(wordDocument);
        }

        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }

    private void AppendContentNode(Body body, ContentNode contentNode, int level)
    {
        if (_documentHeaderHasNumbering && (contentNode.Type == ContentNodeType.Title || contentNode.Type == ContentNodeType.Heading))
        {
            var titleNumberingRegex = new Regex(IDocumentExporter.TitleNumberingRegex);
            var match = titleNumberingRegex.Match(contentNode.Text);

            if (match.Success && match.Groups.Count > 0)
            {
                contentNode.Text = match.Groups[1].Value;
            }
        }

        switch (contentNode.Type)
        {
            case ContentNodeType.Title:
                {
                    body.Append(ConvertHeader(contentNode.Text, "h1", level));
                    break;
                }
            case ContentNodeType.Heading:
                {
                    body.Append(ConvertHeader(contentNode.Text, "h2", level));
                    break;
                }
            default:
                {
                    var html = ConvertMarkdownToHtml(contentNode.Text);
                    var elements = ConvertHtmlToOpenXmlElements(html);

                    foreach (var element in elements)
                    {
                        body.Append(element);
                    }

                    break;
                }
        }

        foreach (var childNode in contentNode.Children)
        {
            AppendContentNode(body, childNode, level + 1);
        }
    }

    private List<OpenXmlElement> ConvertHtmlToOpenXmlElements(string html)
    {
        var elements = new List<OpenXmlElement>();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        for (int i = 0; i < doc.DocumentNode.ChildNodes.Count; i++)
        {
            var node = doc.DocumentNode.ChildNodes[i];
            switch (node.Name)
            {
                case "p":
                    elements.Add(ConvertParagraph(node));
                    break;
                case "table":
                    elements.Add(ConvertTable(node));
                    break;
                case "ul":
                    elements.AddRange(ConvertList(node, false, 0));
                    break;
                case "ol":
                    elements.AddRange(ConvertList(node, true, 0));
                    break;
                case "img":
                    elements.Add(ConvertImage(node));
                    break;
                case "h1":
                case "h2":
                case "h3":
                    elements.Add(ConvertHeader(node.InnerText, node.Name));
                    break;
                default:
                    elements.Add(ConvertParagraph(node));
                    break;
            }
        }
        return elements;
    }

    private Paragraph ConvertParagraph(HtmlNode node)
    {
        var paragraph = new Paragraph();
        var run = new Run();
        run.Append(new Text(node.InnerText));

        // Add paragraph properties
        var paragraphProperties = new ParagraphProperties();

        // Set exact line spacing with no additional space between paragraphs
        paragraphProperties.Append(new SpacingBetweenLines()
        {
            After = "0",  // No additional space after the paragraph
            Before = "0", // No additional space before the paragraph
            Line = "240", // Single line spacing (can adjust for tighter or looser spacing)
            LineRule = LineSpacingRuleValues.Auto
        });

        paragraph.Append(paragraphProperties);
        paragraph.Append(run);

        return paragraph;
    }

    private Drawing ConvertImage(HtmlNode node)
    {
        var imageUrl = node.GetAttributeValue("src", null);
        if (imageUrl == null)
        {
            return null;
        }

        var drawing = new Drawing();
        return drawing;
    }

    private Table ConvertTable(HtmlNode node)
    {
        var table = new Table();

        var tablesProperty = new TableProperties(
            new TableBorders(
                new TopBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 16 },
                new BottomBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 16 },
                new LeftBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 16 },
                new RightBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 16 },
                new InsideHorizontalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 16 },
                new InsideVerticalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 16 }
            ));

        table.AppendChild(tablesProperty);

        foreach (var rowNode in node.SelectNodes(".//tr"))
        {
            var tableRow = new TableRow();
            foreach (var cellNode in rowNode.SelectNodes(".//td|.//th"))
            {
                var tableCell = new TableCell();

                var tableCellProperties = new TableCellProperties(
                    new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }
                );
                tableCell.AppendChild(tableCellProperties);

                var paragraph = new Paragraph(new Run(new Text(cellNode.InnerText)));
                var paragraphProperties = new ParagraphProperties
                {
                    Indentation = new Indentation { Left = "113" }
                };

                paragraph.AddChild(paragraphProperties);

                tableCell.Append(paragraph);
                tableRow.Append(tableCell);
            }
            table.Append(tableRow);
        }

        return table;
    }

    private Paragraph ConvertHeader(string nodeText, string nodeName, int? level = null)
    {
        var paragraph = new Paragraph();
        var run = new Run();
        run.Append(new Text(nodeText));

        var paragraphProperties = new ParagraphProperties();
        var headerRank = nodeName[1];
        var headingStyle = new ParagraphStyleId() { Val = $"Heading{headerRank}" };

        if (level is not null)
        {
            if (_documentHeaderHasNumbering)
            {
                paragraphProperties.NumberingProperties =
                    new NumberingProperties(
                        new NumberingLevelReference() { Val = level },
                        new NumberingId() { Val = 2 }
                    );
            }

            headingStyle.Val = $"NumberingHeading{headerRank}";
        }

        paragraphProperties.Append(headingStyle);
        paragraph.Append(paragraphProperties);
        paragraph.Append(run);

        return paragraph;
    }

    private List<OpenXmlElement> ConvertList(HtmlNode node, bool isOrdered, int level)
    {
        var elements = new List<OpenXmlElement>();
        int numberingId = isOrdered ? GetNewNumberingId() : 1; // New numbering ID for each ordered list

        foreach (var liNode in node.ChildNodes.Where(n => n.Name == "li"))
        {
            var listItem = new Paragraph(new Run(new Text(liNode.InnerText.Trim())));

            // Add numbering properties
            var numberingProperties = new NumberingProperties(
                new NumberingLevelReference() { Val = level },
                new NumberingId() { Val = numberingId }
            );

            // Add paragraph properties for hanging indentation
            var paragraphProperties = new ParagraphProperties(numberingProperties);

            // Set indentation properties for hanging indent
            paragraphProperties.Append(new Indentation
            {
                Left = "720",   // Adjust for the level of indentation for the bullet or number (720 = 0.5 inch)
                Hanging = "360" // Adjust for hanging indent (360 = 0.25 inch)
            });

            // Apply the updated paragraph properties
            listItem.ParagraphProperties = paragraphProperties;
            elements.Add(listItem);

            // Handle nested lists
            var nestedListNode = liNode.SelectSingleNode("./ul|./ol");
            if (nestedListNode != null)
            {
                bool nestedIsOrdered = nestedListNode.Name == "ol";
                elements.AddRange(ConvertList(nestedListNode, nestedIsOrdered, level + 1));
            }
        }

        return elements;
    }

    private int GetNewNumberingId()
    {
        return _numberingIdCounter++;
    }

    /// <summary>
    /// Add header and footer to the Word document
    /// </summary>
    /// <param name="mainPart">main document part</param>
    /// <param name="headerText">header text to add</param>
    /// <param name="footerText">footer text to add</param>
    private void AddHeaderAndFooterToDocument(MainDocumentPart mainPart, string headerText, string footerText)
    {
        // Add a header part to the document
        var headerPart = mainPart.HeaderParts.FirstOrDefault();
        if (headerPart == null)
        {
            headerPart = mainPart.AddNewPart<HeaderPart>();
        }

        string headerPartId = mainPart.GetIdOfPart(headerPart);

        // Create the header content
        var header = new Header();
        var headerParagraph = new Paragraph();

        // Align the header text to the right
        ParagraphProperties paragraphProperties = new ParagraphProperties();
        Justification justification = new Justification { Val = JustificationValues.Right };
        paragraphProperties.Append(justification);
        headerParagraph.Append(paragraphProperties);

        // We can add more content to the header here besides the title and subject.
        Run headerRun = new Run();
        headerRun.Append(new Text(headerText));
        headerRun.Append(new Break()); // add a line break
        headerRun.Append(new SimpleField { Instruction = "SUBJECT" });

        headerParagraph.Append(headerRun);
        header.Append(headerParagraph);
        headerPart.Header = header;

        // Add a footer part to the document
        var footerPart = mainPart.FooterParts.FirstOrDefault();
        if (footerPart == null)
        {
            footerPart = mainPart.AddNewPart<FooterPart>();
        }

        string footerPartId = mainPart.GetIdOfPart(footerPart);

        // Create the footer content with a table
        Footer footer = new Footer();
        Table table = new Table();

        // Create table properties. Width = 10000 is from Copilot, meaning 100% page width. Adjust to 60%
        TableProperties tableProperties = new TableProperties(
            new TableWidth { Width = "6000", Type = TableWidthUnitValues.Pct });
        table.AppendChild(tableProperties);

        // Create a table row
        TableRow tableRow = new TableRow();

        // Create left-aligned cell
        TableCell leftCell = new TableCell(new Paragraph(new Run(new Text(footerText))));
        leftCell.Append(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "33%" }));

        // Create center-aligned cell. Add page number field to the footer.
        Run pageNumberRun = new Run();
        pageNumberRun.Append(new FieldChar { FieldCharType = FieldCharValues.Begin });
        pageNumberRun.Append(new FieldCode("PAGE"));
        pageNumberRun.Append(new FieldChar { FieldCharType = FieldCharValues.End });
        pageNumberRun.Append(new Text(" - "));
        pageNumberRun.Append(new FieldChar { FieldCharType = FieldCharValues.Begin });
        pageNumberRun.Append(new FieldCode("NUMPAGES"));
        pageNumberRun.Append(new FieldChar { FieldCharType = FieldCharValues.End });

        TableCell centerCell = new TableCell(new Paragraph(pageNumberRun));
        centerCell.Append(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "33%" }));
        centerCell.Append(new ParagraphProperties(new Justification { Val = JustificationValues.Center }));

        // Create right-aligned cell. Add generated date field
        Run dateRun = new Run();
        dateRun.Append(new FieldChar { FieldCharType = FieldCharValues.Begin });
        dateRun.Append(new FieldCode("DATE"));
        dateRun.Append(new FieldChar { FieldCharType = FieldCharValues.End });
        TableCell rightCell = new TableCell(new Paragraph(dateRun));
        rightCell.Append(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "33%" }));
        rightCell.Append(new ParagraphProperties(new Justification { Val = JustificationValues.Right }));

        // Append cells to the row
        tableRow.Append(leftCell);
        tableRow.Append(centerCell);
        tableRow.Append(rightCell);

        // Append row to the table
        table.Append(tableRow);

        // Append table to the footer
        footer.Append(table);
        footerPart.Footer = footer;

        // Create the header and footer references
        SectionProperties sectionProps = new SectionProperties();
        HeaderReference headerReference = new HeaderReference { Type = HeaderFooterValues.Default, Id = headerPartId };
        FooterReference footerReference = new FooterReference { Type = HeaderFooterValues.Default, Id = footerPartId };
        sectionProps.Append(headerReference);
        sectionProps.Append(footerReference);

        // Add the section properties to the document body
        if (mainPart.Document.Body == null)
        {
            mainPart.Document.Body = new Body();
        }

        mainPart.Document.Body.Append(sectionProps);
    }

    private void AddTableOfContents(WordprocessingDocument wordDoc)
    {
        var tocTitleParagraph = new Paragraph(
       new ParagraphProperties(
           new ParagraphStyleId() { Val = "Heading1" } // Use a heading style for the title
       ),
       new Run(new Text("Table of Contents"))
   );

        // Create a new SdtBlock for the TOC
        var sdtBlock = new SdtBlock(
            new SdtProperties(
                new SdtAlias { Val = "Table of Contents" },
                new Tag { Val = "TableOfContents" }
            ),
            new SdtContentBlock(
                new Paragraph(
                    new Run(
                        new FieldChar { FieldCharType = FieldCharValues.Begin },
                        new FieldCode(" TOC \\t \"Numbering Heading 1,1,Numbering Heading 2,2\" \\h "),
                        new FieldChar { FieldCharType = FieldCharValues.Separate },
                        new Run(new Text("Right-click to update field.")),
                        new FieldChar { FieldCharType = FieldCharValues.End }
                    )
                )
            )
        );

        // Add the TOC title and the TOC to the document body
        var body = wordDoc.MainDocumentPart.Document.Body;
        body.PrependChild(sdtBlock);
        body.PrependChild(tocTitleParagraph);

        var settingsPart = wordDoc.MainDocumentPart.DocumentSettingsPart;
        if (settingsPart == null)
        {
            settingsPart = wordDoc.MainDocumentPart.AddNewPart<DocumentSettingsPart>();
            settingsPart.Settings = new Settings();
        }

        // Add or update the UpdateFieldsOnOpen element
        var updateFieldsOnOpen = settingsPart.Settings.Elements<UpdateFieldsOnOpen>().FirstOrDefault();
        if (updateFieldsOnOpen == null)
        {
            updateFieldsOnOpen = new UpdateFieldsOnOpen { Val = true };
            settingsPart.Settings.Append(updateFieldsOnOpen);
        }
        else
        {
            updateFieldsOnOpen.Val = true;
        }

        settingsPart.Settings.Save();

        wordDoc.MainDocumentPart.Document.Save();
    }

    private void AddNumberingDefinitions(WordprocessingDocument wordDocument)
    {
        var numberingPart = wordDocument.MainDocumentPart!.AddNewPart<NumberingDefinitionsPart>();
        var numbering = new Numbering(
            new AbstractNum(
                new Level(
                    new NumberingFormat() { Val = NumberFormatValues.Bullet },
                    new LevelText() { Val = "•" }
                )
                { LevelIndex = 0 }
            )
            { AbstractNumberId = 1 },
            new AbstractNum(
                new Level(
                    new NumberingFormat() { Val = NumberFormatValues.Decimal },
                    new LevelText() { Val = "%1." }
                )
                { LevelIndex = 0 }
            )
            { AbstractNumberId = 3 },
            new AbstractNum(
                new Level(
                    new NumberingFormat() { Val = NumberFormatValues.Decimal },
                    new LevelText() { Val = "%1.0" },
                    new StartNumberingValue() { Val = 1 }
                )
                { LevelIndex = 0 },
                new Level(
                    new NumberingFormat() { Val = NumberFormatValues.Decimal },
                    new LevelText() { Val = "%1.%2" },
                    new StartNumberingValue() { Val = 1 }
                )
                { LevelIndex = 1 },
                new Level(
                    new NumberingFormat() { Val = NumberFormatValues.Decimal },
                    new LevelText() { Val = "%1.%2.%3" },
                    new StartNumberingValue() { Val = 1 }
                )
                { LevelIndex = 2 },
                new Level(
                    new NumberingFormat() { Val = NumberFormatValues.Decimal },
                    new LevelText() { Val = "%1.%2.%3.%4" },
                    new StartNumberingValue() { Val = 1 }
                )
                { LevelIndex = 3 },
                new Level(
                    new NumberingFormat() { Val = NumberFormatValues.Decimal },
                    new LevelText() { Val = "%1.%2.%3.%4.%5" },
                    new StartNumberingValue() { Val = 1 }
                )
                { LevelIndex = 4 },
                new Level(
                    new NumberingFormat() { Val = NumberFormatValues.Decimal },
                    new LevelText() { Val = "%1.%2.%3.%4.%5.%6" },
                    new StartNumberingValue() { Val = 1 }
                )
                { LevelIndex = 5 },
                new Level(
                    new NumberingFormat() { Val = NumberFormatValues.Decimal },
                    new LevelText() { Val = "%1.%2.%3.%4.%5.%6.%7" },
                    new StartNumberingValue() { Val = 1 }
                )
                { LevelIndex = 6 },
                new Level(
                    new NumberingFormat() { Val = NumberFormatValues.Decimal },
                    new LevelText() { Val = "%1.%2.%3.%4.%5.%6.%7.%8" },
                    new StartNumberingValue() { Val = 1 }
                )
                { LevelIndex = 7 }
            )
            { AbstractNumberId = 2 }
        );

        // Add a NumberingInstance for bullets
        numbering.AppendChild(new NumberingInstance(
            new AbstractNumId() { Val = 1 }
        )
        { NumberID = 1 });

        // Add NumberingInstances for ordered lists
        for (int i = 3; i < _numberingIdCounter; i++)
        {
            numbering.AppendChild(new NumberingInstance(
                new AbstractNumId() { Val = 3 }
            )
            { NumberID = i });
        }

        // Add NumberingInstances for headers
        numbering.AppendChild(new NumberingInstance(
            new AbstractNumId() { Val = 2 }
        )
        { NumberID = 2 });

        numberingPart.Numbering = numbering;
    }

    private void AddStylesToDocument(WordprocessingDocument wordDocument)
    {
        var stylesPart = wordDocument.MainDocumentPart.AddNewPart<StyleDefinitionsPart>();
        var styles = new Styles();

        // Title style (with light blue color)
        var titleStyle = new Style()
        {
            StyleId = "Title",
            CustomStyle = true,
            Type = StyleValues.Paragraph,
            StyleName = new StyleName() { Val = "Title" }
        };
        titleStyle.Append(new StyleRunProperties(
            new Color() { Val = "ADD8E6" }, // Light Blue
            new FontSize() { Val = "40" }  // 20*2 = 40 half-points for 20px
        ));
        styles.Append(titleStyle);

        // Numbering Heading 1 style (30px, light blue)
        var numberingHeading1Style = new Style()
        {
            StyleId = "NumberingHeading1",
            CustomStyle = true,
            Type = StyleValues.Paragraph,
            StyleName = new StyleName() { Val = "Numbering Heading 1" }
        };
        numberingHeading1Style.Append(new StyleRunProperties(
            new Color() { Val = "ADD8E6" }, // Light Blue
            new FontSize() { Val = "60" }  // 30px
        ));
        styles.Append(numberingHeading1Style);

        // Numbering Heading 2 style (18px, light blue)
        var numberingHeading2Style = new Style()
        {
            StyleId = "NumberingHeading2",
            CustomStyle = true,
            Type = StyleValues.Paragraph,
            StyleName = new StyleName() { Val = "Numbering Heading 2" }
        };
        numberingHeading2Style.Append(new StyleRunProperties(
            new Color() { Val = "ADD8E6" }, // Light Blue
            new FontSize() { Val = "36" }  // 18px
        ));
        styles.Append(numberingHeading2Style);

        // Heading 1 style (30px, light blue)
        var heading1Style = new Style()
        {
            StyleId = "Heading1",
            CustomStyle = true,
            Type = StyleValues.Paragraph,
            StyleName = new StyleName() { Val = "Heading 1" }
        };
        heading1Style.Append(new StyleRunProperties(
            new Color() { Val = "ADD8E6" }, // Light Blue
            new FontSize() { Val = "60" }  // 30px
        ));
        styles.Append(heading1Style);

        // Heading 2 style (18px, light blue)
        var heading2Style = new Style()
        {
            StyleId = "Heading2",
            CustomStyle = true,
            Type = StyleValues.Paragraph,
            StyleName = new StyleName() { Val = "Heading 2" }
        };
        heading2Style.Append(new StyleRunProperties(
            new Color() { Val = "ADD8E6" }, // Light Blue
            new FontSize() { Val = "36" }  // 18px
        ));
        styles.Append(heading2Style);

        // Heading 3 style (16px, light blue)
        var heading3Style = new Style()
        {
            StyleId = "Heading3",
            CustomStyle = true,
            Type = StyleValues.Paragraph,
            StyleName = new StyleName() { Val = "Heading 3" }
        };
        heading3Style.Append(new StyleRunProperties(
            new Color() { Val = "ADD8E6" }, // Light Blue
            new FontSize() { Val = "32" }  // 16px
        ));
        styles.Append(heading3Style);

        // Append the styles to the document
        stylesPart.Styles = styles;
    }

    private string ConvertMarkdownToHtml(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder()
                        .UseAdvancedExtensions() // Enable tables, lists, and other advanced features
                        .Build();

        return HttpUtility.HtmlDecode(Markdown.ToHtml(markdown, pipeline));
    }
}
