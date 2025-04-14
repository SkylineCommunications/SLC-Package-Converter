using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

// Define constants and variables
const string SourceDirectory = @"C:\GIT\SLC-AS-MediaOps";
string DestinationDirectory = @"";
string[] ExcludedDirs = { "CompanionFiles", "Internal", "Documentation", "Dlls" };
string[] ExcludedSubDirs = { };
string[] ExcludedFiles = { "AssemblyInfo.cs" };
bool branchMode = false;
XNamespace Ns = "http://www.skyline.be/automation";

// List to store project names
List<string> ProjectNames = new List<string>();

try
{
    LogInfo("Starting the package conversion process.");

    if (string.IsNullOrEmpty(DestinationDirectory))
    {
        var currentSln = GetSolutionFile(SourceDirectory);
        if (currentSln == null)
        {
            throw new FileNotFoundException("Solution file not found.");
        }

        string currentSlnNameWithoutExtension = Path.GetFileNameWithoutExtension(currentSln);
        DestinationDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        LogInfo($"Destination Directory not specified. Creating a new branch with the new project.");
        Directory.CreateDirectory(DestinationDirectory);

        string createProjectCommand =
            $"cd \"{DestinationDirectory}\" && " +
            $"dotnet new dataminer-package-project -o \"{currentSlnNameWithoutExtension}\" -auth \"\" -cdp true -I Complete --force && " +
            $"dotnet new sln -n \"{currentSlnNameWithoutExtension}\" && " +
            $"dotnet sln add \"{currentSlnNameWithoutExtension}/{currentSlnNameWithoutExtension}.csproj\"";
        ExecuteCommand(createProjectCommand);

        branchMode = true;
    }

    string? slnFile = GetSolutionFile(DestinationDirectory);

    if (!Directory.Exists(SourceDirectory))
    {
        LogError("Source directory does not exist.");
        return;
    }

    // Process XML files and copy other directories
    ProcessXmlFiles(slnFile);
    CopyOtherDirectories();

    if (branchMode)
    {
        CreateBranchAndCopyFiles();
    }
}
catch (DirectoryNotFoundException ex)
{
    LogError($"Directory not found: {ex.Message}");
}
catch (IOException ex)
{
    LogError($"I/O error: {ex.Message}");
}
catch (UnauthorizedAccessException ex)
{
    LogError($"Access denied: {ex.Message}");
}
catch (Exception ex)
{
    LogError($"An unexpected error occurred: {ex.Message}");
}

// Creates a new branch and copies files from the destination to the source directory.
void CreateBranchAndCopyFiles()
{
    try
    {
        Directory.SetCurrentDirectory(SourceDirectory);

        string branchName = "converted-package";
        ExecuteCommand($"git checkout --orphan {branchName}");
        ExecuteCommand("git rm -rf .");
        ExecuteCommand("git clean -fd");

        foreach (string dirPath in Directory.GetDirectories(DestinationDirectory, "*", SearchOption.AllDirectories))
        {
            string targetDirPath = dirPath.Replace(DestinationDirectory, SourceDirectory);
            if (!Directory.Exists(targetDirPath))
            {
                Directory.CreateDirectory(targetDirPath);
            }
        }

        foreach (string filePath in Directory.GetFiles(DestinationDirectory, "*.*", SearchOption.AllDirectories))
        {
            string targetFilePath = filePath.Replace(DestinationDirectory, SourceDirectory);
            File.Copy(filePath, targetFilePath, true);
        }

        ExecuteCommand("git add .");
        ExecuteCommand($"git commit -m \"Copied files from destination directory to new branch {branchName}\"");

        LogInfo($"Successfully created branch '{branchName}' and copied files.");
    }
    catch (Exception ex)
    {
        LogError($"Error creating branch and copying files: {ex.Message}");
        throw;
    }
}

// Retrieves the solution file from the specified directory.
string? GetSolutionFile(string directory)
{
    try
    {
        // Get the first .sln file in the directory
        string[] slnFiles = Directory.GetFiles(directory, "*.sln", SearchOption.TopDirectoryOnly);
        return slnFiles.FirstOrDefault();
    }
    catch (Exception ex)
    {
        LogError($"Error retrieving solution file: {ex.Message}");
        throw;
    }
}

