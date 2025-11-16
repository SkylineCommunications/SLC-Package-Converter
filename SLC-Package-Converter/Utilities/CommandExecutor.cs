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
                // Log the command being executed for debugging purposes
                Logger.LogInfo($"Executing command: {command}");
                Logger.LogInfo($"Working directory: {Directory.GetCurrentDirectory()}");

                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
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
                            
                            // Log exit code for debugging
                            if (exitCode != 0)
                            {
                                Logger.LogError($"Command exited with code {exitCode}");
                            }
                            
                            // Log both stdout and stderr if there was an error (non-zero exit code or stderr output)
                            if (!string.IsNullOrEmpty(error) || exitCode != 0)
                            {
                                if (!string.IsNullOrEmpty(output))
                                {
                                    Logger.LogInfo($"Command output (stdout):{Environment.NewLine}{output}");
                                }
                                if (!string.IsNullOrEmpty(error))
                                {
                                    Logger.LogError($"Command error output (stderr):{Environment.NewLine}{error}");
                                }
                            }
                        }
                        
                        if (returnOutput)
                        {
                            return output;
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(output))
                            {
                                // Logger.LogInfo(output);
                            }
                            return null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error executing command '{command}': {ex.Message}");
                if (returnOutput)
                {
                    return null;
                }
                throw;
            }
        }
    }
}
