using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SmartDocScan.Desktop.Services;

public class ScannerService : IScannerService
{
    public Task<List<string>> GetAvailableScannersAsync()
    {
        var result = new List<string>();
        try
        {
            var deviceManagerType = Type.GetTypeFromProgID("WIA.DeviceManager");
            if (deviceManagerType != null)
            {
                dynamic deviceManager = Activator.CreateInstance(deviceManagerType)!;
                foreach (dynamic info in deviceManager.DeviceInfos)
                {
                    // WIA device type 1 is Scanner
                    if ((int)info.Type == 1)
                    {
                        string name = info.Properties["Name"].Value.ToString();
                        result.Add($"WIA: {name}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WIA scanner discovery warning: {ex.Message}");
        }

        // Include default WIA entry if no hardware detected yet
        if (result.Count == 0)
        {
            result.Add("Windows WIA Scanner (Default)");
        }

        return Task.FromResult(result);
    }

    public Task<bool> ScanDocumentAsync(string scannerName, string outputFilePath)
    {
        return Task.Run(() =>
        {
            try
            {
                var commonDialogType = Type.GetTypeFromProgID("WIA.CommonDialog");
                if (commonDialogType != null)
                {
                    dynamic commonDialog = Activator.CreateInstance(commonDialogType)!;
                    
                    // Show standard Windows WIA Scanner acquisition dialog
                    // DeviceType = 1 (Scanner), Intent = 0 (Unspecified / User UI), FormatID = PNG GUID
                    dynamic imageFile = commonDialog.ShowAcquireImage(
                        1, // WiaDeviceType.ScannerDeviceType
                        0, // WiaImageIntent.UnspecifiedIntent
                        0, // WiaImageBias.MaximizeQuality
                        "{B96B3CAB-0728-11D3-9D7B-0000F81EF32E}", // PNG Format GUID
                        false, // AlwaysSelectDevice
                        true,  // UseCommonUI
                        false  // CancelError
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
                System.Diagnostics.Debug.WriteLine($"WIA scanning error: {ex.Message}");
            }

            return false;
        });
    }
}
