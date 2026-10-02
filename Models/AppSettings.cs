namespace dbmselect.Models;

public class AppSettings 
{
    public string? LastOutputFolder { get; set; } 
    public string? LastExcelFolder { get; set; } 
    public string? LastBrowseFolder { get; set; }

    public double BasicPackageFileLabelSize { get; set; } = 9;
    public double PackageAFileLabelSize { get; set; } = 9;
    public double PackageBFileLabelSize { get; set; } = 9;
    public double PackageCFileLabelSize { get; set; } = 9;
    public double PackageDFileLabelSize { get; set; } = 9;

    // Email Settings
    public bool EmailEnabled { get; set; } = false;
    public string? SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUser { get; set; } = "";
    public string? SmtpPassword { get; set; } = "";
    public string? SmtpSenderName { get; set; } = "DBM Photography";
}