// Processes XML files in the source directory.
void ProcessXmlFiles(string? slnFile)
{
    try
    {
        // Get all XML files in the source directory
        string[] xmlFiles = Directory.GetFiles(SourceDirectory, "*.xml", SearchOption.TopDirectoryOnly);
        foreach (string file in xmlFiles)
        {
            try
            {
                // Load the XML document
                XDocument doc = XDocument.Load(file);
                var exeElements = doc.Descendants(Ns + "Exe");

                // Skip files with multiple Exe elements
                if (exeElements.Count() > 1)
                {
                    LogWarning($"Multiple Exe elements found in {file}. Skipping the file.");
                    continue;
                }

                // Extract the script name from the <Name> element
                string? scriptName = doc.Root?.Element(Ns + "Name")?.Value;
                if (string.IsNullOrEmpty(scriptName))
                {
                    LogWarning($"No script name found in {file}. Skipping the file.");
                    continue;
                }

                foreach (var exe in exeElements)
                {
                    var projectValue = exe.Element(Ns + "Value")?.Value;
                    if (projectValue != null && projectValue.Contains("[Project:"))
                    {
                        // Extract the project name from the project value
                        string projectName = ExtractProjectName(projectValue);
                        string newName = Regex.Replace(
                            projectName,
                            @"_\d+$", // Matches an underscore followed by one or more digits at the end of the string
                            string.Empty // Replaces the match with an empty string
                        );

                        // Add projectName to the list
                        ProjectNames.Add(projectName);

                        // Create the ScriptExe object from the XML element
                        ScriptExe scriptExe = new ScriptExe(exe);

                        // Determine the template name based on the precompile flag
                        string templateName = scriptExe.IsPrecompile ? "dataminer-automation-library-project" : "dataminer-automation-project";

                        // Run dotnet commands to create the project
                        ExecuteDotnetCommands(templateName, Path.Combine(DestinationDirectory, newName), slnFile);

                        // Remove the scriptName.xml and scriptName.cs files
                        string projectDirectory = Path.Combine(DestinationDirectory, newName);
                        string xmlFilePath = Path.Combine(projectDirectory, $"{newName}.xml");
                        string csFilePath = Path.Combine(projectDirectory, $"{newName}.cs");

                        if (File.Exists(xmlFilePath))
                        {
                            File.Delete(xmlFilePath);
                        }

                        if (File.Exists(csFilePath))
                        {
                            File.Delete(csFilePath);
                        }

                        // Write the XML content to the destination directory
                        File.WriteAllText(Path.Combine(projectDirectory, $"{newName}.xml"), File.ReadAllText(file).Replace($"Project:{projectName}", $"Project:{newName}"));

                        // Merge the .csproj files
                        MergeCsprojFiles(Path.Combine(Path.Combine(Path.GetDirectoryName(file)!, projectName), $"{projectName}.csproj"), Path.Combine(projectDirectory, $"{newName}.csproj"));

                    }
                }
            }
            catch (XmlException ex)
            {
                LogError($"XML error in file {file}: {ex.Message}");
            }
            catch (Exception ex)
            {
                LogError($"Error processing file {file}: {ex.Message}");
            }
        }
    }
    catch (Exception ex)
    {
        LogError($"Error processing XML files: {ex.Message}");
        throw;
    }
}

// Extracts the project name from the project value string.
string ExtractProjectName(string projectValue)
{
    try
    {
        // Extract the project name from the project value string
        int startIndex = projectValue.IndexOf("[Project:") + "[Project:".Length;
        int endIndex = projectValue.IndexOf("]", startIndex);
        return projectValue.Substring(startIndex, endIndex - startIndex);
    }
    catch (Exception ex)
    {
        LogError($"Error extracting project name: {ex.Message}");
        throw;
    }
}

// Copies other directories from the source to the destination, excluding specified directories.
void CopyOtherDirectories()
{
    try
    {
        DirectoryInfo sourceDirInfo = new DirectoryInfo(SourceDirectory);

        foreach (DirectoryInfo dir in sourceDirInfo.GetDirectories())
        {
            // Skip excluded directories and hidden directories
            if (ExcludedDirs.Contains(dir.Name, StringComparer.OrdinalIgnoreCase) ||
                (dir.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden ||
                dir.Name.StartsWith("."))
            {
                continue;
            }

            string destinationDirPath = "";

            // Check if the folder name ends with ".Tests"
            if (dir.Name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase))
            {
                // Extract the part before ".Tests", apply the regex, and add ".Tests" back
                string baseName = dir.Name.Substring(0, dir.Name.Length - ".Tests".Length);
                string sanitizedBaseName = Regex.Replace(baseName, @"_\d+$", string.Empty);
                destinationDirPath = Path.Combine(DestinationDirectory, sanitizedBaseName + ".Tests");
            }
            else
            {
                // Apply the regex directly if the folder name does not end with ".Tests"
                destinationDirPath = Regex.Replace(
                    Path.Combine(DestinationDirectory, dir.Name),
                    @"_\d+$", // Matches an underscore followed by one or more digits at the end of the string
                    string.Empty // Replaces the match with an empty string
                );
            }

            DirectoryCopy(dir.FullName, destinationDirPath, true);
        }
    }
    catch (Exception ex)
    {
        LogError($"Error copying directories: {ex.Message}");
        throw;
    }
}

