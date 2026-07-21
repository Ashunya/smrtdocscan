using System;
using System.Text.Json.Serialization;

namespace SmartDocScan.Desktop.Models;

public class CategoryModel
{
    [JsonPropertyName("categoryId")]
    public int CategoryId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("subCategories")]
    public System.Collections.ObjectModel.ObservableCollection<CategoryModel> SubCategories { get; set; } = new();
}
