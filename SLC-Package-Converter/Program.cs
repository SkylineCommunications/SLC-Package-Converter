using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

const string SourceDirectory = @"C:\GIT\SLC-AS-MediaOps";
const string DestinationDirectory = @"C:\GIT\SLC-MediaOps";
string[] ExcludedDirs = { "CompanionFiles", "Internal", "Documentation", "Dlls" };
XNamespace Ns = "http://www.skyline.be/automation";

try
{
    LogInfo("Starting the package conversion process.");

    string slnFile = GetSolutionFile(DestinationDirectory);
    string slnContent = slnFile != null ? File.ReadAllText(slnFile) : null;
    string projectId = slnContent != null ? ExtractProjectId(slnContent) : null;

    if (Directory.Exists(SourceDirectory))
    {
        ProcessXmlFiles(slnFile, slnContent, projectId);
        CopyOtherDirectories();
        CheckCsprojFilesForDataMinerType(DestinationDirectory);
    }
    else
    {
        LogError("Source directory does not exist.");
    }
}
catch (Exception ex)
{
    LogError($"An error occurred: {ex.Message}");
}

string GetSolutionFile(string directory)
{
    string[] slnFiles = Directory.GetFiles(directory, "*.sln", SearchOption.TopDirectoryOnly);
    return slnFiles.FirstOrDefault();
}

string ExtractProjectId(string slnContent)
{
    string projectIdPattern = @"Project\(""\{(?<id>[A-F0-9\-]+)\}""\)";
    Match match = Regex.Match(slnContent, projectIdPattern);
    if (match.Success)
    {
        string projectId = match.Groups["id"].Value;
        LogInfo($"First project ID: {projectId}");
        return projectId;
    }
    LogWarning("No project ID found in the solution file.");
    return null;
}

