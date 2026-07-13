namespace SmartDocScan.Api.Models;

public sealed class CategoryDto
{
    public int CategoryId { get; set; }
    public int CompanyId { get; set; }
    public string? CategoryName { get; set; }
    public string? Access { get; set; }
    public int? ParentId { get; set; }
    public string? ParentName { get; set; }
    public string CategoryType { get; set; } = "patient";
}
