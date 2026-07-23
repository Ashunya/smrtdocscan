using System;

namespace SmartDocScan.Api.Models;

public sealed class CorrespondentDto
{
    public int CorrespondentId { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public string MatchAlgorithm { get; set; } = "any";
    public string? MatchPattern { get; set; }
}
