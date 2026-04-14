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
        bool DebugMode = false; // Default value
        string SolutionFormat = "slnx"; // Default value

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
            else if (args[i] == "--solutionFormat" && i + 1 < args.Length)
            {
                SolutionFormat = args[i + 1];
                i++; // Skip the value
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
            else if (args[i] == "--debug")
            {
                DebugMode = true;
            }
        }

        if (string.IsNullOrEmpty(SourceDirectory))
        {
            Console.WriteLine("Usage: SLC-Package-Converter.exe --sourceDir <SourceDirectory> [--destDir <DestinationDirectory>] [--solutionName <CustomName>] [--solutionFormat <slnx|sln>] [--includeGitHubWorkflow <None|Basic|Complete>] [--branchName <BranchName>] [--preserveHistory] [--debug]");
            return;
        }

        // Set debug mode in Logger
        Logger.DebugMode = DebugMode;

        // Validate GitHub workflow type
        string[] validWorkflowTypes = { "None", "Basic", "Complete" };
        if (!validWorkflowTypes.Contains(IncludeGitHubWorkflow, StringComparer.Ordinal))
        {
            Console.WriteLine($"Invalid GitHub workflow type '{IncludeGitHubWorkflow}'. Valid options are: None, Basic, Complete");
            return;
        }

        string[] validSolutionFormats = { "slnx", "sln" };
        if (!validSolutionFormats.Contains(SolutionFormat, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Invalid solution format '{SolutionFormat}'. Valid options are: slnx, sln");
            return;
        }

        string[] ExcludedDirs = { "CompanionFiles", "Internal", "Documentation", "AutomationScript_ClassLibrary" };
        string[] ExcludedSubDirs = { };
        string[] ExcludedFiles = { "AssemblyInfo.cs", "Jenkinsfile", "Directory.Build.props", "Directory.Build.targets" };
        bool branchMode = false;
        XNamespace Ns = "http://www.skyline.be/automation";

        try
        {
            Logger.LogDebug("Starting SLC-Package-Converter...");

            // Validate the existence of the source directory
            if (!Directory.Exists(SourceDirectory))
            {
                Logger.LogError($"Source directory does not exist: {SourceDirectory}");
                return;
            }

            var sourceSlnFile = SolutionHelper.GetSolutionFile(SourceDirectory);

            // Determine if the destination directory is missing or empty
            bool destinationDirMissingOrEmpty;
            if (string.IsNullOrEmpty(DestinationDirectory))
            {
                // Destination directory not provided
                destinationDirMissingOrEmpty = true;
            }
            else if (!Directory.Exists(DestinationDirectory))
            {
                // Destination directory does not exist
                destinationDirMissingOrEmpty = true;
            }
            else
            {
                // Destination directory exists - check if it's empty
                destinationDirMissingOrEmpty = !Directory.EnumerateFileSystemEntries(DestinationDirectory).Any();
            }

            // If DestinationDirectory is not provided, generate a temporary directory
            // If DestinationDirectory exists but is empty, treat it as a new package project
            // If DestinationDirectory does not exist, create it and add a new package project
            if (destinationDirMissingOrEmpty)
            {
                Logger.LogDebug("Creating new package project...");

                string? currentSlnNameWithoutExtension = null;

                if (sourceSlnFile != null)
                {
                    currentSlnNameWithoutExtension = Path.GetFileNameWithoutExtension(sourceSlnFile);
                }

                // Determine the solution name and project name:
                // ProjectName: Always "Package"
                // SolutionName: Custom name from --solutionName, otherwise use source solution file name, or "Package" if no solution exists
                string packageProjectName = "Package";
                string solutionName = !string.IsNullOrEmpty(SolutionName) ? SolutionName : (currentSlnNameWithoutExtension ?? "Package");

                if (string.IsNullOrEmpty(DestinationDirectory))
                {
                    DestinationDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                    Directory.CreateDirectory(DestinationDirectory);
                }
                else if (!Directory.Exists(DestinationDirectory))
                {
                    Directory.CreateDirectory(DestinationDirectory);
                }

                // Command to create a new project and solution in the destination directory
                // Starting with .NET 10, dotnet new sln can create .slnx files by default
                // Only specify --format when explicitly requesting .sln
                string formatArgument = SolutionFormat.Equals("sln", StringComparison.OrdinalIgnoreCase) ? "--format sln" : string.Empty;
                string createProjectCommand =
                    $"cd \"{DestinationDirectory}\" && " +
                    $"dotnet new dataminer-package-project -o \"{packageProjectName}\" -n \"{packageProjectName}\" -auth \"\" -cdp true -I {IncludeGitHubWorkflow} --force && " +
                    $"dotnet new sln -n \"{solutionName}\" {formatArgument}".TrimEnd() + " && " +
                    $"dotnet sln add \"{packageProjectName}/{packageProjectName}.csproj\"";

                var output = CommandExecutor.ExecuteCommand(createProjectCommand, returnOutput: true);
                
                // Check if the command failed due to --format option not being available
                if (!string.IsNullOrEmpty(formatArgument) && output != null && output.Contains("'--format' is not a valid option"))
                {
                    Logger.LogDebug("--format option not available, retrying without it...");
                    
                    // Retry without --format option
                    createProjectCommand =
                        $"cd \"{DestinationDirectory}\" && " +
                        $"dotnet new dataminer-package-project -o \"{packageProjectName}\" -n \"{packageProjectName}\" -auth \"\" -cdp true -I {IncludeGitHubWorkflow} --force && " +
                        $"dotnet new sln -n \"{solutionName}\" && " +
                        $"dotnet sln add \"{packageProjectName}/{packageProjectName}.csproj\"";
                    
                    CommandExecutor.ExecuteCommand(createProjectCommand);
                }

                branchMode = true; // Enable branch mode
            }

            // At this point, DestinationDirectory is guaranteed to be set
            if (string.IsNullOrEmpty(DestinationDirectory))
            {
                Logger.LogError("Destination directory could not be determined.");
                return;
            }

            // Retrieve the solution file from the destination directory
            string? destSlnFile = SolutionHelper.GetSolutionFile(DestinationDirectory);

            // Process XML files in the source directory and copy other directories
                Logger.LogDebug("Processing XML files...");
            var (processedFiles, convertedProjectCount) = XmlProcessor.ProcessXmlFiles(SourceDirectory, DestinationDirectory, destSlnFile);
            
            // Check if any projects were converted
            // If no XML files were converted, there's no point in copying other files,
            // adding references, or creating a branch since the tool's purpose is XML conversion
            if (convertedProjectCount == 0)
            {
                Logger.LogWarning("No projects were converted. Please verify that the source directory contains valid XML files or check the logs above for processing errors. The package converter completed successfully without making any changes.");
                return;
            }

            Logger.LogDebug("Copying other directories...");
            DirectoryHelper.CopyOtherDirectories(SourceDirectory, DestinationDirectory, ExcludedDirs, ExcludedSubDirs, ExcludedFiles, processedFiles);
            
            Logger.LogDebug("Adding shared project references...");
            SolutionHelper.AddSharedProjectReferences(sourceSlnFile, destSlnFile);

            // If branch mode is enabled, create a branch and copy files
            if (branchMode)
            {
                Logger.LogDebug("Creating branch and copying files...");
                BranchManager.CreateBranchAndCopyFiles(SourceDirectory, DestinationDirectory, BranchName, PreserveHistory);
            }
            
            Logger.LogInfo("Package conversion completed successfully!");
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
