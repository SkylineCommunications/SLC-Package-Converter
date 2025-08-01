namespace SLC_Package_Converter.Utilities
{
    public static class BranchManager
    {
        // Creates a new branch and copies files from the destination to the source directory.
        public static void CreateBranchAndCopyFiles(string sourceDir, string destDir, string branchName = "converted-package")
        {
            try
            {
                Directory.SetCurrentDirectory(sourceDir);

                CommandExecutor.ExecuteCommand($"git checkout --orphan {branchName}");
                CommandExecutor.ExecuteCommand("git rm -rf .");
                CommandExecutor.ExecuteCommand("git clean -fd");

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
                CommandExecutor.ExecuteCommand($"git commit -m \"Copied files from destination directory to new branch {branchName}\"");

                Logger.LogInfo($"Successfully created branch '{branchName}' and copied files.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error creating branch and copying files: {ex.Message}");
                throw;
            }
        }
    }
}
