using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SmartDocScan.Desktop.Models;

namespace SmartDocScan.Desktop.Services;

public class ApiClient
{
    private readonly HttpClient _httpClient;
    public string BaseUrl { get; set; } = "https://scandevapi.ashunya.com/";
    public int CurrentCompanyId { get; set; } = 1;
    public string CurrentCompanyName { get; set; } = "Ashunya";
    public string CurrentUserName { get; set; } = "";

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public void SetSessionToken(string token)
    {
        _httpClient.DefaultRequestHeaders.Remove("Cookie");
        if (!string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Add("Cookie", $"smartdocscan.session={token}");
        }
    }

    private Uri GetUri(string relativePath)
    {
        var baseUri = new Uri(BaseUrl.TrimEnd('/') + "/");
        return new Uri(baseUri, relativePath.TrimStart('/'));
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(GetUri("/api/auth/login"), new { username = email, password });
            if (response.IsSuccessStatusCode)
            {
                var userResp = await GetCurrentUserAsync();
                return userResp?.User != null;
            }
        }
        catch { }
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

    public async Task<List<LocationModel>> GetLocationsAsync(int? companyId = null)
    {
        int cid = companyId ?? CurrentCompanyId;
        var response = await _httpClient.GetAsync(GetUri($"/api/locations?companyId={cid}"));
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<List<LocationModel>>() ?? new List<LocationModel>();
        }
        return new List<LocationModel>();
    }

    public async Task<List<CategoryModel>> GetCategoriesAsync(int? companyId = null, string? type = null)
    {
        int cid = companyId ?? CurrentCompanyId;
        string url = $"/api/categories?companyId={cid}";
        if (!string.IsNullOrEmpty(type))
        {
            url += $"&type={type}";
        }
        var response = await _httpClient.GetAsync(GetUri(url));
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<List<CategoryModel>>() ?? new List<CategoryModel>();
        }
        return new List<CategoryModel>();
    }

    public async Task<List<DocumentModel>> GetDocumentsAsync(int categoryId = 0, int? locationId = null, int? companyId = null)
    {
        int cid = companyId ?? CurrentCompanyId;
        string url = $"/api/reports/documents?companyId={cid}&take=100";
        if (locationId.HasValue && locationId.Value > 0)
        {
            url += $"&locationId={locationId.Value}";
        }

        var response = await _httpClient.GetAsync(GetUri(url));
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

    public async Task<(bool Success, string Message)> UploadScannedDocumentAsync(string filePath, int categoryId = 1, int? locationId = null, int? companyId = null, int? patientId = null)
    {
        int cid = companyId ?? CurrentCompanyId;
        int locId = locationId ?? 1;
        try
        {
            int pid = patientId ?? 0;
            if (pid <= 0)
            {
                var patientsResp = await _httpClient.GetAsync(GetUri($"/api/patients?companyId={cid}&take=1"));
                if (patientsResp.IsSuccessStatusCode)
                {
                    var patients = await patientsResp.Content.ReadFromJsonAsync<List<PatientModel>>();
                    if (patients?.Count > 0)
                    {
                        pid = patients[0].PatientId;
                    }
                }
            }

            if (pid <= 0) pid = 1;

            var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
            var mime = ext switch
            {
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".tif" or ".tiff" => "image/tiff",
                _ => "image/png"
            };

            // Attempt 1: Business / General Document Upload (with locationId)
            using (var bizContent = new MultipartFormDataContent())
            using (var bizStream = System.IO.File.OpenRead(filePath))
            {
                var bizFileContent = new StreamContent(bizStream);
                bizFileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mime);

                bizContent.Add(bizFileContent, "file", System.IO.Path.GetFileName(filePath));
                bizContent.Add(new StringContent(cid.ToString()), "companyId");
                bizContent.Add(new StringContent(locId.ToString()), "locationId");
                bizContent.Add(new StringContent(categoryId.ToString()), "categoryId");
                bizContent.Add(new StringContent(System.IO.Path.GetFileNameWithoutExtension(filePath)), "documentName");

                var bizResponse = await _httpClient.PostAsync(GetUri("/api/business-documents"), bizContent);
                if (bizResponse.IsSuccessStatusCode)
                {
                    return (true, "Success");
                }
            }

            // Attempt 2: Patient Document Upload
            using (var content = new MultipartFormDataContent())
            using (var fileStream = System.IO.File.OpenRead(filePath))
            {
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mime);

                content.Add(fileContent, "file", System.IO.Path.GetFileName(filePath));
                content.Add(new StringContent(cid.ToString()), "companyId");
                content.Add(new StringContent(locId.ToString()), "locationId");
                content.Add(new StringContent(pid.ToString()), "patientId");
                content.Add(new StringContent(categoryId.ToString()), "categoryId");
                content.Add(new StringContent(System.IO.Path.GetFileNameWithoutExtension(filePath)), "documentName");

                var response = await _httpClient.PostAsync(GetUri("/api/documents"), content);
                if (response.IsSuccessStatusCode)
                {
                    return (true, "Success");
                }

                var errText = await response.Content.ReadAsStringAsync();
                return (false, $"HTTP {(int)response.StatusCode}: {(string.IsNullOrWhiteSpace(errText) ? response.ReasonPhrase : errText)}");
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
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
            var body = new { name = locationName.Trim(), companyId = cid };
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
            var body = new { boxName = boxName.Trim(), companyId = cid, locationId };
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

    public async Task<(bool Success, string Message)> DeleteDocumentAsync(int documentId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(GetUri($"/api/documents/{documentId}"));
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
    [JsonPropertyName("user")]
    public UserDto? User { get; set; }
}

public class UserDto
{
    [JsonPropertyName("userId")]
    public int UserId { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("companyId")]
    public int CompanyId { get; set; }

    [JsonPropertyName("companyName")]
    public string? CompanyName { get; set; }
}

public class PatientModel
{
    [JsonPropertyName("patientId")]
    public int PatientId { get; set; }

    [JsonPropertyName("companyId")]
    public int CompanyId { get; set; }

    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }
}
