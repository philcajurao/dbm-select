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
    FolderNameConstants.DBM_SELECT,
    "settings.json");

    public static bool LoadSettings(this Models.AppSettings appSettings)
    {
        if (File.Exists(_settingsFilePath))
        {
            var json = File.ReadAllText(_settingsFilePath);
            if (!string.IsNullOrEmpty(json))
            {
                appSettings = JsonSerializer.Deserialize<Models.AppSettings>(json) ?? appSettings;
                if (appSettings != null)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public static void SaveSettings(this Models.AppSettings appSettings, string outputFolderPath, string excelFolderPath, string excelFileName, string currentBrowseFolderPath)
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
        File.WriteAllText(_settingsFilePath, JsonSerializer.Serialize(appSettings));
    }
}