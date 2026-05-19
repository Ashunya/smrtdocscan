namespace SmartDocScan.Api.Models;

public sealed class CategoryUpsertRequest
{
    public int CompanyId { get; set; }
    public string? CategoryName { get; set; }
    public string? Access { get; set; }
}