// Copies a directory and its contents to a new location.
void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
{
    try
    {
        DirectoryInfo dir = new DirectoryInfo(sourceDirName);
        DirectoryInfo[] dirs = dir.GetDirectories();

        // Check if the source directory exists
        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {sourceDirName}");
        }

        // Create the destination directory if it does not exist
        if (!Directory.Exists(destDirName))
        {
            Directory.CreateDirectory(destDirName);
        }

        // Copy files to the destination directory
        FileInfo[] files = dir.GetFiles();
        foreach (FileInfo file in files)
        {
            // Skip excluded files and specific file types
            if (file.Extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) ||
                file.Extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
                ExcludedFiles.Contains(file.Name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            // Separate the file name and extension
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.Name);
            string fileExtension = file.Extension;

            // Apply the regex to the file name without the extension
            string sanitizedFileName = Regex.Replace(
                fileNameWithoutExtension,
                @"_\d+$", // Matches an underscore followed by one or more digits at the end of the string
                string.Empty
            );

            // Recombine the sanitized file name with the original extension
            string tempPath = Path.Combine(destDirName, sanitizedFileName + fileExtension);

            file.CopyTo(tempPath, false);
        }

        // Copy subdirectories if specified
        if (copySubDirs)
        {
            foreach (DirectoryInfo subdir in dirs)
            {
                if (ExcludedSubDirs.Contains(subdir.Name, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                string tempPath = Path.Combine(destDirName, subdir.Name);

                DirectoryCopy(subdir.FullName, tempPath, copySubDirs);
            }
        }
    }
    catch (Exception ex)
    {
        LogError($"Error copying directory {sourceDirName} to {destDirName}: {ex.Message}");
        throw;
    }
}

// Logs an informational message.
void LogInfo(string message)
{
    Console.WriteLine($"INFO: {message}");
}

// Logs a warning message.
void LogWarning(string message)
{
    Console.WriteLine($"WARNING: {message}");
}

// Logs an error message.
void LogError(string message)
{
    Console.WriteLine($"ERROR: {message}");
}

// Executes dotnet commands to create and add a project to the solution.
void ExecuteDotnetCommands(string templateName, string projectName, string? slnFile)
{
    try
    {
        // Run the dotnet new command to create a new project
        string createProjectCommand = $"dotnet new {templateName} -o \"{projectName}\" -auth \"\" --force";
        ExecuteCommand(createProjectCommand);

        // Run the dotnet sln command to add the project to the solution
        string addProjectCommand = $"dotnet sln {slnFile} add \"{projectName}\"";
        ExecuteCommand(addProjectCommand);
    }
    catch (Exception ex)
    {
        LogError($"Error running dotnet commands: {ex.Message}");
        throw;
    }
}

// Executes a command in the command prompt.
void ExecuteCommand(string command)
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
            CreateNoWindow = true
        };

        using (var process = System.Diagnostics.Process.Start(processStartInfo))
        {
            if (process == null)
            {
                LogError($"Failed to start process for command '{command}'");
                return;
            }

            using (var reader = process.StandardOutput)
            {
                string output = reader.ReadToEnd();
                if (!string.IsNullOrEmpty(output))
                {
                    // LogInfo($"Command output: {output}");
                }
            }

            using (var reader = process.StandardError)
            {
                string error = reader.ReadToEnd();
                if (!string.IsNullOrEmpty(error))
                {
                    LogError($"Command error: {error}");
                }
            }

            process.WaitForExit();
        }
    }
    catch (Exception ex)
    {
        LogError($"Error executing command '{command}': {ex.Message}");
        throw;
    }
}

