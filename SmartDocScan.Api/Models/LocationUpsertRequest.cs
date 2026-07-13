namespace SmartDocScan.Api.Models;

public sealed class LocationUpsertRequest
{
    public int? LocationId { get; set; }
    public int CompanyId { get; set; }
    public string LocationName { get; set; } = null!;
    public string? LocationCode { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public bool Inactive { get; set; }
}
