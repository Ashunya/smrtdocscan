using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using SmartDocScan.Desktop.Models;

namespace SmartDocScan.Desktop.Services;

public class ApiClient
{
    private readonly HttpClient _httpClient;
    
    // Default to the backend URL for the web app (as configured in Docker typically, e.g., http://localhost:8080 or https://localhost:7196)
    // We will hardcode to a local dev URL for now, but this should be configurable.
    private const string BaseUrl = "http://localhost:5031/api";

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(BaseUrl);
    }

    // Authentication methods would go here

    public async Task<List<CategoryModel>> GetCategoriesAsync()
    {
        var response = await _httpClient.GetAsync("/api/categories/tree");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<List<CategoryModel>>() ?? new List<CategoryModel>();
        }
        return new List<CategoryModel>();
    }

    public async Task<List<DocumentModel>> GetDocumentsAsync(int categoryId)
    {
        // Using companyId=1 for demo purposes
        var response = await _httpClient.GetAsync($"/api/business-documents?companyId=1&categoryId={categoryId}");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<List<DocumentModel>>() ?? new List<DocumentModel>();
        }
        return new List<DocumentModel>();
    }
}
