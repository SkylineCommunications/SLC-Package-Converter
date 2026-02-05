using System.Collections.Generic;

namespace SLC_Package_Converter.Utilities
{
    public static class SolutionHelper
    {
        public static string? GetSolutionFile(string directory)
        {
            try
            {
                // Get the first .sln file in the directory (searches recursively)
                string[] slnFiles = Directory.GetFiles(directory, "*.sln", SearchOption.AllDirectories);
                if (slnFiles.Length > 0)
                {
                    Logger.LogDebug($"Found {slnFiles.Length} solution file(s) in directory and subdirectories");
                    return slnFiles[0];
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
                // If source solution file is null or empty, just log and return (no shared projects to copy)
                if (string.IsNullOrWhiteSpace(sourceSlnFile))
                {
                    Logger.LogDebug("No source solution file provided. Skipping shared project references.");
                    return;
                }
                
                if (string.IsNullOrWhiteSpace(destSlnFile))
                {
                    throw new ArgumentException("Destination solution file path is null or empty.");
                }

                // Read all lines from the source solution file
                var sourceLines = File.ReadAllLines(sourceSlnFile);

                // Filter lines containing ".shproj"
                var sharedProjectLines = sourceLines.Where(line => line.Contains(".shproj")).ToList();

                if (!sharedProjectLines.Any())
                {
                    Logger.LogDebug("No shared project references found in the source solution file.");
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
                
                Logger.LogDebug("Shared project references successfully added to the destination solution file.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error adding shared project references: {ex.Message}");
                throw;
            }
        }

        public static bool IsProjectInSolution(string sourceDir, string fileName)
        {
            try
            {
                // Get all .sln files in the source directory and subdirectories
                string[] slnFiles = Directory.GetFiles(sourceDir, "*.sln", SearchOption.AllDirectories);
                
                if (slnFiles.Length == 0)
                {
                    // If no solution file exists, we can't verify - assume the project should be processed
                    Logger.LogWarning($"No solution file found in source directory or subdirectories. Cannot verify if '{fileName}' should be processed. Proceeding with processing.");
                    return true;
                }

                // Search in all solution files
                foreach (string slnFile in slnFiles)
                {
                    var slnContent = File.ReadAllText(slnFile);
                    
                    // Check if the filename exists in any solution file
                    if (slnContent.Contains(fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.LogDebug($"File '{fileName}' found in solution file: {Path.GetFileName(slnFile)}");
                        return true;
                    }
                }
                
                // File not found in any solution file
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error checking if file '{fileName}' exists in solution files: {ex.Message}");
                // If we can't check, assume it should be processed
                return true;
            }
        }

    }
}