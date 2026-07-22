using System.Text.Json.Serialization;

namespace SmartDocScan.Desktop.Models;

public class CategoryModel
{
    [JsonPropertyName("categoryId")]
    public int CategoryId { get; set; }

    [JsonPropertyName("categoryName")]
    public string CategoryName { get; set; } = string.Empty;

    public string Name => string.IsNullOrWhiteSpace(CategoryName) ? "Uncategorized" : CategoryName;

    [JsonPropertyName("subCategories")]
    public System.Collections.ObjectModel.ObservableCollection<CategoryModel> SubCategories { get; set; } = new();
}
