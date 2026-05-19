namespace SmartDocScan.Api.Models;

public sealed class PatientDto
{
    public int PatientId { get; set; }
    public int CompanyId { get; set; }
    public string? ExternalPatientId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Physician { get; set; }
    public string? Box { get; set; }
    public string? Ssn { get; set; }
    public DateTime? LastDocumentDate { get; set; }
}
