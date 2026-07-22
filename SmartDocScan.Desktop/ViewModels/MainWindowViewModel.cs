using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SmartDocScan.Desktop.Models;
using SmartDocScan.Desktop.Services;
using SmartDocScan.Desktop.Views;

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
    private DocumentModel? _selectedDocument;

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
        ImportFileCommand = new AsyncRelayCommand(ImportFileAsync);
        CreateCategoryCommand = new AsyncRelayCommand(CreateCategoryAsync);
        CreateLocationCommand = new AsyncRelayCommand(CreateLocationAsync);
        CreateBoxCommand = new AsyncRelayCommand(CreateBoxAsync);
        DeleteDocumentCommand = new AsyncRelayCommand(DeleteDocumentAsync);
        ViewDocumentCommand = new RelayCommand(ViewDocument);
    }

    public IAsyncRelayCommand LoadCategoriesCommand { get; }
    public IAsyncRelayCommand ScanDocumentCommand { get; }
    public IAsyncRelayCommand ImportFileCommand { get; }
    public IAsyncRelayCommand CreateCategoryCommand { get; }
    public IAsyncRelayCommand CreateLocationCommand { get; }
    public IAsyncRelayCommand CreateBoxCommand { get; }
    public IAsyncRelayCommand DeleteDocumentCommand { get; }
    public IRelayCommand ViewDocumentCommand { get; }

    private async Task LoadCategoriesAsync()
    {
        StatusMessage = "Loading categories...";
        var cats = await _apiClient.GetCategoriesAsync();
        Categories = new ObservableCollection<CategoryModel>(cats);

        _ = LoadDocumentsAsync(SelectedCategory?.CategoryId ?? 0);
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
                StatusMessage = "Uploading scanned document...";
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

    private async Task ImportFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import Document / Image File",
            Filter = "All Supported Files|*.pdf;*.png;*.jpg;*.jpeg;*.tif;*.tiff|PDF Documents (*.pdf)|*.pdf|Image Files (*.png;*.jpg;*.jpeg;*.tif)|*.png;*.jpg;*.jpeg;*.tif"
        };

        if (dialog.ShowDialog() == true)
        {
            StatusMessage = "Uploading imported file...";
            int catId = SelectedCategory?.CategoryId ?? 1;
            bool uploaded = await _apiClient.UploadScannedDocumentAsync(dialog.FileName, catId);

            if (uploaded)
            {
                StatusMessage = "File imported and uploaded successfully!";
                await LoadDocumentsAsync(catId);
            }
            else
            {
                StatusMessage = "Failed to upload imported file.";
            }
        }
    }

    private async Task CreateCategoryAsync()
    {
        string? inputName = InputDialogWindow.Prompt("Create New Folder / Category", "Enter folder name:", "New Folder");
        if (!string.IsNullOrWhiteSpace(inputName))
        {
            StatusMessage = "Creating category...";
            int? parentId = SelectedCategory?.CategoryId;
            bool created = await _apiClient.CreateCategoryAsync(inputName, parentId);
            if (created)
            {
                StatusMessage = $"Category '{inputName}' created!";
                await LoadCategoriesAsync();
            }
            else
            {
                StatusMessage = "Failed to create category.";
            }
        }
    }

    private async Task CreateLocationAsync()
    {
        string? inputName = InputDialogWindow.Prompt("Create New Storage Location", "Enter location name:", "Warehouse A");
        if (!string.IsNullOrWhiteSpace(inputName))
        {
            StatusMessage = "Creating location...";
            bool created = await _apiClient.CreateLocationAsync(inputName);
            if (created)
            {
                StatusMessage = $"Location '{inputName}' created!";
            }
            else
            {
                StatusMessage = "Failed to create location.";
            }
        }
    }

    private async Task CreateBoxAsync()
    {
        string? inputName = InputDialogWindow.Prompt("Create New Storage Box", "Enter box name:", "Box #101");
        if (!string.IsNullOrWhiteSpace(inputName))
        {
            StatusMessage = "Creating box...";
            bool created = await _apiClient.CreateBoxAsync(inputName, 1);
            if (created)
            {
                StatusMessage = $"Box '{inputName}' created!";
            }
            else
            {
                StatusMessage = "Failed to create box.";
            }
        }
    }

    private async Task DeleteDocumentAsync()
    {
        if (SelectedDocument == null)
        {
            MessageBox.Show("Please select a document to delete.", "Delete Document", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show($"Are you sure you want to delete '{SelectedDocument.DocumentName}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm == MessageBoxResult.Yes)
        {
            StatusMessage = "Deleting document...";
            bool deleted = await _apiClient.DeleteDocumentAsync(SelectedDocument.DocumentId);
            if (deleted)
            {
                StatusMessage = "Document deleted.";
                await LoadDocumentsAsync(SelectedCategory?.CategoryId ?? 0);
            }
            else
            {
                StatusMessage = "Failed to delete document.";
            }
        }
    }

    private void ViewDocument()
    {
        if (SelectedDocument == null) return;

        var url = SelectedDocument.Url;
        if (!string.IsNullOrWhiteSpace(url))
        {
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = _apiClient.BaseUrl.TrimEnd('/') + "/" + url.TrimStart('/');
            }

            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Could not open document: {ex.Message}";
            }
        }
    }
}
