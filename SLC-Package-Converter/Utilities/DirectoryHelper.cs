using System.Text.RegularExpressions;

namespace SLC_Package_Converter.Utilities
{
    public static class DirectoryHelper
    {
        // Copies other directories from the source to the destination, excluding specified directories.
        public static void CopyOtherDirectories(string sourceDir, string destDir, string[] excludedDirs, string[] excludedSubDirs, string[] excludedFiles, HashSet<string>? processedFiles = null)
        {
            try
            {
                DirectoryInfo sourceDirInfo = new DirectoryInfo(sourceDir);
                foreach (FileInfo file in sourceDirInfo.GetFiles())
                {
                    // Skip processed files (if list provided), or skip .xml files by default (if no list provided)
                    bool shouldSkipXmlCsproj = processedFiles != null
                        ? processedFiles.Contains(file.FullName)
                        : (file.Extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) || file.Extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase));

                    if (shouldSkipXmlCsproj ||
                        file.Extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
                        file.Extension.Equals(".slnf", StringComparison.OrdinalIgnoreCase) ||
                        excludedFiles.Contains(file.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Perform operations on non-excluded files  
                    string destinationFilePath = Path.Combine(destDir, file.Name);
                    try
                    {
                        file.CopyTo(destinationFilePath, false);
                    }
                    catch (IOException ex)
                    {
                        Logger.LogError($"Failed to copy file {file.FullName} to {destinationFilePath}. It may already exist or be in use. code {ex}");
                        continue;
                    }
                }

                foreach (DirectoryInfo dir in sourceDirInfo.GetDirectories())
                {
                    // Skip excluded directories and hidden directories
                    if (excludedDirs.Contains(dir.Name, StringComparer.OrdinalIgnoreCase) ||
                        (dir.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden ||
                        dir.Name.StartsWith("."))
                    {
                        continue;
                    }

                    string destinationDirPath = "";

                    // Check if the folder name ends with ".Tests"
                    if (dir.Name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase))
                    {
                        // Extract the part before ".Tests", apply the regex, and add ".Tests" back
                        string baseName = dir.Name.Substring(0, dir.Name.Length - ".Tests".Length);
                        string sanitizedBaseName = Regex.Replace(baseName, @"_\d+$", string.Empty);
                        destinationDirPath = Path.Combine(destDir, sanitizedBaseName + ".Tests");
                    }
                    else
                    {
                        // Apply the regex directly if the folder name does not end with ".Tests"
                        destinationDirPath = Regex.Replace(
                            Path.Combine(destDir, dir.Name),
                            @"_\d+$", // Matches an underscore followed by one or more digits at the end of the string
                            string.Empty // Replaces the match with an empty string
                        );
                    }

                    DirectoryCopy(dir.FullName, destinationDirPath, true, excludedSubDirs, excludedFiles, processedFiles);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error copying directories: {ex.Message}");
                throw;
            }
        }

        // Copies a directory and its contents to a new location.
        public static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs, string[] excludedSubDirs, string[] excludedFiles, HashSet<string>? processedFiles = null)
        {
            try
            {
                DirectoryInfo dir = new DirectoryInfo(sourceDirName);
                DirectoryInfo[] dirs = dir.GetDirectories();

                // Check if the source directory exists
                if (!dir.Exists)
                {
                    throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {sourceDirName}");
                }

                // Create the destination directory if it does not exist
                if (!Directory.Exists(destDirName))
                {
                    Directory.CreateDirectory(destDirName);
                }

                // Copy files to the destination directory
                FileInfo[] files = dir.GetFiles();
                foreach (FileInfo file in files)
                {
                    // Skip processed files (if list provided), or skip .xml/.csproj files by default (if no list provided)
                    bool shouldSkipXmlCsproj = processedFiles != null
                        ? processedFiles.Contains(file.FullName)
                        : (file.Extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) || file.Extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase));

                    if (shouldSkipXmlCsproj || excludedFiles.Contains(file.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Separate the file name and extension
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.Name);
                    string fileExtension = file.Extension;

                    // Apply the regex to the file name without the extension
                    string sanitizedFileName = Regex.Replace(
                        fileNameWithoutExtension,
                        @"_\d+$", // Matches an underscore followed by one or more digits at the end of the string
                        string.Empty
                    );

                    // Recombine the sanitized file name with the original extension
                    string tempPath = Path.Combine(destDirName, sanitizedFileName + fileExtension);

                    try
                    {
                        file.CopyTo(tempPath, false);
                    }
                    catch (IOException ex)
                    {
                        Logger.LogError($"Failed to copy file {file.FullName} to {tempPath}. It may already exist or be in use. code {ex}");
                        continue;
                    }
                }

                // Copy subdirectories if specified
                if (copySubDirs)
                {
                    foreach (DirectoryInfo subdir in dirs)
                    {
                        if (excludedSubDirs.Contains(subdir.Name, StringComparer.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string tempPath = Path.Combine(destDirName, subdir.Name);

                        DirectoryCopy(subdir.FullName, tempPath, copySubDirs, excludedSubDirs, excludedFiles, processedFiles);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error copying directory {sourceDirName} to {destDirName}: {ex.Message}");
                throw;
            }
        }
    }
}
