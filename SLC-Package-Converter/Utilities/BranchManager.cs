namespace SLC_Package_Converter.Utilities
{
    public static class BranchManager
    {
        // Creates a new branch and copies files from the destination to the source directory.
        public static void CreateBranchAndCopyFiles(string sourceDir, string destDir, string branchName = "converted-package", bool preserveHistory = false)
        {
            try
            {
                Directory.SetCurrentDirectory(sourceDir);

                string? currentBranch = null;
                
                // Create branch based on preserveHistory parameter
                if (!preserveHistory)
                {
                    // Use orphan branch (original behavior)
                    CommandExecutor.ExecuteCommand($"git checkout --orphan {branchName}");
                    CommandExecutor.ExecuteCommand("git rm -rf .");
                    CommandExecutor.ExecuteCommand("git clean -fd");
                }
                else
                {
                    // Get current branch name first
                    currentBranch = GetCurrentBranch();
                    if (string.IsNullOrEmpty(currentBranch))
                    {
                        throw new InvalidOperationException("Could not determine current branch. Make sure you are in a git repository with a valid branch.");
                    }
                    
                    // Create branch from current branch to preserve git history
                    CommandExecutor.ExecuteCommand($"git checkout -b {branchName} {currentBranch}");
                    CommandExecutor.ExecuteCommand("git rm -rf .");
                    CommandExecutor.ExecuteCommand("git clean -fd");
                }

                foreach (string dirPath in Directory.GetDirectories(destDir, "*", SearchOption.AllDirectories))
                {
                    string targetDirPath = dirPath.Replace(destDir, sourceDir);
                    if (!Directory.Exists(targetDirPath))
                    {
                        Directory.CreateDirectory(targetDirPath);
                    }
                }

                foreach (string filePath in Directory.GetFiles(destDir, "*.*", SearchOption.AllDirectories))
                {
                    string targetFilePath = filePath.Replace(destDir, sourceDir);
                    File.Copy(filePath, targetFilePath, true);
                }

                CommandExecutor.ExecuteCommand("git add .");
                
                string commitMessage = !preserveHistory 
                    ? $"Converted package using SLC-Package-Converter into {branchName} branch"
                    : $"Converted package using SLC-Package-Converter into {branchName} branch (from {currentBranch})";
                    
                CommandExecutor.ExecuteCommand($"git commit -m \"{commitMessage}\"");

                string successMessage = !preserveHistory
                    ? $"Successfully created orphan branch '{branchName}' and copied files."
                    : $"Successfully created branch '{branchName}' from '{currentBranch}' and copied files.";
                    
                Logger.LogInfo(successMessage);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error creating branch and copying files: {ex.Message}");
                throw;
            }
        }

        private static string? GetCurrentBranch()
        {
            try
            {
                // Use git command to get current branch name
                var result = CommandExecutor.ExecuteCommandWithOutput("git branch --show-current");
                return result?.Trim();
            }
            catch
            {
                return null;
            }
        }
    }
}
