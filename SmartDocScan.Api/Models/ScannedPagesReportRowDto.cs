namespace SmartDocScan.Api.Models;

public sealed class ScannedPagesReportRowDto
{
    public int CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime? Day { get; set; }
    public int Documents { get; set; }
    public long Pages { get; set; }
}
