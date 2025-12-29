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
            else if (args[i] == "--debug")
            {
                DebugMode = true;
            }
        }

        if (string.IsNullOrEmpty(SourceDirectory))
        {
            Console.WriteLine("Usage: SLC-Package-Converter.exe --sourceDir <SourceDirectory> [--destDir <DestinationDirectory>] [--solutionName <CustomName>] [--includeGitHubWorkflow <None|Basic|Complete>] [--branchName <BranchName>] [--preserveHistory] [--debug]");
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

            // If DestinationDirectory is not provided, generate a temporary directory
            if (string.IsNullOrEmpty(DestinationDirectory))
            {
                Logger.LogDebug("Creating new package project...");
                
                var currentSln = SolutionHelper.GetSolutionFile(SourceDirectory);
                string? currentSlnNameWithoutExtension = null;
                
                if (currentSln != null)
                {
                    currentSlnNameWithoutExtension = Path.GetFileNameWithoutExtension(currentSln);
                }
                
                // Determine the solution name and project name:
                // ProjectName: Always "Package"
                // SolutionName: Custom name from --solutionName, otherwise use source solution file name, or "Package" if no solution exists
                string packageProjectName = "Package";
                string solutionName = !string.IsNullOrEmpty(SolutionName) ? SolutionName : (currentSlnNameWithoutExtension ?? "Package");
                
                DestinationDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(DestinationDirectory);

                // Command to create a new project and solution in the destination directory
                string createProjectCommand =
                    $"cd \"{DestinationDirectory}\" && " +
                    $"dotnet new dataminer-package-project -o \"{packageProjectName}\" -n \"{packageProjectName}\" -auth \"\" -cdp true -I {IncludeGitHubWorkflow} --force && " +
                    $"dotnet new sln -n \"{solutionName}\" && " +
                    $"dotnet sln add \"{packageProjectName}/{packageProjectName}.csproj\"";
                
                CommandExecutor.ExecuteCommand(createProjectCommand);

                branchMode = true; // Enable branch mode
            }

            // Retrieve the solution files
            string? sourceSlnFile = SolutionHelper.GetSolutionFile(SourceDirectory);
            string? destSlnFile = SolutionHelper.GetSolutionFile(DestinationDirectory);

            // Process XML files in the source directory and copy other directories
            Logger.LogDebug("Processing XML files...");
            var processedFiles = XmlProcessor.ProcessXmlFiles(SourceDirectory, DestinationDirectory, destSlnFile);
            
            // Check if any files were processed
            // If no XML files were converted, there's no point in copying other files,
            // adding references, or creating a branch since the tool's purpose is XML conversion
            if (processedFiles.Count == 0)
            {
                Logger.LogWarning("No XML files were converted. Please verify that the source directory contains valid XML files or check the logs above for processing errors. The package converter completed successfully without making any changes.");
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
