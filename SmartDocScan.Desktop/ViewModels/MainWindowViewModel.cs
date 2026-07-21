using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartDocScan.Desktop.Models;
using SmartDocScan.Desktop.Services;

namespace SmartDocScan.Desktop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ApiClient _apiClient;

    [ObservableProperty]
    private ObservableCollection<CategoryModel> _categories = new();

    [ObservableProperty]
    private ObservableCollection<DocumentModel> _documents = new();

    [ObservableProperty]
    private CategoryModel? _selectedCategory;

    public MainWindowViewModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
        LoadCategoriesCommand = new AsyncRelayCommand(LoadCategoriesAsync);
    }

    public IAsyncRelayCommand LoadCategoriesCommand { get; }

    private async Task LoadCategoriesAsync()
    {
        var cats = await _apiClient.GetCategoriesAsync();
        Categories = new ObservableCollection<CategoryModel>(cats);
    }

    partial void OnSelectedCategoryChanged(CategoryModel? value)
    {
        if (value != null)
        {
            _ = LoadDocumentsAsync(value.CategoryId);
        }
        else
        {
            Documents.Clear();
        }
    }

    private async Task LoadDocumentsAsync(int categoryId)
    {
        var docs = await _apiClient.GetDocumentsAsync(categoryId);
        Documents = new ObservableCollection<DocumentModel>(docs);
    }
}