// Merges the contents of two .csproj files.
void MergeCsprojFiles(string sourceCsprojPath, string destinationCsprojPath)
{
    try
    {
        // Load the source and destination .csproj files
        XDocument sourceDoc = XDocument.Load(sourceCsprojPath);
        XDocument destinationDoc = XDocument.Load(destinationCsprojPath);

        XNamespace ns = sourceDoc.Root!.GetDefaultNamespace(); // Capture the source namespace

        // Get elements from the source .csproj file
        var sourceImports = sourceDoc.Descendants(ns + "Import")
            .Where(e => e.Attribute("Project")?.Value != "$(MSBuildToolsPath)\\Microsoft.CSharp.targets"); // Exclude unwanted Import
        var sourcePackageReferences = sourceDoc.Descendants(ns + "PackageReference");
        var sourceProjectReferences = sourceDoc.Descendants(ns + "ProjectReference");
        var sourceReferences = sourceDoc.Descendants(ns + "Reference");

        XElement destinationProject = destinationDoc.Element("Project")!;

        // Remove the last ItemGroup element from the destination .csproj file
        XElement? lastItemGroup = destinationProject.Elements("ItemGroup").LastOrDefault();
        lastItemGroup?.Remove();

        // Merge Import elements
        foreach (var import in sourceImports)
        {
            var existingImport = destinationProject.Elements("Import")
                .FirstOrDefault(e => e.Attribute("Project")?.Value == import.Attribute("Project")?.Value);
            if (existingImport != null)
            {
                existingImport.ReplaceWith(new XElement(import));
            }
            else
            {
                destinationProject.Add(new XElement(import));
            }
        }

        // Merge PackageReference elements
        var destinationItemGroups = destinationProject.Elements("ItemGroup").ToList();
        XElement? packageReferenceGroup = destinationItemGroups.FirstOrDefault(ig => ig.Elements("PackageReference").Any());

        if (packageReferenceGroup == null)
        {
            packageReferenceGroup = new XElement("ItemGroup");
            destinationProject.Add(packageReferenceGroup);
        }

        foreach (var packageReference in sourcePackageReferences)
        {
            var existingPackageReference = packageReferenceGroup.Elements("PackageReference")
                .FirstOrDefault(e => e.Attribute("Include")?.Value == packageReference.Attribute("Include")?.Value);
            packageReferenceGroup.Add(new XElement(packageReference));
        }

        // Merge ProjectReference elements
        XElement? projectReferenceGroup = destinationItemGroups.FirstOrDefault(ig => ig.Elements("ProjectReference").Any());

        if (projectReferenceGroup == null)
        {
            projectReferenceGroup = new XElement("ItemGroup");
            destinationProject.Add(projectReferenceGroup);
        }

        foreach (var projectReference in sourceProjectReferences)
        {
            var includeAttribute = projectReference.Attribute("Include");
            if (includeAttribute != null)
            {
                // Apply regex to remove "_int" from the Include path
                string updatedInclude = Regex.Replace(
                    includeAttribute.Value,
                    @"_\d+", // Matches an underscore followed by one or more digits
                    string.Empty
                );
                includeAttribute.Value = updatedInclude;
            }

            var existingProjectReference = projectReferenceGroup.Elements("ProjectReference")
                .FirstOrDefault(e => e.Attribute("Include")?.Value == projectReference.Attribute("Include")?.Value);
            if (existingProjectReference != null)
            {
                existingProjectReference.ReplaceWith(new XElement(projectReference));
            }
            else
            {
                projectReferenceGroup.Add(new XElement(projectReference));
            }
        }




        // Merge Reference elements
        XElement? referenceGroup = destinationItemGroups.FirstOrDefault(ig => ig.Elements("Reference").Any());
        if (referenceGroup == null)
        {
            referenceGroup = new XElement("ItemGroup");
            destinationProject.Add(referenceGroup);
        }

        foreach (var reference in sourceReferences)
        {
            var existingReference = referenceGroup.Elements("Reference")
                .FirstOrDefault(e => e.Attribute("Include")?.Value == reference.Attribute("Include")?.Value);
            if (existingReference != null)
            {
                existingReference.ReplaceWith(new XElement(reference));
            }
            else
            {
                referenceGroup.Add(new XElement(reference));
            }
        }

        // Save the merged .csproj file
        destinationDoc.Save(destinationCsprojPath);

        // Remove xmlns attribute from the saved .csproj file
        string xmlContent = File.ReadAllText(destinationCsprojPath);
        xmlContent = Regex.Replace(xmlContent, @"\sxmlns=""[^""]+""", ""); // Remove xmlns attribute
        File.WriteAllText(destinationCsprojPath, xmlContent);
    }
    catch (Exception ex)
    {
        LogError($"Error merging .csproj files: {ex.Message}");
        throw;
    }
}

// Represents a ScriptExe object. (Automation Script XML)
public class ScriptExe
{
    public string? Type { get; set; }
    public bool IsPrecompile { get; set; }
    public string? LibraryName { get; set; }

    // Initializes a new instance of the ScriptExe class from an XML element.
    public ScriptExe(XElement exeElement)
    {
        // Extract properties from the XML element
        Type = exeElement.Element("Param")?.Attribute("type")?.Value;
        IsPrecompile = exeElement.Descendants("Param")
                                  .Any(p => p.Attribute("type")?.Value == "preCompile" && p.Value == "true");
        LibraryName = exeElement.Descendants("Param")
                                .FirstOrDefault(p => p.Attribute("type")?.Value == "libraryName")?.Value;
    }
}