void ProcessXmlFiles(string slnFile, string slnContent, string projectId)
{
    string[] xmlFiles = Directory.GetFiles(SourceDirectory, "*.xml", SearchOption.TopDirectoryOnly);

    foreach (string file in xmlFiles)
    {
        try
        {
            XDocument doc = XDocument.Load(file);
            var exeElements = doc.Descendants(Ns + "Exe");

            foreach (var exe in exeElements)
            {
                var projectValue = exe.Element(Ns + "Value")?.Value;
                if (projectValue != null && projectValue.Contains("[Project:"))
                {
                    string projectName = ExtractProjectName(projectValue);
                    string projectPath = Path.Combine(SourceDirectory, projectName);

                    if (Directory.Exists(projectPath))
                    {
                        string destinationProjectPath = Path.Combine(DestinationDirectory, projectName);
                        if (!Directory.Exists(destinationProjectPath))
                        {
                            DirectoryCopy(projectPath, destinationProjectPath, true);
                            string destinationXmlPath = Path.Combine(destinationProjectPath, Path.GetFileName(file));
                            File.Copy(file, destinationXmlPath, true);

                            if (slnFile != null && slnContent != null)
                            {
                                AddProjectToSolution(slnFile, ref slnContent, projectId, projectName);
                            }
                        }
                    }
                    else
                    {
                        LogWarning($"Project directory does not exist: {projectPath}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"Error processing file {file}: {ex.Message}");
        }
    }
}

string ExtractProjectName(string projectValue)
{
    int startIndex = projectValue.IndexOf("[Project:") + "[Project:".Length;
    int endIndex = projectValue.IndexOf("]", startIndex);
    return projectValue.Substring(startIndex, endIndex - startIndex);
}

void AddProjectToSolution(string slnFile, ref string slnContent, string projectId, string projectName)
{
    string newProjectGuid = Guid.NewGuid().ToString().ToUpper();
    string projectEntry = $"Project(\"{{{projectId}}}\") = \"{projectName}\", \"{projectName}\\{projectName}.csproj\", \"{{{newProjectGuid}}}\"\nEndProject\n";
    int globalIndex = slnContent.IndexOf("Global");

    if (globalIndex > 0)
    {
        slnContent = slnContent.Insert(globalIndex, projectEntry);
        File.WriteAllText(slnFile, slnContent);
        LogInfo($"Added project {projectName} to solution file.");
    }
    else
    {
        LogWarning("Global section not found in the solution file.");
    }
}

void CopyOtherDirectories()
{
    DirectoryInfo sourceDirInfo = new DirectoryInfo(SourceDirectory);

    foreach (DirectoryInfo dir in sourceDirInfo.GetDirectories())
    {
        if (ExcludedDirs.Contains(dir.Name, StringComparer.OrdinalIgnoreCase) ||
            (dir.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden ||
            dir.Name.StartsWith("."))
        {
            continue;
        }

        string destinationDirPath = Path.Combine(DestinationDirectory, dir.Name);
        if (!Directory.Exists(destinationDirPath))
        {
            DirectoryCopy(dir.FullName, destinationDirPath, true);
        }
    }
}

void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
{
    DirectoryInfo dir = new DirectoryInfo(sourceDirName);
    DirectoryInfo[] dirs = dir.GetDirectories();

    if (!dir.Exists)
    {
        throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {sourceDirName}");
    }

    if (!Directory.Exists(destDirName))
    {
        Directory.CreateDirectory(destDirName);
    }

    FileInfo[] files = dir.GetFiles();
    foreach (FileInfo file in files)
    {
        string tempPath = Path.Combine(destDirName, file.Name);
        file.CopyTo(tempPath, false);
    }

    if (copySubDirs)
    {
        foreach (DirectoryInfo subdir in dirs)
        {
            string tempPath = Path.Combine(destDirName, subdir.Name);
            DirectoryCopy(subdir.FullName, tempPath, copySubDirs);
        }
    }
}

void CheckCsprojFilesForDataMinerType(string directory)
{
    string[] csprojFiles = Directory.GetFiles(directory, "*.csproj", SearchOption.AllDirectories);

    foreach (string csprojFile in csprojFiles)
    {
        try
        {
            string csprojContent = File.ReadAllText(csprojFile);
            if (csprojContent.Contains("<DataMinerType>"))
            {
                continue;
            }

            string csFile = Path.Combine(Path.GetDirectoryName(csprojFile), Path.GetFileNameWithoutExtension(csprojFile) + ".cs");
            if (File.Exists(csFile))
            {
                string csContent = File.ReadAllText(csFile);
                string dataMinerType = "AutomationScript";

                if (csContent.Contains("[AutomationEntryPoint(AutomationEntryPointType.Types.OnApiTrigger)]"))
                {
                    dataMinerType = "UserDefinedApi";
                    LogInfo($"UserDefinedApi found in {csprojFile}");
                }
                else if (csContent.Contains("GQIMetaData"))
                {
                    dataMinerType = "AdHocDataSource";
                    LogInfo($"GQI found in {csprojFile}");
                }
                else
                {
                    LogInfo($"Automation found in {csprojFile}");
                }

                // Insert DataMinerType property before the closing </Project> tag
                string propertyGroup = $"\n  <PropertyGroup>\n    <DataMinerType>{dataMinerType}</DataMinerType>\n  </PropertyGroup>\n";
                int insertIndex = csprojContent.LastIndexOf("</Project>");
                if (insertIndex != -1)
                {
                    csprojContent = csprojContent.Insert(insertIndex, propertyGroup);
                    File.WriteAllText(csprojFile, csprojContent);
                }
            }
            else
            {
                LogWarning($"Corresponding .cs file not found for {csprojFile}");
            }
        }
        catch (Exception ex)
        {
            LogError($"Error processing .csproj file {csprojFile}: {ex.Message}");
        }
    }
}

void LogInfo(string message)
{
    Console.WriteLine($"INFO: {message}");
}

void LogWarning(string message)
{
    Console.WriteLine($"WARNING: {message}");
}

void LogError(string message)
{
    Console.WriteLine($"ERROR: {message}");
}
