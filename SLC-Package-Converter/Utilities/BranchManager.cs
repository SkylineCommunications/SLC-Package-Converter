namespace SLC_Package_Converter.Utilities
{
    public static class BranchManager
    {
        // Creates a new branch and copies files from the destination to the source directory.
        public static void CreateBranchAndCopyFiles(string sourceDir, string destDir, string branchName = "converted-package", bool preserveHistory = false)
        {
            try
            {
                Logger.LogInfo($"Creating branch '{branchName}'");
                Logger.LogDebug($"Source directory: {sourceDir}");
                Logger.LogDebug($"Destination directory: {destDir}");
                Logger.LogDebug($"Branch name: {branchName}");
                Logger.LogDebug($"Preserve history: {preserveHistory}");
                Logger.LogDebug($"Current working directory (before change): {Directory.GetCurrentDirectory()}");
                
                Directory.SetCurrentDirectory(sourceDir);
                Logger.LogDebug($"Changed working directory to: {Directory.GetCurrentDirectory()}");

                string? currentBranch = null;
                
                // Create branch based on preserveHistory parameter
                if (!preserveHistory)
                {
                    Logger.LogDebug("Creating orphan branch (no history preservation)");
                    // Use orphan branch (original behavior)
                    CommandExecutor.ExecuteCommand($"git checkout --orphan {branchName}");
                    CommandExecutor.ExecuteCommand("git rm -rf .");
                    CommandExecutor.ExecuteCommand("git clean -fd");
                }
                else
                {
                    Logger.LogDebug("Creating branch with history preservation");
                    // Get current branch name first
                    currentBranch = GetCurrentBranch();
                    if (string.IsNullOrEmpty(currentBranch))
                    {
                        Logger.LogError("Could not determine current branch");
                        throw new InvalidOperationException("Could not determine current branch. Make sure you are in a git repository with a valid branch.");
                    }
                    
                    Logger.LogDebug($"Current branch: {currentBranch}");
                    
                    // Create branch from current branch to preserve git history
                    CommandExecutor.ExecuteCommand($"git checkout -b {branchName} {currentBranch}");
                    CommandExecutor.ExecuteCommand("git rm -rf .");
                    CommandExecutor.ExecuteCommand("git clean -fd");
                }

                int directoriesCopied = 0;
                int filesCopied = 0;
                
                foreach (string dirPath in Directory.GetDirectories(destDir, "*", SearchOption.AllDirectories))
                {
                    string targetDirPath = dirPath.Replace(destDir, sourceDir);
                    if (!Directory.Exists(targetDirPath))
                    {
                        Directory.CreateDirectory(targetDirPath);
                        directoriesCopied++;
                        Logger.LogDebug($"Created directory: {targetDirPath}");
                    }
                }
                
                Logger.LogDebug($"Total directories created: {directoriesCopied}");

                foreach (string filePath in Directory.GetFiles(destDir, "*.*", SearchOption.AllDirectories))
                {
                    string targetFilePath = filePath.Replace(destDir, sourceDir);
                    File.Copy(filePath, targetFilePath, true);
                    filesCopied++;
                }
                
                Logger.LogInfo($"Copied {filesCopied} files");
                Logger.LogDebug($"Total files copied: {filesCopied}");

                CommandExecutor.ExecuteCommand("git add .");
                
                string commitMessage = !preserveHistory 
                    ? $"Converted package using SLC-Package-Converter into {branchName} branch"
                    : $"Converted package using SLC-Package-Converter into {branchName} branch (from {currentBranch})";
                    
                Logger.LogDebug($"Commit message: {commitMessage}");
                CommandExecutor.ExecuteCommand($"git commit -m \"{commitMessage}\"");

                string successMessage = !preserveHistory
                    ? $"Created orphan branch '{branchName}'"
                    : $"Created branch '{branchName}' from '{currentBranch}'";
                    
                Logger.LogInfo(successMessage);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error creating branch: {ex.Message}");
                Logger.LogDebug($"Exception Type: {ex.GetType().Name}");
                Logger.LogDebug($"Exception Message: {ex.Message}");
                Logger.LogDebug($"Stack Trace:{Environment.NewLine}{ex.StackTrace}");
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
