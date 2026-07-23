using System;

namespace SmartDocScan.Api.Models;

public sealed class BusinessDocumentDto
{
    public int DocumentId { get; set; }
    public int CompanyId { get; set; }
    public int? LocationId { get; set; }
    public string? LocationName { get; set; }
    public int? DocumentTypeId { get; set; }
    public string? DocumentTypeName { get; set; }
    public string DocumentName { get; set; } = null!;
    public string? Title { get; set; }
    public string Url { get; set; } = null!;
    public int NumberOfPages { get; set; }
    public DateTime Date { get; set; }
    public DateTime? DocumentDate { get; set; }
    public int? CorrespondentId { get; set; }
    public string? CorrespondentName { get; set; }
    public decimal? Amount { get; set; }
    public string? UploadedBy { get; set; }
    public int? ArchiveSerialNumber { get; set; }
    public string? Content { get; set; }
    public System.Collections.Generic.List<TagDto> Tags { get; set; } = new();
}
