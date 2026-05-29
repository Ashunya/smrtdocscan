namespace SmartDocScan.Api.Models;

public sealed class CompanyDto
{
    public int CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string? Owner { get; set; }
    public string? Address { get; set; }
    public string? Location { get; set; }
    public string? Phone { get; set; }
    public bool Barcode { get; set; }
    public bool Inactive { get; set; }
    public string? MicrosoftTenantId { get; set; }
    public string? MicrosoftTenantName { get; set; }
    public bool MicrosoftTenantEnabled { get; set; }
    public List<CompanyTenantDto> Tenants { get; set; } = new();
}
