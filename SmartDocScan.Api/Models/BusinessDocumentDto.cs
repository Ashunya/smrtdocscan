using System;

namespace SmartDocScan.Api.Models;

public sealed class BusinessDocumentDto
{
    public int DocumentId { get; set; }
    public int CompanyId { get; set; }
    public int? LocationId { get; set; }
    public string? LocationName { get; set; }
    public int CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string DocumentName { get; set; } = null!;
    public string Url { get; set; } = null!;
    public int NumberOfPages { get; set; }
    public DateTime Date { get; set; }
    public DateTime? DocumentDate { get; set; }
    public string? VendorName { get; set; }
    public decimal? Amount { get; set; }
    public string? UploadedBy { get; set; }
}
