using System.Collections.Generic;
using System.IO;

namespace dbm_select.Utils
{
    public static class GetFiles
    {
        public static IEnumerable<string> FromFolder(string folderPath, string pattern = "*.*")
        {
            if (Directory.Exists(folderPath))
                return Directory.GetFiles(folderPath, pattern);
            return new List<string>();
        }
    }
}
