namespace SmartDocScan.Api.Models;

public sealed class CompanyTenantDto
{
    public string TenantId { get; set; } = null!;
    public string? TenantName { get; set; }
    public bool Enabled { get; set; } = true;
}
