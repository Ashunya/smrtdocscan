namespace SmartDocScan.Api.Models;

public sealed class CategoryDto
{
    public int CategoryId { get; set; }
    public int CompanyId { get; set; }
    public string? CategoryName { get; set; }
    public string? Access { get; set; }
}
