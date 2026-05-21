namespace SmartDocScan.Api.Models;

public sealed class AuditLogDto
{
    public long AuditId { get; set; }
    public string? Action { get; set; }
    public string? Actor { get; set; }
    public int? CompanyId { get; set; }
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public string? Outcome { get; set; }
    public string? IpAddress { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedOn { get; set; }
}
