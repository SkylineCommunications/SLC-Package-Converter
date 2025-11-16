namespace SLC_Package_Converter.Utilities
{
    public static class BranchManager
    {
        // Creates a new branch and copies files from the destination to the source directory.
        public static void CreateBranchAndCopyFiles(string sourceDir, string destDir, string branchName = "converted-package", bool preserveHistory = false)
        {
            try
            {
                Logger.LogInfo("=== Creating Branch and Copying Files ===");
                Logger.LogInfo($"Source directory: {sourceDir}");
                Logger.LogInfo($"Destination directory: {destDir}");
                Logger.LogInfo($"Branch name: {branchName}");
                Logger.LogInfo($"Preserve history: {preserveHistory}");
                Logger.LogInfo($"Current working directory (before change): {Directory.GetCurrentDirectory()}");
                
                Directory.SetCurrentDirectory(sourceDir);
                Logger.LogInfo($"Changed working directory to: {Directory.GetCurrentDirectory()}");

                string? currentBranch = null;
                
                // Create branch based on preserveHistory parameter
                if (!preserveHistory)
                {
                    Logger.LogInfo("Creating orphan branch (no history preservation)");
                    // Use orphan branch (original behavior)
                    CommandExecutor.ExecuteCommand($"git checkout --orphan {branchName}");
                    CommandExecutor.ExecuteCommand("git rm -rf .");
                    CommandExecutor.ExecuteCommand("git clean -fd");
                }
                else
                {
                    Logger.LogInfo("Creating branch with history preservation");
                    // Get current branch name first
                    currentBranch = GetCurrentBranch();
                    if (string.IsNullOrEmpty(currentBranch))
                    {
                        Logger.LogError("Could not determine current branch");
                        throw new InvalidOperationException("Could not determine current branch. Make sure you are in a git repository with a valid branch.");
                    }
                    
                    Logger.LogInfo($"Current branch: {currentBranch}");
                    
                    // Create branch from current branch to preserve git history
                    CommandExecutor.ExecuteCommand($"git checkout -b {branchName} {currentBranch}");
                    CommandExecutor.ExecuteCommand("git rm -rf .");
                    CommandExecutor.ExecuteCommand("git clean -fd");
                }

                Logger.LogInfo("=== Copying directories and files ===");
                int directoriesCopied = 0;
                int filesCopied = 0;
                
                foreach (string dirPath in Directory.GetDirectories(destDir, "*", SearchOption.AllDirectories))
                {
                    string targetDirPath = dirPath.Replace(destDir, sourceDir);
                    if (!Directory.Exists(targetDirPath))
                    {
                        Directory.CreateDirectory(targetDirPath);
                        directoriesCopied++;
                        Logger.LogInfo($"Created directory: {targetDirPath}");
                    }
                }
                
                Logger.LogInfo($"Total directories created: {directoriesCopied}");

                foreach (string filePath in Directory.GetFiles(destDir, "*.*", SearchOption.AllDirectories))
                {
                    string targetFilePath = filePath.Replace(destDir, sourceDir);
                    File.Copy(filePath, targetFilePath, true);
                    filesCopied++;
                }
                
                Logger.LogInfo($"Total files copied: {filesCopied}");

                Logger.LogInfo("=== Committing changes ===");
                CommandExecutor.ExecuteCommand("git add .");
                
                string commitMessage = !preserveHistory 
                    ? $"Converted package using SLC-Package-Converter into {branchName} branch"
                    : $"Converted package using SLC-Package-Converter into {branchName} branch (from {currentBranch})";
                    
                Logger.LogInfo($"Commit message: {commitMessage}");
                CommandExecutor.ExecuteCommand($"git commit -m \"{commitMessage}\"");

                string successMessage = !preserveHistory
                    ? $"Successfully created orphan branch '{branchName}' and copied files."
                    : $"Successfully created branch '{branchName}' from '{currentBranch}' and copied files.";
                    
                Logger.LogInfo(successMessage);
            }
            catch (Exception ex)
            {
                Logger.LogError($"=== Error creating branch and copying files ===");
                Logger.LogError($"Exception Type: {ex.GetType().Name}");
                Logger.LogError($"Exception Message: {ex.Message}");
                Logger.LogError($"Stack Trace:{Environment.NewLine}{ex.StackTrace}");
                throw;
            }
        }

        private static string? GetCurrentBranch()
        {
            try
            {
                // Use git command to get current branch name
                var result = CommandExecutor.ExecuteCommand("git branch --show-current", returnOutput: true);
                return result?.Trim();
            }
            catch
            {
                return null;
            }
        }
    }
}
