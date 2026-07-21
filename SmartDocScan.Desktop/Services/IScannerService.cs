using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartDocScan.Desktop.Services;

public interface IScannerService
{
    Task<List<string>> GetAvailableScannersAsync();
    Task<bool> ScanDocumentAsync(string scannerName, string outputFilePath);
}
