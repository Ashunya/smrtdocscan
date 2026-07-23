using System;
using System.Collections.Generic;
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
    private ObservableCollection<LocationModel> _locations = new();

    [ObservableProperty]
    private LocationModel? _selectedLocation;

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

    [ObservableProperty]
    private string _companyName = "Connecting...";

    [ObservableProperty]
    private string _userName = "";

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
    
    private System.Windows.Threading.DispatcherTimer? _refreshTimer;

    private void StartAutoRefresh()
    {
        _refreshTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(20)
        };
        _refreshTimer.Tick += async (s, e) => await SilentRefreshAsync();
        _refreshTimer.Start();
    }

    private async Task SilentRefreshAsync()
    {
        if (IsScanning) return;

        // Refresh documents silently
        int catId = SelectedCategory?.CategoryId ?? 0;
        int? locId = SelectedLocation?.LocationId;
        
        if (catId > 0)
        {
            var docs = await _apiClient.GetDocumentsAsync(catId, locId);
            
            // Only replace if count differs or just replace directly. Replacing ObservableCollection will re-render grid but that's fast.
            // To avoid UI stutter and losing selection in grid, we can just merge, but replacing is easier for now.
            // Actually, just replacing is fine since the user expects fresh data.
            Documents = new ObservableCollection<DocumentModel>(docs);
        }
        
        // Refresh categories
        var cats = await _apiClient.GetCategoriesAsync(null, "business");
        MergeCategories(Categories, cats);
    }
    
    private void MergeCategories(ObservableCollection<CategoryModel> existing, List<CategoryModel> latest)
    {
        // Simple merge: if counts differ or any ID differs, just replace and accept selection loss.
        // It's too complex to do a deep merge of WPF TreeView items without IsExpanded/IsSelected tracking.
        bool changed = existing.Count != latest.Count;
        if (!changed)
        {
            for (int i = 0; i < existing.Count; i++)
            {
                if (existing[i].CategoryId != latest[i].CategoryId)
                {
                    changed = true;
                    break;
                }
            }
        }
        
        if (changed)
        {
            Categories = new ObservableCollection<CategoryModel>(latest);
        }
    }

    private async Task LoadCategoriesAsync()
    {
        StatusMessage = "Connecting to SmartDocScan Cloud...";
        var userResp = await _apiClient.GetCurrentUserAsync();
        if (userResp?.User != null)
        {
            CompanyName = !string.IsNullOrWhiteSpace(userResp.User.CompanyName) 
                ? userResp.User.CompanyName 
                : $"Company #{_apiClient.CurrentCompanyId}";
            UserName = userResp.User.Name ?? userResp.User.Username ?? "";
        }
        else
        {
            CompanyName = $"Company #{_apiClient.CurrentCompanyId}";
        }

        StatusMessage = $"Loading storage locations & categories for {CompanyName}...";
        
        var locs = await _apiClient.GetLocationsAsync();
        Locations = new ObservableCollection<LocationModel>(locs);
        if (Locations.Count > 0 && SelectedLocation == null)
        {
            SelectedLocation = Locations[0];
        }

        var cats = await _apiClient.GetCategoriesAsync(null, "business");
        Categories = new ObservableCollection<CategoryModel>(cats);

        _ = LoadDocumentsAsync(SelectedCategory?.CategoryId ?? 0);
        StatusMessage = $"Ready | Company: {CompanyName}";
        
        if (_refreshTimer == null)
        {
            StartAutoRefresh();
        }
    }

    partial void OnSelectedCategoryChanged(CategoryModel? value)
    {
        _ = LoadDocumentsAsync(value?.CategoryId ?? 0);
    }

    partial void OnSelectedLocationChanged(LocationModel? value)
    {
        _ = LoadDocumentsAsync(SelectedCategory?.CategoryId ?? 0);
    }

    private async Task LoadDocumentsAsync(int categoryId)
    {
        StatusMessage = "Loading documents...";
        int? locId = SelectedLocation?.LocationId;
        var docs = await _apiClient.GetDocumentsAsync(categoryId, locId);
        Documents = new ObservableCollection<DocumentModel>(docs);
        StatusMessage = $"{Documents.Count} documents loaded for {CompanyName}.";
    }

    private async Task ScanDocumentAsync()
    {
        IsScanning = true;
        StatusMessage = "Discovering connected scanners...";

        try
        {
            var scanners = await _scannerService.GetAvailableScannersAsync();
            var settingsWindow = new ScanSettingsWindow(scanners, new List<LocationModel>(Locations), SelectedLocation)
            {
                Owner = Application.Current.MainWindow
            };

            if (settingsWindow.ShowDialog() == true)
            {
                if (settingsWindow.SelectedLocation != null)
                {
                    SelectedLocation = settingsWindow.SelectedLocation;
                }

                var scanner = settingsWindow.SelectedScanner;
                var tempPath = Path.Combine(Path.GetTempPath(), $"Scan_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                StatusMessage = $"NAPS2 Scanning via '{scanner}'...";
                bool scanned = await _scannerService.ScanDocumentAsync(scanner, tempPath);

                if (scanned && File.Exists(tempPath))
                {
                    // Open Live Interactive Scan Preview Window
                    var previewWindow = new ScanPreviewWindow(tempPath)
                    {
                        Owner = Application.Current.MainWindow
                    };

                    if (previewWindow.ShowDialog() == true)
                    {
                        StatusMessage = "Uploading scanned document to SmartDocScan Cloud...";
                        int catId = SelectedCategory?.CategoryId ?? 1;
                        int? locId = SelectedLocation?.LocationId;
                        var (uploaded, uploadMessage) = await _apiClient.UploadScannedDocumentAsync(tempPath, catId, locId);

                        if (uploaded)
                        {
                            StatusMessage = "Document scanned and uploaded successfully!";
                            await LoadDocumentsAsync(catId);
                        }
                        else
                        {
                            StatusMessage = $"Cloud upload failed: {uploadMessage}";
                            MessageBox.Show($"Could not upload scanned document to cloud:\n\n{uploadMessage}", "Cloud Upload Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        StatusMessage = "Scan discarded by user.";
                    }
                }
                else
                {
                    StatusMessage = "Scanning cancelled or no image acquired.";
                }
            }
            else
            {
                StatusMessage = "Scanning cancelled.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan error: {ex.Message}";
            MessageBox.Show($"Scan Error:\n\n{ex.Message}", "NAPS2 Scan Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            int? locId = SelectedLocation?.LocationId;
            var (uploaded, uploadMessage) = await _apiClient.UploadScannedDocumentAsync(dialog.FileName, catId, locId);

            if (uploaded)
            {
                StatusMessage = "File imported and uploaded successfully!";
                await LoadDocumentsAsync(catId);
            }
            else
            {
                StatusMessage = $"Failed to upload imported file: {uploadMessage}";
                MessageBox.Show($"Could not upload imported file:\n\n{uploadMessage}", "File Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            var (created, message) = await _apiClient.CreateCategoryAsync(inputName, parentId);
            if (created)
            {
                StatusMessage = $"Category '{inputName}' created successfully!";
                await LoadCategoriesAsync();
            }
            else
            {
                StatusMessage = $"Failed to create category: {message}";
                MessageBox.Show($"Could not create folder:\n\n{message}", "Create Category Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async Task CreateLocationAsync()
    {
        string? inputName = InputDialogWindow.Prompt("Create New Storage Location", "Enter location name:", "Warehouse A");
        if (!string.IsNullOrWhiteSpace(inputName))
        {
            StatusMessage = "Creating location...";
            var (created, message) = await _apiClient.CreateLocationAsync(inputName);
            if (created)
            {
                StatusMessage = $"Location '{inputName}' created successfully!";
                await LoadCategoriesAsync();
            }
            else
            {
                StatusMessage = $"Failed to create location: {message}";
                MessageBox.Show($"Could not create location:\n\n{message}", "Create Location Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async Task CreateBoxAsync()
    {
        string? inputName = InputDialogWindow.Prompt("Create New Storage Box", "Enter box name:", "Box #101");
        if (!string.IsNullOrWhiteSpace(inputName))
        {
            StatusMessage = "Creating box...";
            int locId = SelectedLocation?.LocationId ?? 1;
            var (created, message) = await _apiClient.CreateBoxAsync(inputName, locId);
            if (created)
            {
                StatusMessage = $"Box '{inputName}' created successfully!";
            }
            else
            {
                StatusMessage = $"Failed to create box: {message}";
                MessageBox.Show($"Could not create box:\n\n{message}", "Create Box Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            var (deleted, message) = await _apiClient.DeleteDocumentAsync(SelectedDocument.DocumentId);
            if (deleted)
            {
                StatusMessage = "Document deleted.";
                await LoadDocumentsAsync(SelectedCategory?.CategoryId ?? 0);
            }
            else
            {
                StatusMessage = $"Failed to delete document: {message}";
                MessageBox.Show($"Could not delete document:\n\n{message}", "Delete Document Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ViewDocument()
    {
        if (SelectedDocument == null) return;

        var url = _apiClient.BaseUrl.TrimEnd('/') + SelectedDocument.PreviewUrl;
        
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
