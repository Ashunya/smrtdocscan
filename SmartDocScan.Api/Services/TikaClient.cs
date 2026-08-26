using System.Net.Http.Headers;

namespace SmartDocScan.Api.Services;

public sealed class TikaClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TikaClient> _logger;

    public TikaClient(HttpClient httpClient, ILogger<TikaClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Extracts text from a document using Apache Tika.
    /// Supports OCR if Tika is configured with Tesseract.
    /// </summary>
    public async Task<string?> ExtractTextAsync(string fileName, Stream fileStream, CancellationToken cancellationToken)
    {
        try
        {
            var content = new StreamContent(fileStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Headers.Add("file-name", fileName);
            content.Headers.Add("X-Tika-PDFOcrStrategy", "ocr_and_text_extraction");
            // Request text format
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));

            var response = await _httpClient.PutAsync("/tika", content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Tika failed to extract text from {FileName}. Status: {StatusCode}", fileName, response.StatusCode);
                return null;
            }

            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            return text?.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error communicating with Tika for file {FileName}", fileName);
            return null;
        }
    }
}
