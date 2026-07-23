using System;

namespace SmartDocScan.Api.Models;

public sealed class TagUpsertRequest
{
    public int CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public string? Color { get; set; }
    public string? MatchAlgorithm { get; set; }
    public string? MatchPattern { get; set; }
}

public sealed class CorrespondentUpsertRequest
{
    public int CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public string? MatchAlgorithm { get; set; }
    public string? MatchPattern { get; set; }
}

public sealed class DocumentTypeUpsertRequest
{
    public int CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public string? MatchAlgorithm { get; set; }
    public string? MatchPattern { get; set; }
}

public sealed class UpdateBusinessDocumentMetadataRequest
{
    public int? DocumentTypeId { get; set; }
    public int? CorrespondentId { get; set; }
    public int? Asn { get; set; }
    public string? Title { get; set; }
    public decimal? Amount { get; set; }
    public DateTime? DocumentDate { get; set; }
    public System.Collections.Generic.List<int>? TagIds { get; set; }
}
