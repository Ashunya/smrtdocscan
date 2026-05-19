namespace SmartDocScan.Api.Models;

public sealed class BoxUpsertRequest
{
    public int CompanyId { get; set; }
    public int ExternalBoxId { get; set; }
    public string? BoxName { get; set; }
    public string? Aisle { get; set; }
    public string? Section { get; set; }
    public string? Row { get; set; }
    public string? Column { get; set; }
}
