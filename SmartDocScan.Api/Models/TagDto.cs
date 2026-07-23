using System;

namespace SmartDocScan.Api.Models;

public sealed class TagDto
{
    public int TagId { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public string Color { get; set; } = "#c1692a";
    public string MatchAlgorithm { get; set; } = "any";
    public string? MatchPattern { get; set; }
}
