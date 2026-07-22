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
    public int? CategoryId { get; set; }

    [JsonPropertyName("categoryName")]
    public string? CategoryName { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    public string ThumbnailUrl => $"/api/business-documents/{DocumentId}/thumbnail";
    public string PreviewUrl => $"/api/business-documents/{DocumentId}/preview";
}
