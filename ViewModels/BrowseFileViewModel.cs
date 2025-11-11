using dbm_select.Utils;
using dbm_select.ViewModels;
using System.Collections.ObjectModel;

public class FileBrowserViewModel : ViewModelBase
{
    public ObservableCollection<string> Files { get; } = new();

    public void LoadFiles(string folderPath)
    {
        Files.Clear();
        foreach (var file in GetFiles.FromFolder(folderPath))
            Files.Add(System.IO.Path.GetFileName(file)); // or full path
    }
}
