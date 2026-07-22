using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartDocScan.Desktop.Models;
using SmartDocScan.Desktop.Services;

namespace SmartDocScan.Desktop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ApiClient _apiClient;
    private readonly IScannerService _scannerService;

    [ObservableProperty]
    private ObservableCollection<CategoryModel> _categories = new();

    [ObservableProperty]
    private ObservableCollection<DocumentModel> _documents = new();

    [ObservableProperty]
    private CategoryModel? _selectedCategory;

    [ObservableProperty]
    private bool _isScanning = false;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    public MainWindowViewModel(ApiClient apiClient, IScannerService scannerService)
    {
        _apiClient = apiClient;
        _scannerService = scannerService;

        LoadCategoriesCommand = new AsyncRelayCommand(LoadCategoriesAsync);
        ScanDocumentCommand = new AsyncRelayCommand(ScanDocumentAsync);
    }

    public IAsyncRelayCommand LoadCategoriesCommand { get; }
    public IAsyncRelayCommand ScanDocumentCommand { get; }

    private async Task LoadCategoriesAsync()
    {
        StatusMessage = "Loading categories...";
        var cats = await _apiClient.GetCategoriesAsync();
        Categories = new ObservableCollection<CategoryModel>(cats);

        // Load all documents by default
        _ = LoadDocumentsAsync(0);
        StatusMessage = $"{Categories.Count} categories loaded.";
    }

    partial void OnSelectedCategoryChanged(CategoryModel? value)
    {
        if (value != null)
        {
            _ = LoadDocumentsAsync(value.CategoryId);
        }
        else
        {
            _ = LoadDocumentsAsync(0);
        }
    }

    private async Task LoadDocumentsAsync(int categoryId)
    {
        StatusMessage = "Loading documents...";
        var docs = await _apiClient.GetDocumentsAsync(categoryId);
        Documents = new ObservableCollection<DocumentModel>(docs);
        StatusMessage = $"{Documents.Count} documents loaded.";
    }

    private async Task ScanDocumentAsync()
    {
        IsScanning = true;
        StatusMessage = "Initializing scanner...";

        try
        {
            var scanners = await _scannerService.GetAvailableScannersAsync();
            var scanner = scanners.Count > 0 ? scanners[0] : "Default WIA Scanner";

            var tempPath = Path.Combine(Path.GetTempPath(), $"Scan_{DateTime.Now:yyyyMMdd_HHmmss}.png");

            StatusMessage = "Acquiring scan from scanner...";
            bool scanned = await _scannerService.ScanDocumentAsync(scanner, tempPath);

            if (scanned && File.Exists(tempPath))
            {
                StatusMessage = "Uploading scanned document to cloud...";
                int catId = SelectedCategory?.CategoryId ?? 1;
                bool uploaded = await _apiClient.UploadScannedDocumentAsync(tempPath, catId);

                if (uploaded)
                {
                    StatusMessage = "Document scanned and uploaded successfully!";
                    await LoadDocumentsAsync(catId);
                }
                else
                {
                    StatusMessage = "Scan completed locally, but upload failed.";
                }
            }
            else
            {
                StatusMessage = "Scanning cancelled or no image acquired.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }
}
