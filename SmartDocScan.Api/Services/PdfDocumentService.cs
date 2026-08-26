using System;
using System.IO;
using System.Threading.Tasks;
using ImageMagick;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace SmartDocScan.Api.Services;

public class PdfDocumentService
{
    public Task MergeToPdfAsync(string[] sourceFilePaths, string outputFilePath)
    {
        return Task.Run(() =>
        {
            using var outputDocument = new PdfDocument();

            foreach (var filePath in sourceFilePaths)
            {
                if (!File.Exists(filePath)) continue;

                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext == ".pdf")
                {
                    using var inputDocument = PdfReader.Open(filePath, PdfDocumentOpenMode.Import);
                    for (int i = 0; i < inputDocument.PageCount; i++)
                    {
                        outputDocument.AddPage(inputDocument.Pages[i]);
                    }
                }
                else if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".tiff" || ext == ".tif" || ext == ".bmp")
                {
                    // Create a new page
                    var page = outputDocument.AddPage();
                    using var gfx = XGraphics.FromPdfPage(page);
                    
                    // Draw image
                    using var image = XImage.FromFile(filePath);
                    
                    // Scale image to fit the page while maintaining aspect ratio
                    double width = image.PixelWidth * 72 / image.HorizontalResolution;
                    double height = image.PixelHeight * 72 / image.HorizontalResolution;
                    page.Width = width;
                    page.Height = height;
                    
                    gfx.DrawImage(image, 0, 0, width, height);
                }
            }

            outputDocument.Save(outputFilePath);
        });
    }

    public Task ReorderPagesAsync(string sourceFilePath, int[] newPageOrder, string outputFilePath)
    {
        return Task.Run(() =>
        {
            using var inputDocument = PdfReader.Open(sourceFilePath, PdfDocumentOpenMode.Import);
            using var outputDocument = new PdfDocument();

            foreach (var index in newPageOrder)
            {
                if (index >= 0 && index < inputDocument.PageCount)
                {
                    outputDocument.AddPage(inputDocument.Pages[index]);
                }
            }

            outputDocument.Save(outputFilePath);
        });
    }

    public Task ExtractPagesAsync(string sourceFilePath, int[] pageIndices, string outputFilePath)
    {
        return ReorderPagesAsync(sourceFilePath, pageIndices, outputFilePath);
    }
}
