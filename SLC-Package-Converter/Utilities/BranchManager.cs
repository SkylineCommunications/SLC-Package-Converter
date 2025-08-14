namespace SLC_Package_Converter.Utilities
{
    public static class BranchManager
    {
        // Creates a new branch and copies files from the destination to the source directory.
        public static void CreateBranchAndCopyFiles(string sourceDir, string destDir, string branchName = "converted-package", string? baseBranch = null)
        {
            try
            {
                Directory.SetCurrentDirectory(sourceDir);

                // Create branch based on baseBranch parameter
                if (string.IsNullOrEmpty(baseBranch))
                {
                    // Use orphan branch (original behavior)
                    CommandExecutor.ExecuteCommand($"git checkout --orphan {branchName}");
                    CommandExecutor.ExecuteCommand("git rm -rf .");
                    CommandExecutor.ExecuteCommand("git clean -fd");
                }
                else
                {
                    // Create branch from base branch to preserve git history
                    CommandExecutor.ExecuteCommand($"git checkout -b {branchName} {baseBranch}");
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
                
                string commitMessage = string.IsNullOrEmpty(baseBranch) 
                    ? $"Converted package using SLC-Package-Converter into {branchName} branch"
                    : $"Converted package using SLC-Package-Converter into {branchName} branch (from {baseBranch})";
                    
                CommandExecutor.ExecuteCommand($"git commit -m \"{commitMessage}\"");

                string successMessage = string.IsNullOrEmpty(baseBranch)
                    ? $"Successfully created orphan branch '{branchName}' and copied files."
                    : $"Successfully created branch '{branchName}' from '{baseBranch}' and copied files.";
                    
                Logger.LogInfo(successMessage);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error creating branch and copying files: {ex.Message}");
                throw;
            }
        }
    }
}
