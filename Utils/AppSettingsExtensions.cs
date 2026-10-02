using dbmselect.Models;
using System;
using System.IO;
using System.Text.Json;
using Models = dbmselect.Models;

namespace Utils;

public static class AppSettingsExtensions
{
    private static readonly string _settingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DBM Select", 
        "settings.json");

    public static bool LoadSettings(this Models.AppSettings appSettings)
    {
        try 
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                if (!string.IsNullOrEmpty(json))
                {
                    var loaded = JsonSerializer.Deserialize<Models.AppSettings>(json);
                    if (loaded != null)
                    {
                        // FIX: Map properties manually to the existing instance
                        appSettings.LastOutputFolder = loaded.LastOutputFolder;
                        appSettings.LastExcelFolder = loaded.LastExcelFolder;
                        appSettings.LastBrowseFolder = loaded.LastBrowseFolder;
                        appSettings.BasicPackageFileLabelSize = loaded.BasicPackageFileLabelSize > 0 ? loaded.BasicPackageFileLabelSize : 9;
                        appSettings.PackageAFileLabelSize = loaded.PackageAFileLabelSize > 0 ? loaded.PackageAFileLabelSize : 9;
                        appSettings.PackageBFileLabelSize = loaded.PackageBFileLabelSize > 0 ? loaded.PackageBFileLabelSize : 9;
                        appSettings.PackageCFileLabelSize = loaded.PackageCFileLabelSize > 0 ? loaded.PackageCFileLabelSize : 9;
                        appSettings.PackageDFileLabelSize = loaded.PackageDFileLabelSize > 0 ? loaded.PackageDFileLabelSize : 9;
                        return true;
                    }
                }
            }
        }
        catch 
        {
            // Ignore errors, return false to trigger default creation
        }
        return false;
    }

    public static void SaveSettings(this Models.AppSettings appSettings, string outputFolderPath, string excelFolderPath, string currentBrowseFolderPath)
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory!);
            }
            appSettings.LastOutputFolder = outputFolderPath;
            appSettings.LastExcelFolder = excelFolderPath;
            appSettings.LastBrowseFolder = currentBrowseFolderPath;

            var json = JsonSerializer.Serialize(appSettings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
        }
        catch
        {
            // Handle save errors if needed
        }
    }
}