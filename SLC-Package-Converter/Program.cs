using SLC_Package_Converter.Utilities;
using System.Xml.Linq;

class Program
{
    static void Main(string[] args)
    {
        // Parse command-line arguments
        string? SourceDirectory = null;
        string? DestinationDirectory = null;
        string? SolutionName = null; // Optional: custom solution name
        string IncludeGitHubWorkflow = "Complete"; // Default value
        string BranchName = "converted-package"; // Default value
        bool PreserveHistory = false; // Default value (false means use orphan branch)

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--sourceDir" && i + 1 < args.Length)
            {
                SourceDirectory = args[i + 1];
                i++; // Skip the value
            }
            else if (args[i] == "--destDir" && i + 1 < args.Length)
            {
                DestinationDirectory = args[i + 1];
                i++; // Skip the value
            }
            else if (args[i] == "--solutionName" && i + 1 < args.Length)
            {
                SolutionName = args[i + 1];
                if (string.IsNullOrWhiteSpace(SolutionName))
                {
                    Console.WriteLine("Error: --solutionName requires a non-empty value.");
                    return;
                }
                i++; // Skip the value
            }
            else if (args[i] == "--solutionName")
            {
                Console.WriteLine("Error: --solutionName requires a value.");
                return;
            }
            else if (args[i] == "--includeGitHubWorkflow" && i + 1 < args.Length)
            {
                IncludeGitHubWorkflow = args[i + 1];
                i++; // Skip the value
            }
            else if (args[i] == "--branchName" && i + 1 < args.Length)
            {
                BranchName = args[i + 1];
                i++; // Skip the value
            }
            else if (args[i] == "--preserveHistory")
            {
                PreserveHistory = true;
            }
        }

        if (string.IsNullOrEmpty(SourceDirectory))
        {
            Console.WriteLine("Usage: SLC-Package-Converter.exe --sourceDir <SourceDirectory> [--destDir <DestinationDirectory>] [--solutionName <CustomName>] [--includeGitHubWorkflow <None|Basic|Complete>] [--branchName <BranchName>] [--preserveHistory]");
            return;
        }

        // Validate GitHub workflow type
        string[] validWorkflowTypes = { "None", "Basic", "Complete" };
        if (!validWorkflowTypes.Contains(IncludeGitHubWorkflow, StringComparer.Ordinal))
        {
            Console.WriteLine($"Invalid GitHub workflow type '{IncludeGitHubWorkflow}'. Valid options are: None, Basic, Complete");
            return;
        }

        string[] ExcludedDirs = { "CompanionFiles", "Internal", "Documentation", "AutomationScript_ClassLibrary" };
        string[] ExcludedSubDirs = { };
        string[] ExcludedFiles = { "AssemblyInfo.cs", "Jenkinsfile", "Directory.Build.props", "Directory.Build.targets" };
        bool branchMode = false;
        XNamespace Ns = "http://www.skyline.be/automation";

