using System.Net.Http.Headers;

namespace SmartDocScan.Api.Services;

public sealed class GotenbergClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GotenbergClient> _logger;

    public GotenbergClient(HttpClient httpClient, ILogger<GotenbergClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Converts a document to PDF using Gotenberg.
    /// Supports Word, Excel, PowerPoint, LibreOffice, HTML, EML, etc.
    /// </summary>
    public async Task<Stream?> ConvertToPdfAsync(string fileName, Stream fileStream, CancellationToken cancellationToken)
    {
        try
        {
            var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "files", fileName);

            // Using the LibreOffice route for generic office files. Gotenberg also has /forms/chromium/convert/html
            // but libreoffice handles most documents.
            var response = await _httpClient.PostAsync("/forms/libreoffice/convert", content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gotenberg failed to convert {FileName}. Status: {StatusCode}", fileName, response.StatusCode);
                return null;
            }

            var pdfStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return pdfStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error communicating with Gotenberg for file {FileName}", fileName);
            return null;
        }
    }
}
