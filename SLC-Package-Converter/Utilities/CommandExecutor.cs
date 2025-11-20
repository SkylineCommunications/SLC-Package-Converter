namespace SLC_Package_Converter.Utilities
{
    public static class CommandExecutor
    {
        // Executes dotnet commands to create and add a project to the solution.
        public static void ExecuteDotnetCommands(string templateName, string projectName, string? slnFile)
        {
            try
            {
                // Run the dotnet new command to create a new project
                string createProjectCommand = $"dotnet new {templateName} -o \"{projectName}\" -auth \"\" --force";
                ExecuteCommand(createProjectCommand);

                // Run the dotnet sln command to add the project to the solution
                string addProjectCommand = $"dotnet sln \"{slnFile}\" add \"{projectName}\"";
                ExecuteCommand(addProjectCommand);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error running dotnet commands: {ex.Message}");
                throw;
            }
        }

        public static string? ExecuteCommand(string command, bool returnOutput = false)
        {
            try
            {
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Directory.GetCurrentDirectory()
                };

                using (var process = System.Diagnostics.Process.Start(processStartInfo))
                {
                    if (process == null)
                    {
                        Logger.LogError($"Failed to start process for command '{command}'");
                        return null;
                    }

                    using (var reader = process.StandardOutput)
                    {
                        string output = reader.ReadToEnd();
                        
                        using (var errorReader = process.StandardError)
                        {
                            string error = errorReader.ReadToEnd();
                            
                            process.WaitForExit();
                            int exitCode = process.ExitCode;
                            
                            // Log only errors
                            if (!string.IsNullOrEmpty(error) || exitCode != 0)
                            {
                                Logger.LogError($"Command failed (exit code {exitCode}): {command}");
                                if (!string.IsNullOrEmpty(error))
                                {
                                    Logger.LogError($"Error output: {error}");
                                }
                            }
                        }
                        
                        if (returnOutput)
                        {
                            return output;
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Exception executing command '{command}': {ex.Message}");
                if (returnOutput)
                {
                    return null;
                }
                throw;
            }
        }
    }
}
