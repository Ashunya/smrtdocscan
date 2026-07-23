using System;

namespace SmartDocScan.Api.Models;

public sealed class DocumentTypeDto
{
    public int DocumentTypeId { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public string MatchAlgorithm { get; set; } = "any";
    public string? MatchPattern { get; set; }
}
