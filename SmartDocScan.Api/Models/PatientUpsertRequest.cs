using System.ComponentModel.DataAnnotations;

namespace SmartDocScan.Api.Models;

public sealed class PatientUpsertRequest
{
    [Required]
    public int CompanyId { get; set; }

    [StringLength(30)]
    public string? ExternalPatientId { get; set; }

    [Required]
    [StringLength(60)]
    public string? FirstName { get; set; }

    [Required]
    [StringLength(60)]
    public string? LastName { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [StringLength(10)]
    public string? Gender { get; set; }

    [StringLength(100)]
    public string? Physician { get; set; }

    [StringLength(100)]
    public string? Box { get; set; }

    [StringLength(20)]
    public string? Ssn { get; set; }
}
