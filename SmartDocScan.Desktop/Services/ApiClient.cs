using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using SmartDocScan.Desktop.Models;

namespace SmartDocScan.Desktop.Services;

public class ApiClient
{
    private readonly HttpClient _httpClient;
    private string _baseUrl = "http://localhost:5080";

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string BaseUrl
    {
        get => _baseUrl;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                var trimmed = value.TrimEnd('/');
                if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    trimmed = "http://" + trimmed;
                }
                _baseUrl = trimmed;
            }
        }
    }

    private Uri GetUri(string path)
    {
        var baseClean = _baseUrl.TrimEnd('/');
        var pathClean = path.StartsWith('/') ? path : "/" + path;
        return new Uri(baseClean + pathClean);
    }

    // Authentication methods
    public async Task<bool> LoginAsync(string username, string password)
    {
        var request = new { Username = username, Password = password };
        var response = await _httpClient.PostAsJsonAsync(GetUri("/api/auth/login"), request);
        return response.IsSuccessStatusCode;
    }

    public async Task<List<CategoryModel>> GetCategoriesAsync(int companyId = 1)
    {
        var response = await _httpClient.GetAsync(GetUri($"/api/categories?companyId={companyId}"));
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<List<CategoryModel>>() ?? new List<CategoryModel>();
        }
        return new List<CategoryModel>();
    }

    public async Task<List<DocumentModel>> GetDocumentsAsync(int categoryId = 0, int companyId = 1)
    {
        var response = await _httpClient.GetAsync(GetUri($"/api/reports/documents?companyId={companyId}&take=50"));
        if (response.IsSuccessStatusCode)
        {
            var docs = await response.Content.ReadFromJsonAsync<List<DocumentModel>>() ?? new List<DocumentModel>();
            if (categoryId > 0)
            {
                docs = docs.Where(d => d.CategoryId == categoryId).ToList();
            }
            return docs;
        }
        return new List<DocumentModel>();
    }

    public async Task<bool> UploadScannedDocumentAsync(string filePath, int categoryId = 1, int companyId = 1, int patientId = 1)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            using var fileStream = System.IO.File.OpenRead(filePath);
            var fileContent = new StreamContent(fileStream);
            
            var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
            var mime = ext switch
            {
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".tif" or ".tiff" => "image/tiff",
                _ => "image/png"
            };

            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mime);

            content.Add(fileContent, "file", System.IO.Path.GetFileName(filePath));
            content.Add(new StringContent(companyId.ToString()), "companyId");
            content.Add(new StringContent(patientId.ToString()), "patientId");
            content.Add(new StringContent(categoryId.ToString()), "categoryId");
            content.Add(new StringContent(System.IO.Path.GetFileNameWithoutExtension(filePath)), "documentName");

            var response = await _httpClient.PostAsync(GetUri("/api/documents"), content);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CreateCategoryAsync(string categoryName, int? parentId = null, int companyId = 1)
    {
        try
        {
            var body = new { categoryName = categoryName.Trim(), companyId, parentId, access = "all", categoryType = "patient" };
            var response = await _httpClient.PostAsJsonAsync(GetUri("/api/categories"), body);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CreateLocationAsync(string locationName, int companyId = 1)
    {
        try
        {
            var body = new { locationName = locationName.Trim(), companyId };
            var response = await _httpClient.PostAsJsonAsync(GetUri("/api/locations"), body);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CreateBoxAsync(string boxName, int locationId, int companyId = 1)
    {
        try
        {
            var body = new { boxName = boxName.Trim(), locationId, companyId };
            var response = await _httpClient.PostAsJsonAsync(GetUri("/api/boxes"), body);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteDocumentAsync(int documentId, int companyId = 1)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(GetUri($"/api/documents/{documentId}?companyId={companyId}"));
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
