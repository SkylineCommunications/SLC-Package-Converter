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
                return slnFiles.FirstOrDefault();
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


    }
}