        try
        {
            // Log the start of the package conversion process
            Logger.LogInfo("=== SLC-Package-Converter Started ===");
            Logger.LogInfo($"Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
            Logger.LogInfo($"Execution Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Logger.LogInfo($"Current Working Directory: {Directory.GetCurrentDirectory()}");
            Logger.LogInfo($"Executable Location: {System.Reflection.Assembly.GetExecutingAssembly().Location}");
            
            // Log environment information
            Logger.LogInfo("=== Environment Information ===");
            Logger.LogInfo($"OS Version: {Environment.OSVersion}");
            Logger.LogInfo($".NET Runtime Version: {Environment.Version}");
            Logger.LogInfo($"Machine Name: {Environment.MachineName}");
            Logger.LogInfo($"User: {Environment.UserName}");
            Logger.LogInfo($"Process ID: {Environment.ProcessId}");
            
            // Log dotnet version
            try
            {
                var dotnetVersion = CommandExecutor.ExecuteCommand("dotnet --version", returnOutput: true);
                Logger.LogInfo($"Dotnet SDK Version: {dotnetVersion?.Trim() ?? "Unable to determine"}");
            }
            catch
            {
                Logger.LogWarning("Unable to determine dotnet SDK version");
            }
            
            // Log all command-line arguments
            Logger.LogInfo("=== Command-Line Arguments ===");
            Logger.LogInfo($"Total arguments: {args.Length}");
            if (args.Length > 0)
            {
                Logger.LogInfo("Arguments received:");
                for (int i = 0; i < args.Length; i++)
                {
                    Logger.LogInfo($"  [{i}]: {args[i]}");
                }
            }
            else
            {
                Logger.LogInfo("No arguments provided");
            }
            
            // Log parsed parameters
            Logger.LogInfo("=== Parsed Parameters ===");
            Logger.LogInfo($"SourceDirectory: {SourceDirectory ?? "(not set)"}");
            Logger.LogInfo($"DestinationDirectory: {DestinationDirectory ?? "(not set)"}");
            Logger.LogInfo($"SolutionName: {SolutionName ?? "(not set)"}");
            Logger.LogInfo($"IncludeGitHubWorkflow: {IncludeGitHubWorkflow}");
            Logger.LogInfo($"BranchName: {BranchName}");
            Logger.LogInfo($"PreserveHistory: {PreserveHistory}");
            
            Logger.LogInfo("=== Starting Package Conversion Process ===");

            // Validate the existence of the source directory
            if (!Directory.Exists(SourceDirectory))
            {
                Logger.LogError($"Source directory does not exist: {SourceDirectory}");
                return;
            }
            Logger.LogInfo($"Source directory validated: {SourceDirectory}");

            // If DestinationDirectory is not provided, generate a temporary directory
            if (string.IsNullOrEmpty(DestinationDirectory))
            {
                Logger.LogInfo("=== Destination Directory Not Specified - Creating New Package Project ===");
                
                var currentSln = SolutionHelper.GetSolutionFile(SourceDirectory);
                string? currentSlnNameWithoutExtension = null;
                
                if (currentSln != null)
                {
                    currentSlnNameWithoutExtension = Path.GetFileNameWithoutExtension(currentSln);
                    Logger.LogInfo($"Found source solution file: {currentSln}");
                    Logger.LogInfo($"Source solution name (without extension): {currentSlnNameWithoutExtension}");
                }
                else
                {
                    Logger.LogInfo("No solution file found in source directory");
                }
                
                // Determine the solution name and project name:
                // ProjectName: Always "Package"
                // SolutionName: Custom name from --solutionName, otherwise use source solution file name, or "Package" if no solution exists
                string packageProjectName = "Package";
                string solutionName = !string.IsNullOrEmpty(SolutionName) ? SolutionName : (currentSlnNameWithoutExtension ?? "Package");
                
                Logger.LogInfo($"Project Name: {packageProjectName}");
                Logger.LogInfo($"Solution Name: {solutionName}");
                
                DestinationDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Logger.LogInfo($"Generated temporary destination directory: {DestinationDirectory}");
                
                Directory.CreateDirectory(DestinationDirectory);
                Logger.LogInfo($"Created destination directory: {DestinationDirectory}");

                // Command to create a new project and solution in the destination directory
                string createProjectCommand =
                    $"cd \"{DestinationDirectory}\" && " +
                    $"dotnet new dataminer-package-project -o \"{packageProjectName}\" -n \"{packageProjectName}\" -auth \"\" -cdp true -I {IncludeGitHubWorkflow} --force && " +
                    $"dotnet new sln -n \"{solutionName}\" && " +
                    $"dotnet sln add \"{packageProjectName}/{packageProjectName}.csproj\"";
                    
                Logger.LogInfo("=== Creating Package Project ===");
                Logger.LogInfo($"Command to execute: {createProjectCommand}");
                
                CommandExecutor.ExecuteCommand(createProjectCommand);

                branchMode = true; // Enable branch mode
                Logger.LogInfo("Branch mode enabled");
            }
            else
            {
                Logger.LogInfo($"Using provided destination directory: {DestinationDirectory}");
            }

            // Retrieve the solution file from the source directory
            Logger.LogInfo("=== Retrieving Solution Files ===");
            string? sourceSlnFile = SolutionHelper.GetSolutionFile(SourceDirectory);
            if (sourceSlnFile != null)
            {
                Logger.LogInfo($"Source solution file: {sourceSlnFile}");
            }
            else
            {
                Logger.LogInfo("No source solution file found");
            }

            // Retrieve the solution file from the destination directory
            string? destSlnFile = SolutionHelper.GetSolutionFile(DestinationDirectory);
            if (destSlnFile != null)
            {
                Logger.LogInfo($"Destination solution file: {destSlnFile}");
            }
            else
            {
                Logger.LogInfo("No destination solution file found");
            }

            // Process XML files in the source directory and copy other directories
            Logger.LogInfo("=== Processing XML Files ===");
            var processedFiles = XmlProcessor.ProcessXmlFiles(SourceDirectory, DestinationDirectory, destSlnFile);
            Logger.LogInfo($"Processed {processedFiles.Count} XML/csproj files");
            
            Logger.LogInfo("=== Copying Other Directories ===");
            DirectoryHelper.CopyOtherDirectories(SourceDirectory, DestinationDirectory, ExcludedDirs, ExcludedSubDirs, ExcludedFiles, processedFiles);
            Logger.LogInfo("Directory copying completed");
            
            Logger.LogInfo("=== Adding Shared Project References ===");
            SolutionHelper.AddSharedProjectReferences(sourceSlnFile, destSlnFile);

            // If branch mode is enabled, create a branch and copy files
            if (branchMode)
            {
                Logger.LogInfo("=== Creating Branch and Copying Files ===");
                Logger.LogInfo($"Branch name: {BranchName}");
                Logger.LogInfo($"Preserve history: {PreserveHistory}");
                BranchManager.CreateBranchAndCopyFiles(SourceDirectory, DestinationDirectory, BranchName, PreserveHistory);
            }
            
            Logger.LogInfo("=== Package Conversion Completed Successfully ===");
        }
        catch (DirectoryNotFoundException ex)
        {
            // Handle directory not found exceptions
            Logger.LogError($"Directory not found: {ex.Message}");
            Environment.Exit(1);
        }
        catch (IOException ex)
        {
            // Handle I/O exceptions
            Logger.LogError($"I/O error: {ex.Message}");
            Environment.Exit(1);
        }
        catch (UnauthorizedAccessException ex)
        {
            // Handle unauthorized access exceptions
            Logger.LogError($"Access denied: {ex.Message}");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            // Handle any other unexpected exceptions
            Logger.LogError($"An unexpected error occurred: {ex.Message}");
            Environment.Exit(1);
        }
    }
}