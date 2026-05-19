namespace SmartDocScan.Api.Models;

public sealed class DocumentDto
{
    public int DocumentId { get; set; }
    public int CompanyId { get; set; }
    public int? PatientId { get; set; }
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? DocumentName { get; set; }
    public string? Url { get; set; }
    public int NumberOfPages { get; set; }
    public DateTime Date { get; set; }
    public string? UploadedBy { get; set; }
}
