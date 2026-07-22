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
    private string _baseUrl = "https://scandevapi.ashunya.com";

    public int CurrentCompanyId { get; set; } = 1;
    public string CurrentCompanyName { get; set; } = "";
    public string CurrentUserName { get; set; } = "";
    public string? SessionToken { get; private set; }

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public void SetSessionToken(string? token)
    {
        SessionToken = token;
        _httpClient.DefaultRequestHeaders.Remove("Cookie");
        if (!string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Add("Cookie", $"smartdocscan.session={token}");
        }
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
                    trimmed = "https://" + trimmed;
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

    public async Task<bool> LoginAsync(string username, string password)
    {
        var request = new { Username = username, Password = password };
        var response = await _httpClient.PostAsJsonAsync(GetUri("/api/auth/login"), request);
        if (response.IsSuccessStatusCode)
        {
            await GetCurrentUserAsync();
            return true;
        }
        return false;
    }

    public async Task<CurrentUserResponse?> GetCurrentUserAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(GetUri("/api/auth/me"));
            if (response.IsSuccessStatusCode)
            {
                var dto = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
                if (dto?.User != null)
                {
                    if (dto.User.CompanyId > 0)
                    {
                        CurrentCompanyId = dto.User.CompanyId;
                    }
                    if (!string.IsNullOrWhiteSpace(dto.User.CompanyName))
                    {
                        CurrentCompanyName = dto.User.CompanyName;
                    }
                    CurrentUserName = dto.User.Name ?? dto.User.Username ?? "";
                }
                return dto;
            }
        }
        catch { }
        return null;
    }

    public async Task<List<CategoryModel>> GetCategoriesAsync(int? companyId = null)
    {
        int cid = companyId ?? CurrentCompanyId;
        var response = await _httpClient.GetAsync(GetUri($"/api/categories?companyId={cid}"));
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<List<CategoryModel>>() ?? new List<CategoryModel>();
        }
        return new List<CategoryModel>();
    }

    public async Task<List<DocumentModel>> GetDocumentsAsync(int categoryId = 0, int? companyId = null)
    {
        int cid = companyId ?? CurrentCompanyId;
        var response = await _httpClient.GetAsync(GetUri($"/api/reports/documents?companyId={cid}&take=100"));
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

    public async Task<bool> UploadScannedDocumentAsync(string filePath, int categoryId = 1, int? companyId = null, int patientId = 1)
    {
        int cid = companyId ?? CurrentCompanyId;
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
            content.Add(new StringContent(cid.ToString()), "companyId");
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

    public async Task<(bool Success, string Message)> CreateCategoryAsync(string categoryName, int? parentId = null, int? companyId = null)
    {
        int cid = companyId ?? CurrentCompanyId;
        try
        {
            var body = new { categoryName = categoryName.Trim(), companyId = cid, parentId, access = "all", categoryType = "patient" };
            var response = await _httpClient.PostAsJsonAsync(GetUri("/api/categories"), body);
            if (response.IsSuccessStatusCode)
            {
                return (true, "Success");
            }

            var err = await response.Content.ReadAsStringAsync();
            return (false, $"HTTP {(int)response.StatusCode}: {(string.IsNullOrWhiteSpace(err) ? response.ReasonPhrase : err)}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string Message)> CreateLocationAsync(string locationName, int? companyId = null)
    {
        int cid = companyId ?? CurrentCompanyId;
        try
        {
            var body = new { locationName = locationName.Trim(), companyId = cid };
            var response = await _httpClient.PostAsJsonAsync(GetUri("/api/locations"), body);
            if (response.IsSuccessStatusCode)
            {
                return (true, "Success");
            }

            var err = await response.Content.ReadAsStringAsync();
            return (false, $"HTTP {(int)response.StatusCode}: {(string.IsNullOrWhiteSpace(err) ? response.ReasonPhrase : err)}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string Message)> CreateBoxAsync(string boxName, int locationId = 1, int? companyId = null)
    {
        int cid = companyId ?? CurrentCompanyId;
        try
        {
            var body = new { boxName = boxName.Trim(), locationId, companyId = cid };
            var response = await _httpClient.PostAsJsonAsync(GetUri("/api/boxes"), body);
            if (response.IsSuccessStatusCode)
            {
                return (true, "Success");
            }

            var err = await response.Content.ReadAsStringAsync();
            return (false, $"HTTP {(int)response.StatusCode}: {(string.IsNullOrWhiteSpace(err) ? response.ReasonPhrase : err)}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string Message)> DeleteDocumentAsync(int documentId, int? companyId = null)
    {
        int cid = companyId ?? CurrentCompanyId;
        try
        {
            var response = await _httpClient.DeleteAsync(GetUri($"/api/documents/{documentId}?companyId={cid}"));
            if (response.IsSuccessStatusCode)
            {
                return (true, "Success");
            }

            var err = await response.Content.ReadAsStringAsync();
            return (false, $"HTTP {(int)response.StatusCode}: {(string.IsNullOrWhiteSpace(err) ? response.ReasonPhrase : err)}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}

public class CurrentUserResponse
{
    public bool Authenticated { get; set; }
    public UserProfileDto? User { get; set; }
}

public class UserProfileDto
{
    public string? Username { get; set; }
    public string? Name { get; set; }
    public int CompanyId { get; set; }
    public string? CompanyName { get; set; }
}
