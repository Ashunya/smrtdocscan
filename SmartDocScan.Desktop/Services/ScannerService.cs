using NAPS2.Images.Gdi;
using NAPS2.Pdf;
using NAPS2.Scan;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SmartDocScan.Desktop.Services;

public class ScannerService : IScannerService
{
    private ScanningContext? _scanningContext;

    public ScannerService()
    {
        try
        {
            var imageContext = new GdiImageContext();
            _scanningContext = new ScanningContext(imageContext);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"NAPS2 context initialization warning: {ex.Message}");
        }
    }

    public async Task<List<string>> GetAvailableScannersAsync()
    {
        var list = new List<string>();
        try
        {
            if (_scanningContext != null)
            {
                var controller = new ScanController(_scanningContext);

                // 1. TWAIN Drivers (including TWAIN2 FreeImage Virtual Scanner)
                try
                {
                    var twainDevices = await controller.GetDeviceList(Driver.Twain);
                    foreach (var dev in twainDevices)
                    {
                        if (dev != null && !string.IsNullOrWhiteSpace(dev.Name))
                        {
                            list.Add($"TWAIN: {dev.Name}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"TWAIN discovery warning: {ex.Message}");
                }

                // 2. WIA Drivers
                try
                {
                    var wiaDevices = await controller.GetDeviceList(Driver.Wia);
                    foreach (var dev in wiaDevices)
                    {
                        if (dev != null && !string.IsNullOrWhiteSpace(dev.Name))
                        {
                            list.Add($"WIA: {dev.Name}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"WIA discovery warning: {ex.Message}");
                }

                // 3. eSCL Network Scanners
                try
                {
                    var esclDevices = await controller.GetDeviceList(Driver.Escl);
                    foreach (var dev in esclDevices)
                    {
                        if (dev != null && !string.IsNullOrWhiteSpace(dev.Name))
                        {
                            list.Add($"eSCL: {dev.Name}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"eSCL discovery warning: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"NAPS2 device listing error: {ex.Message}");
        }

        // Include TWAIN2 Virtual Test Scanner
        if (!list.Any(s => s.Contains("FreeImage", StringComparison.OrdinalIgnoreCase)))
        {
            list.Insert(0, "TWAIN: TWAIN2 FreeImage Software Scanner");
        }

        if (list.Count == 0)
        {
            list.Add("Default Windows WIA Scanner");
        }

        return list.Distinct().ToList();
    }

    public async Task<bool> ScanDocumentAsync(string scannerName, string outputFilePath)
    {
        try
        {
            if (_scanningContext != null)
            {
                var controller = new ScanController(_scanningContext);
                
                Driver driver = Driver.Wia;
                if (scannerName.StartsWith("TWAIN:", StringComparison.OrdinalIgnoreCase))
                {
                    driver = Driver.Twain;
                }
                else if (scannerName.StartsWith("eSCL:", StringComparison.OrdinalIgnoreCase))
                {
                    driver = Driver.Escl;
                }

                var devices = await controller.GetDeviceList(driver);
                var selectedDevice = devices.FirstOrDefault(d => d != null && scannerName.Contains(d.Name, StringComparison.OrdinalIgnoreCase)) 
                                     ?? devices.FirstOrDefault();

                var options = new ScanOptions
                {
                    Driver = driver,
                    Device = selectedDevice,
                    Dpi = 300
                };

                var images = new List<NAPS2.Images.ProcessedImage>();
                await foreach (var img in controller.Scan(options))
                {
                    if (img != null)
                    {
                        images.Add(img);
                    }
                }

                if (images.Count > 0)
                {
                    if (File.Exists(outputFilePath))
                    {
                        File.Delete(outputFilePath);
                    }

                    var pdfExporter = new PdfExporter(_scanningContext);
                    await pdfExporter.Export(outputFilePath, images);
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"NAPS2 scan execution error: {ex.Message}");
        }

        // Fallback to WIA acquisition dialog
        return await WiaFallbackScan(outputFilePath);
    }

    private Task<bool> WiaFallbackScan(string outputFilePath)
    {
        return Task.Run(() =>
        {
            try
            {
                var commonDialogType = Type.GetTypeFromProgID("WIA.CommonDialog");
                if (commonDialogType != null)
                {
                    dynamic commonDialog = Activator.CreateInstance(commonDialogType)!;
                    dynamic imageFile = commonDialog.ShowAcquireImage(
                        1, 0, 0, "{B96B3CAB-0728-11D3-9D7B-0000F81EF32E}", false, true, false
                    );

                    if (imageFile != null)
                    {
                        if (File.Exists(outputFilePath))
                        {
                            File.Delete(outputFilePath);
                        }

                        imageFile.SaveFile(outputFilePath);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WIA Fallback error: {ex.Message}");
            }

            return false;
        });
    }
}
