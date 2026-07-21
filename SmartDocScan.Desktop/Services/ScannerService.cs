using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartDocScan.Desktop.Services;

public class ScannerService : IScannerService
{
    public Task<List<string>> GetAvailableScannersAsync()
    {
        // Stub implementation
        // Here we will use TWAIN or WIA libraries (e.g. NTwain or WIA COM)
        // to detect attached scanners.
        return Task.FromResult(new List<string>
        {
            "TWAIN: Canon DR-C225",
            "WIA: HP OfficeJet Pro 9010",
            "TWAIN: Epson WorkForce ES-400"
        });
    }

    public Task<bool> ScanDocumentAsync(string scannerName, string outputFilePath)
    {
        // Stub implementation
        // Here we will trigger the TWAIN/WIA scan dialog or silent scan
        // and save the multi-page TIFF/PDF to the outputFilePath.
        return Task.FromResult(true);
    }
}
