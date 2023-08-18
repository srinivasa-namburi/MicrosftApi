using LicenseCopilot.Framework;
using LicenseCopilot.Framework.Connectors;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LicenseCopilot;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    private PdfDocument _CurrentDocument;
    private DocumentCopilot _DocumentCopilot;

    public MainWindow()
    {
        this.InitializeComponent();

        var configBuilder = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true)
            .Build();

        var embeddingConfig = configBuilder.GetRequiredSection("EmbeddingConfig").Get<Config>();
        var completionConfig = configBuilder.GetRequiredSection("CompletionConfig").Get<Config>();
        var mapsKey = configBuilder.GetValue<string>("AzureMapsKey");

        var sk = Kernel.Builder.Configure(embeddingConfig, completionConfig);

        _DocumentCopilot = new DocumentCopilot(sk, new AzureMapsConnector(mapsKey));
    }

    private async void FindFacets_Click(object sender, RoutedEventArgs e)
    {
        //just take the text on the current page. we're simulating someone in word working to write their own version of the document
        var text = PdfViewer.Text;

        FacetsTextBlock.Text = "Just a moment...";

        var facets = await _DocumentCopilot.FindFacetsAsync(new Framework.Model.DocumentSection
        {
            Text = text
        });

        if (facets != null)
        {
            FacetsTextBlock.Text = string.Join("\r\n\r\n", facets.Select(f=> $"[{f.Label}]\r\n{f.Reason}"));
        }
    }

    private async void OpenPdf_Click(object sender, RoutedEventArgs e)
    {        
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        
        var fileOpenPicker = new Windows.Storage.Pickers.FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(fileOpenPicker, hwnd);

        fileOpenPicker.FileTypeFilter.Add(".pdf");

        var file = await fileOpenPicker.PickSingleFileAsync();

        if (file == null)
        {
            return;
        }

        _CurrentDocument = PdfDocument.Open(file.Path);

        var allTextOnPage = ContentOrderTextExtractor.GetText(_CurrentDocument.GetPage(1));
        
        PdfViewer.Text = allTextOnPage;
        CurrentPage.Text = "1";
        TotalPages.Text = _CurrentDocument.NumberOfPages.ToString();
    }

    private void CurrentPage_TextChanged(object sender, TextChangedEventArgs e)
    {
        if(CurrentPage.Text.Length > 0 &&
            int.TryParse(CurrentPage.Text, out var desiredPageNumber) &&
            desiredPageNumber > 1 && desiredPageNumber <= _CurrentDocument.NumberOfPages)
        {
            var allTextOnPage = ContentOrderTextExtractor.GetText(_CurrentDocument.GetPage(desiredPageNumber));
            PdfViewer.Text = allTextOnPage;
        }
    }
}
