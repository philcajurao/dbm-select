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
        "DBM Select", // Hardcoded safely or use your constant
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
                        appSettings.LastExcelFileName = loaded.LastExcelFileName;
                        appSettings.LastBrowseFolder = loaded.LastBrowseFolder;
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

    public static void SaveSettings(this Models.AppSettings appSettings, string outputFolderPath, string excelFolderPath, string excelFileName, string currentBrowseFolderPath)
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
            appSettings.LastExcelFileName = excelFileName;
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