using System.Text.Json.Serialization;

namespace SmartDocScan.Desktop.Models;

public class LocationModel
{
    [JsonPropertyName("locationId")]
    public int LocationId { get; set; }

    [JsonPropertyName("companyId")]
    public int CompanyId { get; set; }

    [JsonPropertyName("locationName")]
    public string Name { get; set; } = "";

    public override string ToString() => Name;
}
