namespace SmartDocScan.Api.Models;

public sealed class UserUpsertRequest
{
    public string? Username { get; set; }
    public string? Name { get; set; }
    public string? Password { get; set; }
    public int CompanyId { get; set; }
    public bool UploadDocument { get; set; }
    public bool ScanDocument { get; set; }
    public bool DeleteDocument { get; set; }
    public bool DeleteManage { get; set; }
    public bool PrintDocument { get; set; }
    public bool DownloadDocument { get; set; }
    public bool AddCategory { get; set; }
    public bool AddUsers { get; set; }
    public bool AddPatients { get; set; }
    public bool Box { get; set; }
    public bool Report { get; set; }
    public bool SuperUser { get; set; }
    public bool Disabled { get; set; }
    public bool IsAdmin { get; set; }
}
