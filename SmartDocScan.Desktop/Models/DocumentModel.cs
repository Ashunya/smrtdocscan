using System;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartDocScan.Desktop.Models;

public partial class DocumentModel : ObservableObject
{
    [JsonPropertyName("documentId")]
    public int DocumentId { get; set; }

    [JsonPropertyName("documentName")]
    public string DocumentName { get; set; } = string.Empty;

    [JsonPropertyName("categoryId")]
    public int CategoryId { get; set; }

    [JsonPropertyName("thumbnailUrl")]
    public string ThumbnailUrl { get; set; } = string.Empty;
    
    [ObservableProperty]
    private string _localThumbnailPath = string.Empty;
}
