using SLC_Package_Converter.Utilities;
using System.Xml.Linq;

class Program
{
    static void Main(string[] args)
    {
        // Parse command-line arguments
        string? SourceDirectory = null;
        string? DestinationDirectory = null;
        string IntegrationType = "Complete"; // Default value

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
            else if ((args[i] == "-I" || args[i] == "--integration") && i + 1 < args.Length)
            {
                IntegrationType = args[i + 1];
                i++; // Skip the value
            }
        }

        if (string.IsNullOrEmpty(SourceDirectory))
        {
            Console.WriteLine("Usage: SLC-Package-Converter.exe --sourceDir <SourceDirectory> [--destDir <DestinationDirectory>] [-I|--integration <None|Basic|Complete>]");
            Console.WriteLine("  -I, --integration  Type of integration (None, Basic, Complete). Default: Complete");
            return;
        }

        // Validate integration type
        string[] validIntegrationTypes = { "None", "Basic", "Complete" };
        if (!validIntegrationTypes.Contains(IntegrationType, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Invalid integration type '{IntegrationType}'. Valid options are: None, Basic, Complete");
            return;
        }

        string[] ExcludedDirs = { "CompanionFiles", "Internal", "Documentation", "Dlls" };
        string[] ExcludedSubDirs = { };
        string[] ExcludedFiles = { "AssemblyInfo.cs", "Jenkinsfile", "Directory.Build.props", "Directory.Build.targets" };
        bool branchMode = false;
        XNamespace Ns = "http://www.skyline.be/automation";

        try
        {
            // Log the start of the package conversion process
            Logger.LogInfo("Starting the package conversion process.");

            // Validate the existence of the source directory
            if (!Directory.Exists(SourceDirectory))
            {
                Logger.LogError("Source directory does not exist.");
                return;
            }

            // If DestinationDirectory is not provided, generate a temporary directory
            if (string.IsNullOrEmpty(DestinationDirectory))
            {
                var currentSln = SolutionHelper.GetSolutionFile(SourceDirectory);
                if (currentSln == null)
                {
                    throw new FileNotFoundException("Solution file not found.");
                }

                string currentSlnNameWithoutExtension = Path.GetFileNameWithoutExtension(currentSln);
                DestinationDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Logger.LogInfo("Destination Directory not specified. Creating a new branch with the new project.");
                Directory.CreateDirectory(DestinationDirectory);

                // Command to create a new project and solution in the destination directory
                string createProjectCommand =
                    $"cd \"{DestinationDirectory}\" && " +
                    $"dotnet new dataminer-package-project -o \"{currentSlnNameWithoutExtension}\" -auth \"\" -cdp true -I {IntegrationType} --force && " +
                    $"dotnet new sln -n \"{currentSlnNameWithoutExtension}\" && " +
                    $"dotnet sln add \"{currentSlnNameWithoutExtension}/{currentSlnNameWithoutExtension}.csproj\"";
                CommandExecutor.ExecuteCommand(createProjectCommand);

                branchMode = true; // Enable branch mode
            }

            // Retrieve the solution file from the source directory
            string? sourceSlnFile = SolutionHelper.GetSolutionFile(SourceDirectory);

            // Retrieve the solution file from the destination directory
            string? destSlnFile = SolutionHelper.GetSolutionFile(DestinationDirectory);

            // Process XML files in the source directory and copy other directories
            var processedFiles = XmlProcessor.ProcessXmlFiles(SourceDirectory, DestinationDirectory, destSlnFile);
            DirectoryHelper.CopyOtherDirectories(SourceDirectory, DestinationDirectory, ExcludedDirs, ExcludedSubDirs, ExcludedFiles, processedFiles);
            SolutionHelper.AddSharedProjectReferences(sourceSlnFile, destSlnFile);

            // If branch mode is enabled, create a branch and copy files
            if (branchMode)
            {
                BranchManager.CreateBranchAndCopyFiles(SourceDirectory, DestinationDirectory);
            }
        }
        catch (DirectoryNotFoundException ex)
        {
            // Handle directory not found exceptions
            Logger.LogError($"Directory not found: {ex.Message}");
        }
        catch (IOException ex)
        {
            // Handle I/O exceptions
            Logger.LogError($"I/O error: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            // Handle unauthorized access exceptions
            Logger.LogError($"Access denied: {ex.Message}");
        }
        catch (Exception ex)
        {
            // Handle any other unexpected exceptions
            Logger.LogError($"An unexpected error occurred: {ex.Message}");
        }
    }
}