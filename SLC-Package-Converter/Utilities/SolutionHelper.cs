namespace SLC_Package_Converter.Utilities
{
    public static class SolutionHelper
    {
        public static string? GetSolutionFile(string directory)
        {
            try
            {
                // Get the first .sln file in the directory
                string[] slnFiles = Directory.GetFiles(directory, "*.sln", SearchOption.TopDirectoryOnly);
                if (slnFiles.Length > 0)
                {
                    return slnFiles.FirstOrDefault();
                }

                // If no solution file found in root, search one level down in subdirectories
                Logger.LogInfo("No solution file found in root directory. Searching in subdirectories...");
                string[] subdirectories = Directory.GetDirectories(directory);
                
                foreach (string subdirectory in subdirectories)
                {
                    string[] subSlnFiles = Directory.GetFiles(subdirectory, "*.sln", SearchOption.TopDirectoryOnly);
                    if (subSlnFiles.Length > 0)
                    {
                        Logger.LogInfo($"Solution file found in subdirectory: {subdirectory}");
                        // Copy all files from subdirectory to root directory
                        CopySubdirectoryFilesToRoot(subdirectory, directory);
                        
                        // Return the path to the solution file now in the root directory
                        string solutionFileName = Path.GetFileName(subSlnFiles.First());
                        string rootSolutionPath = Path.Combine(directory, solutionFileName);
                        
                        return rootSolutionPath;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error retrieving solution file: {ex.Message}");
                throw;
            }
        }
        public static void AddSharedProjectReferences(string? sourceSlnFile, string? destSlnFile)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sourceSlnFile) || string.IsNullOrWhiteSpace(destSlnFile))
                {
                    throw new ArgumentException("Source or destination solution file path is null or empty.");
                }

                // Read all lines from the source solution file
                var sourceLines = File.ReadAllLines(sourceSlnFile);

                // Filter lines containing ".shproj"
                var sharedProjectLines = sourceLines.Where(line => line.Contains(".shproj")).ToList();

                if (!sharedProjectLines.Any())
                {
                    Logger.LogInfo("No shared project references found in the source solution file.");
                    return;
                }

                // Read all lines from the destination solution file
                var destLines = File.ReadAllLines(destSlnFile).ToList();

                // Find the index of the "Global" section
                var globalIndex = destLines.FindIndex(line => line.TrimStart().StartsWith("Global", StringComparison.OrdinalIgnoreCase));
                if (globalIndex == -1)
                {
                    throw new InvalidOperationException("The destination solution file does not contain a 'Global' section.");
                }

                // Insert shared project lines before the "Global" section if not already present
                foreach (var line in sharedProjectLines)
                {
                    if (!destLines.Contains(line))
                    {
                        destLines.Insert(globalIndex, "EndProject");
                        destLines.Insert(globalIndex, line);
                    }
                }

                // Write updated lines back to the destination solution file
                File.WriteAllLines(destSlnFile, destLines);

                Logger.LogInfo("Shared project references successfully added to the destination solution file.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error adding shared project references: {ex.Message}");
                throw;
            }
        }

        private static void CopySubdirectoryFilesToRoot(string subdirectoryPath, string rootDirectory)
        {
            try
            {
                Logger.LogInfo($"Copying files from {subdirectoryPath} to {rootDirectory}");
                
                // Get all files in the subdirectory
                string[] files = Directory.GetFiles(subdirectoryPath, "*", SearchOption.TopDirectoryOnly);
                
                foreach (string sourceFile in files)
                {
                    string fileName = Path.GetFileName(sourceFile);
                    string destinationFile = Path.Combine(rootDirectory, fileName);
                    
                    // Check if file already exists in root directory
                    if (File.Exists(destinationFile))
                    {
                        // Compare file sizes and warn if different
                        FileInfo sourceInfo = new FileInfo(sourceFile);
                        FileInfo destInfo = new FileInfo(destinationFile);
                        
                        if (sourceInfo.Length != destInfo.Length)
                        {
                            Logger.LogWarning($"Replacing {fileName}: file size differs (original: {destInfo.Length} bytes, new: {sourceInfo.Length} bytes)");
                        }
                        else
                        {
                            Logger.LogInfo($"Replacing {fileName} (same file size: {sourceInfo.Length} bytes)");
                        }
                    }
                    else
                    {
                        Logger.LogInfo($"Copying {fileName} to root directory");
                    }
                    
                    // Copy the file, overwriting if it exists
                    File.Copy(sourceFile, destinationFile, true);
                }
                
                // Also copy subdirectories from the subdirectory
                string[] subDirectories = Directory.GetDirectories(subdirectoryPath);
                foreach (string subDir in subDirectories)
                {
                    string subDirName = Path.GetFileName(subDir);
                    string destinationSubDir = Path.Combine(rootDirectory, subDirName);
                    
                    if (!Directory.Exists(destinationSubDir))
                    {
                        Directory.CreateDirectory(destinationSubDir);
                        Logger.LogInfo($"Created directory {subDirName} in root");
                    }
                    
                    CopyDirectoryRecursively(subDir, destinationSubDir);
                }
                
                Logger.LogInfo($"Successfully copied all files from {subdirectoryPath} to root directory");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error copying files from subdirectory to root: {ex.Message}");
                throw;
            }
        }

        private static void CopyDirectoryRecursively(string sourceDir, string destDir)
        {
            // Create destination directory if it doesn't exist
            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            // Copy all files
            string[] files = Directory.GetFiles(sourceDir);
            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(destDir, fileName);
                
                if (File.Exists(destFile))
                {
                    FileInfo sourceInfo = new FileInfo(file);
                    FileInfo destInfo = new FileInfo(destFile);
                    
                    if (sourceInfo.Length != destInfo.Length)
                    {
                        Logger.LogWarning($"Replacing {Path.Combine(Path.GetFileName(destDir), fileName)}: file size differs (original: {destInfo.Length} bytes, new: {sourceInfo.Length} bytes)");
                    }
                }
                
                File.Copy(file, destFile, true);
            }

            // Copy all subdirectories
            string[] subdirs = Directory.GetDirectories(sourceDir);
            foreach (string subdir in subdirs)
            {
                string subdirName = Path.GetFileName(subdir);
                string destSubdir = Path.Combine(destDir, subdirName);
                CopyDirectoryRecursively(subdir, destSubdir);
            }
        }


    }
}