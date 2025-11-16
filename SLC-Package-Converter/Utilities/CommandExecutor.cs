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
                Logger.LogInfo("=== Executing Command ===");
                Logger.LogInfo($"Command: {command}");
                Logger.LogInfo($"Working directory: {Directory.GetCurrentDirectory()}");
                Logger.LogInfo($"Return output: {returnOutput}");
                Logger.LogInfo($"Shell: cmd.exe");
                Logger.LogInfo($"Shell arguments: /c {command}");

                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                Logger.LogInfo($"Process start info configured:");
                Logger.LogInfo($"  FileName: {processStartInfo.FileName}");
                Logger.LogInfo($"  Arguments: {processStartInfo.Arguments}");
                Logger.LogInfo($"  UseShellExecute: {processStartInfo.UseShellExecute}");
                Logger.LogInfo($"  CreateNoWindow: {processStartInfo.CreateNoWindow}");
                Logger.LogInfo($"  RedirectStandardOutput: {processStartInfo.RedirectStandardOutput}");
                Logger.LogInfo($"  RedirectStandardError: {processStartInfo.RedirectStandardError}");

                using (var process = System.Diagnostics.Process.Start(processStartInfo))
                {
                    if (process == null)
                    {
                        Logger.LogError($"Failed to start process for command '{command}'");
                        return null;
                    }
                    
                    Logger.LogInfo($"Process started successfully (PID: {process.Id})");

                    using (var reader = process.StandardOutput)
                    {
                        string output = reader.ReadToEnd();
                        
                        using (var errorReader = process.StandardError)
                        {
                            string error = errorReader.ReadToEnd();
                            
                            process.WaitForExit();
                            int exitCode = process.ExitCode;
                            
                            // Log execution results
                            Logger.LogInfo($"=== Command Execution Results ===");
                            Logger.LogInfo($"Exit code: {exitCode}");
                            Logger.LogInfo($"Standard output length: {output?.Length ?? 0} characters");
                            Logger.LogInfo($"Standard error length: {error?.Length ?? 0} characters");
                            
                            // Log exit code for debugging
                            if (exitCode != 0)
                            {
                                Logger.LogError($"Command exited with non-zero code: {exitCode}");
                            }
                            else
                            {
                                Logger.LogInfo("Command completed successfully (exit code 0)");
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
                            else if (!string.IsNullOrEmpty(output) && returnOutput)
                            {
                                Logger.LogInfo($"Command output:{Environment.NewLine}{output}");
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
                Logger.LogError($"=== Exception during command execution ===");
                Logger.LogError($"Command: {command}");
                Logger.LogError($"Exception Type: {ex.GetType().Name}");
                Logger.LogError($"Exception Message: {ex.Message}");
                Logger.LogError($"Stack Trace:{Environment.NewLine}{ex.StackTrace}");
                if (returnOutput)
                {
                    return null;
                }
                throw;
            }
        }
    }
}
