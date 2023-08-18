using LicenseCopilot.Framework.Model;
using Microsoft.SemanticKernel;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

/// <summary>
/// This class will take the path to a given PDF and split it 
/// </summary>
internal class PdfProcessor
{
    private readonly IKernel _kernel;

    public PdfProcessor(IKernel sk)
    {
        _kernel = sk;
    }

    internal async Task<DocumentSection> LoadPage(string filePath, int pageNumber)
    {
        if (pageNumber < 1)
        {
            throw new ArgumentException("Invalid page number. Must be greater than zero.");
        }        

        using (var pdfDocument = PdfDocument.Open(filePath))
        {
            if(pageNumber > pdfDocument.NumberOfPages)
            {
                throw new ArgumentException("Page number must be less than the size of the total number of pages in the document");
            }

            var page = pdfDocument.GetPage(pageNumber);
            
            return new DocumentSection
            {
                Text = ContentOrderTextExtractor.GetText(page)
            };
        }
    }

    
    internal async Task<IEnumerable<DocumentSection>> Load(string filePath)
    {
        throw new Exception("This doesn't work as expected");

        var toReturn = new Dictionary<string, DocumentSection>();

        //naieve implementation that assumes section headers of this format:
        //1.3 PURPOSE AND NEED FOR THE PROPOSED ACTION

        //also assumes an index page which will list all of these items the first time so we will need to skip the index
        using (var pdfDocument = PdfDocument.Open(filePath))
        {
            var documentIndex = new HashSet<string>();
            
            string pattern = @"\d+\.\d+\s+.+";

            var allPages = pdfDocument.GetPages();

            foreach (var currentPage in allPages)
            {
                var allTextOnPage = ContentOrderTextExtractor.GetText(currentPage);

                var matches = Regex.Matches(allTextOnPage, pattern);

                foreach (Match match in matches!)
                {
                    if(documentIndex.Contains(match.Value))
                    {
                        //this is not the index reference, this is the actual section, store it
                        //take the beginning index and all the text from there up until this point
                        if(!toReturn.ContainsKey(match.Value))
                        {
                            toReturn.Add(match.Value, 
                                new DocumentSection 
                                {
                                    Text = "",
                                    StartIndexOnPage = match.Index, StartPage = currentPage.Number 
                                });
                        }
                        else
                        {
                            //we already know the start, this is now implied to be the end
                            var currentDocument = toReturn[match.Value];

                            if (currentPage.Number == currentDocument.StartPage)
                            {
                                var endIndex = match.Index - 1;
                                currentDocument.Text = allTextOnPage.Substring(currentDocument.StartIndexOnPage, endIndex - currentDocument.StartIndexOnPage);
                            }
                            else
                            {
                                //go back to the start page
                                var rewoundPage = allPages.ElementAt(currentDocument.StartPage - 1);
                                
                                var sb = new StringBuilder();

                                //we're definitely on multiple pages, so take the entirety of the first page from the start index
                                sb.Append(ContentOrderTextExtractor.GetText(rewoundPage).Substring(currentDocument.StartIndexOnPage));

                                while (rewoundPage.Number < currentPage.Number)
                                {
                                    //move to next page
                                    rewoundPage = allPages.ElementAt(rewoundPage.Number + 1);

                                    var textOnRewoundPage = ContentOrderTextExtractor.GetText(rewoundPage);

                                    if (rewoundPage != currentPage)
                                    {
                                        sb.Append(textOnRewoundPage);
                                    }
                                    else
                                    {
                                        //take into consideration the regex index
                                        var endIndex = match.Index - 1;
                                        sb.Append(allTextOnPage.Substring(currentDocument.StartIndexOnPage, endIndex - currentDocument.StartIndexOnPage));
                                    }
                                }

                                currentDocument.Text = sb.ToString();
                            }
                            
                        }
                    }
                    else
                    {
                        documentIndex.Add(match.Value);
                    }
                }
            }
        }

        return toReturn.Values;
    }
}
