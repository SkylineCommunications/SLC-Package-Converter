using SLC_Package_Converter.Models;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace SLC_Package_Converter.Utilities
{
    public static class XmlProcessor
    {
        // Constants for DataMiner Files references
        private const string DataMinerFilesPath = @"C:\Skyline DataMiner\Files\";
        private const string AutomationPackageName = "Skyline.DataMiner.Dev.Automation";
        private const string AutomationPackageVersion = "10.4.0.22";
        private const string NewtonsoftJsonPackageName = "Newtonsoft.Json";

        // Deprecated/Obsolete packages that are automatically replaced:
        // - SLC.Lib.Automation → Skyline.DataMiner.Core.DataMinerSystem.Automation
        // - SLC.Lib.Common → Skyline.DataMiner.Core.DataMinerSystem.Automation
        // - SLSRMLibrary → Skyline.DataMiner.Core.SRM
        // - AutomationScript_ClassLibrary (project reference) → Skyline.DataMiner.Core.DataMinerSystem.Automation
        // - References to C:\Skyline DataMiner\Files\ → Skyline.DataMiner.Dev.Automation (version 10.4.0.22)

        // Processes XML files in the source directory.
        public static HashSet<string> ProcessXmlFiles(string sourceDir, string destDir, string? slnFile)
        {
            var processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int successfulFileCount = 0; // Track number of successfully processed files
            try
            {
                // Get all XML files in the source directory
                string[] xmlFiles = Directory.GetFiles(sourceDir, "*.xml", SearchOption.TopDirectoryOnly);
                foreach (string file in xmlFiles)
                {
                    bool fileProcessed = false; // Track if this file was processed
                    try
                    {
                        // Load the XML document
                        XDocument doc = XDocument.Load(file);
                        XNamespace ns = doc.Root?.Name.Namespace ?? XNamespace.None;

                        // Use ns + "ElementName" only if the namespace exists
                        string? scriptName = doc.Root?.Element(ns + "Name")?.Value ?? doc.Root?.Element("Name")?.Value;

                        var exeElements = doc.Descendants(ns + "Exe").Any()
                            ? doc.Descendants(ns + "Exe")
                            : doc.Descendants("Exe");

                        // Log if multiple Exe elements found - now supported
                        if (exeElements.Count() > 1)
                        {
                            Logger.LogInfo($"Multiple Exe elements found in {file}. Processing all {exeElements.Count()} EXE blocks.");
                        }

                        if (string.IsNullOrEmpty(scriptName))
                        {
                            Logger.LogWarning($"No script name found in {file}. Skipping the file.");
                            continue;
                        }

                        // Track processed project names to detect conflicts
                        var processedProjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        foreach (var exe in exeElements)
                        {
                            var projectValue = exe.Element(ns + "Value")?.Value;
                            if (projectValue != null && projectValue.Contains("[Project:"))
                            {
                                // Extract the project name from the project value
                                string projectName = ExtractProjectName(projectValue);
                                
                                // Handle numeric suffixes: keep _63000 (special), remove others (normal)
                                string newName = RemoveNumericSuffixExceptSpecial(projectName);

                                // Check for duplicate project names after suffix handling
                                if (processedProjectNames.Contains(newName))
                                {
                                    Logger.LogError($"Conflict detected: Multiple EXE blocks would result in the same project name '{newName}'. Original project: '{projectName}'. This typically happens when multiple EXE blocks have different normal numeric suffixes (e.g., _1, _2) that get removed. Skipping this file.");
                                    fileProcessed = false;
                                    break; // Exit the foreach loop to skip this entire file
                                }
                                processedProjectNames.Add(newName);

                                // Track the processed XML file
                                processedFiles.Add(file);
                                fileProcessed = true;

                                // Track the associated csproj file
                                string originalCsprojPath = Path.Combine(Path.Combine(Path.GetDirectoryName(file)!, projectName), $"{projectName}.csproj");
                                if (File.Exists(originalCsprojPath))
                                {
                                    processedFiles.Add(originalCsprojPath);
                                }

                                // Create the ScriptExe object from the XML element
                                ScriptExe scriptExe = new ScriptExe(exe);

                                // Determine the template name based on the precompile flag
                                string templateName = scriptExe.IsPrecompile ? "dataminer-automation-library-project" : "dataminer-automation-project";

                                // Run dotnet commands to create the project
                                CommandExecutor.ExecuteDotnetCommands(templateName, Path.Combine(destDir, newName), slnFile);

                                // Remove the scriptName.xml file and handle the .cs file
                                string projectDirectory = Path.Combine(destDir, newName);
                                string xmlFilePath = Path.Combine(projectDirectory, $"{newName}.xml");
                                string csFilePath = Path.Combine(projectDirectory, $"{newName}.cs");

                                if (File.Exists(xmlFilePath))
                                {
                                    File.Delete(xmlFilePath);
                                }

                                // Write C# code to .cs file if available, otherwise delete the template file
                                if (!string.IsNullOrEmpty(scriptExe.CSharpCode))
                                {
                                    // Write the C# code from XML to the .cs file with UTF-8 BOM encoding
                                    File.WriteAllText(csFilePath, scriptExe.CSharpCode, new System.Text.UTF8Encoding(true));
                                }
                                else if (File.Exists(csFilePath))
                                {
                                    File.Delete(csFilePath);
                                }

                                // Write the XML content to the destination directory with UTF-8 BOM encoding
                                // UTF-8 BOM is required for DataMiner automation scripts
                                var xmlContent = File.ReadAllText(file).Replace($"Project:{projectName}", $"Project:{newName}");
                                File.WriteAllText(Path.Combine(projectDirectory, $"{newName}.xml"), xmlContent, new System.Text.UTF8Encoding(true));

                                // Merge the .csproj files
                                MergeCsprojFiles(Path.Combine(Path.Combine(Path.GetDirectoryName(file)!, projectName), $"{projectName}.csproj"), Path.Combine(projectDirectory, $"{newName}.csproj"));
                            }
                        }
                        if (fileProcessed)
                        {
                            successfulFileCount++;
                        }
                    }
                    catch (XmlException ex)
                    {
                        Logger.LogError($"XML error in file {file}: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Error processing file {file}: {ex.Message}");
                    }
                }
                // If no files were successfully processed, fail
                if (successfulFileCount == 0)
                {
                    Logger.LogError("No XML files were successfully converted. All files were skipped due to errors or invalid format.");
                    throw new InvalidOperationException("No XML files were successfully converted.");
                }
                return processedFiles;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error processing XML files: {ex.Message}");
                throw;
            }
        }

        // Extracts the project name from the project value string.
        public static string ExtractProjectName(string projectValue)
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
                Logger.LogError($"Error extracting project name: {ex.Message}");
                throw;
            }
        }

        // Removes numeric suffixes from names, but preserves the special _63000 suffix.
        // This provides consistent handling across the codebase for project names, file names, and directory names.
        public static string RemoveNumericSuffixExceptSpecial(string name)
        {
            var match = Regex.Match(name, @"_(\d+)$");
            if (match.Success)
            {
                string suffix = match.Groups[1].Value;
                if (suffix == "63000")
                {
                    // Keep _63000 suffix as-is (special case)
                    return name;
                }
                else
                {
                    // Remove normal numeric suffixes (like _1, _2, _69)
                    return Regex.Replace(name, @"_\d+$", string.Empty);
                }
            }
            return name;
        }

        // Merges the contents of two .csproj files.
        public static void MergeCsprojFiles(string sourceCsprojPath, string destinationCsprojPath)
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

                bool hasSlcLibAutomationReference = false;
                bool hasSlcLibCommonReference = false;
                bool hasSlSrmLibraryReference = false;

                foreach (var packageReference in sourcePackageReferences)
                {
                    // Check if this is SLC.Lib.Automation (obsolete package)
                    var includeAttribute = packageReference.Attribute("Include");
                    if (includeAttribute != null && includeAttribute.Value.Equals("SLC.Lib.Automation", StringComparison.OrdinalIgnoreCase))
                    {
                        hasSlcLibAutomationReference = true;
                        // Skip this reference - it will be replaced with NuGet package
                        continue;
                    }

                    // Check if this is SLC.Lib.Common (obsolete package)
                    if (includeAttribute != null && includeAttribute.Value.Equals("SLC.Lib.Common", StringComparison.OrdinalIgnoreCase))
                    {
                        hasSlcLibCommonReference = true;
                        // Skip this reference - it will be replaced with NuGet package
                        continue;
                    }

                    // Check if this is SLSRMLibrary (obsolete package)
                    if (includeAttribute != null && includeAttribute.Value.Equals("SLSRMLibrary", StringComparison.OrdinalIgnoreCase))
                    {
                        hasSlSrmLibraryReference = true;
                        // Skip this reference - it will be replaced with NuGet package
                        continue;
                    }

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

                bool hasAutomationScriptClassLibraryReference = false;

                foreach (var projectReference in sourceProjectReferences)
                {
                    var includeAttribute = projectReference.Attribute("Include");
                    if (includeAttribute != null)
                    {
                        // Check if this references AutomationScript_ClassLibrary
                        if (includeAttribute.Value.Contains("AutomationScript_ClassLibrary", StringComparison.OrdinalIgnoreCase))
                        {
                            hasAutomationScriptClassLibraryReference = true;
                            // Skip this reference - it will be replaced with NuGet package
                            continue;
                        }

                        // Apply consistent suffix removal logic to the Include path
                        // Split path into parts, apply suffix removal to each part, then reconstruct
                        string originalPath = includeAttribute.Value;
                        string[] pathParts = originalPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                        
                        for (int i = 0; i < pathParts.Length; i++)
                        {
                            string part = pathParts[i];
                            
                            // Process .csproj files
                            if (part.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                            {
                                string nameWithoutExt = Path.GetFileNameWithoutExtension(part);
                                string processedName = RemoveNumericSuffixExceptSpecial(nameWithoutExt);
                                pathParts[i] = processedName + ".csproj";
                            }
                            // Process directory names (parts without file extensions)
                            else if (!part.Contains("."))
                            {
                                pathParts[i] = RemoveNumericSuffixExceptSpecial(part);
                            }
                            // Leave other file types (like .xml, .config) unchanged
                        }
                        
                        // Reconstruct the path preserving the original separator style
                        // Note: ProjectReferences in .csproj files typically use backslashes on Windows
                        char separator = originalPath.Contains('\\') ? '\\' : '/';
                        string updatedInclude = string.Join(separator.ToString(), pathParts);
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

                bool hasDataMinerFilesReferences = false;
                bool hasNewtonsoftJsonReference = false;
                foreach (var reference in sourceReferences)
                {
                    // Check if the reference has a HintPath pointing to Skyline DataMiner\Files\ (can be absolute or relative path)
                    var hintPath = reference.Element(ns + "HintPath")?.Value ?? reference.Element("HintPath")?.Value;
                    if (!string.IsNullOrEmpty(hintPath) && 
                        (hintPath.Contains(@"Skyline DataMiner\Files\", StringComparison.OrdinalIgnoreCase) ||
                         hintPath.Contains("Skyline DataMiner/Files/", StringComparison.OrdinalIgnoreCase)))
                    {
                        // Exclude this reference and log it
                        string referenceName = reference.Attribute("Include")?.Value ?? "Unknown";
                        
                        // Special handling for Newtonsoft.Json
                        if (referenceName.StartsWith("Newtonsoft.Json", StringComparison.OrdinalIgnoreCase))
                        {
                            Logger.LogInfo($"Excluding reference '{referenceName}' with HintPath pointing to DataMiner Files directory. It will be replaced by the {NewtonsoftJsonPackageName} NuGet package.");
                            hasNewtonsoftJsonReference = true;
                        }
                        else
                        {
                            Logger.LogInfo($"Excluding reference '{referenceName}' with HintPath pointing to DataMiner Files directory. It will be replaced by the {AutomationPackageName} NuGet package.");
                            hasDataMinerFilesReferences = true;
                        }
                        continue;
                    }

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

                // If Newtonsoft.Json reference was excluded, add the NuGet package using dotnet add
                if (hasNewtonsoftJsonReference)
                {
                    AddNewtonsoftJsonPackage(destinationCsprojPath);
                }

                // If DataMiner Files references were excluded, add the Dev.Automation NuGet package using dotnet add
                if (hasDataMinerFilesReferences)
                {
                    AddDevAutomationPackage(destinationCsprojPath);
                }

                // If SLC.Lib.Automation was referenced, add the NuGet package instead using dotnet add
                if (hasSlcLibAutomationReference)
                {
                    AddDataMinerSystemAutomationPackage(destinationCsprojPath);
                }

                // If SLC.Lib.Common was referenced, add the NuGet package instead using dotnet add
                if (hasSlcLibCommonReference)
                {
                    AddDataMinerSystemAutomationPackage(destinationCsprojPath);
                }

                // If SLSRMLibrary was referenced, add the NuGet package instead using dotnet add
                if (hasSlSrmLibraryReference)
                {
                    AddDataMinerSystemSrmPackage(destinationCsprojPath);
                }

                // If AutomationScript_ClassLibrary was referenced, add the NuGet package instead using dotnet add
                if (hasAutomationScriptClassLibraryReference)
                {
                    AddDataMinerSystemAutomationPackage(destinationCsprojPath);
                }

                // Add Skyline.DataMiner.Utils.SecureCoding.Analyzers package using dotnet command to get latest version
                AddSecureCodingAnalyzersPackage(destinationCsprojPath);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error merging .csproj files: {ex.Message}");
                throw;
            }
        }

        // Adds the SecureCoding.Analyzers package to a project using dotnet add command.
        private static void AddSecureCodingAnalyzersPackage(string csprojPath)
        {
            try
            {
                // Use dotnet add package to add the latest version (updates if already present)
                string addPackageCommand = $"dotnet add \"{csprojPath}\" package Skyline.DataMiner.Utils.SecureCoding.Analyzers --source https://api.nuget.org/v3/index.json";
                CommandExecutor.ExecuteCommand(addPackageCommand);
                Logger.LogInfo("SecureCoding.Analyzers package added/updated successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error adding SecureCoding.Analyzers package: {ex.Message}");
                throw;
            }
        }

        // Adds the Skyline.DataMiner.Core.DataMinerSystem.Automation package to a project using dotnet add command.
        private static void AddDataMinerSystemAutomationPackage(string csprojPath)
        {
            try
            {
                // Use dotnet add package to add the latest version (updates if already present)
                string addPackageCommand = $"dotnet add \"{csprojPath}\" package Skyline.DataMiner.Core.DataMinerSystem.Automation --source https://api.nuget.org/v3/index.json";
                CommandExecutor.ExecuteCommand(addPackageCommand);
                Logger.LogInfo("Replaced AutomationScript_ClassLibrary reference with NuGet package Skyline.DataMiner.Core.DataMinerSystem.Automation");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error adding DataMinerSystem.Automation package: {ex.Message}");
                throw;
            }
        }

        // Adds the Skyline.DataMiner.Core.SRM package to a project using dotnet add command.
        private static void AddDataMinerSystemSrmPackage(string csprojPath)
        {
            try
            {
                // Use dotnet add package to add the latest version (updates if already present)
                string addPackageCommand = $"dotnet add \"{csprojPath}\" package Skyline.DataMiner.Core.SRM --source https://api.nuget.org/v3/index.json";
                CommandExecutor.ExecuteCommand(addPackageCommand);
                Logger.LogInfo("Replaced SLSRMLibrary reference with NuGet package Skyline.DataMiner.Core.SRM");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error adding DataMiner.Core.SRM package: {ex.Message}");
                throw;
            }
        }

        // Adds the Skyline.DataMiner.Dev.Automation package to a project using dotnet add command.
        private static void AddDevAutomationPackage(string csprojPath)
        {
            try
            {
                // Use dotnet add package with minimum version constraint
                // The version "[10.4.0.22,)" means "10.4.0.22 or higher", allowing NuGet to upgrade when needed
                string versionConstraint = $"[{AutomationPackageVersion},)";
                string addPackageCommand = $"dotnet add \"{csprojPath}\" package {AutomationPackageName} --version \"{versionConstraint}\" --source https://api.nuget.org/v3/index.json";
                CommandExecutor.ExecuteCommand(addPackageCommand);
                Logger.LogInfo($"Added NuGet package '{AutomationPackageName}' with minimum version constraint [{AutomationPackageVersion},) to allow NuGet resolution of higher versions when needed.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error adding {AutomationPackageName} package: {ex.Message}");
                throw;
            }
        }

        // Adds the Newtonsoft.Json package to a project using dotnet add command.
        private static void AddNewtonsoftJsonPackage(string csprojPath)
        {
            try
            {
                // Use dotnet add package to add the latest version (updates if already present)
                string addPackageCommand = $"dotnet add \"{csprojPath}\" package {NewtonsoftJsonPackageName} --source https://api.nuget.org/v3/index.json";
                CommandExecutor.ExecuteCommand(addPackageCommand);
                Logger.LogInfo($"Added latest stable NuGet package '{NewtonsoftJsonPackageName}' as a replacement for Newtonsoft.Json DLL reference.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error adding {NewtonsoftJsonPackageName} package: {ex.Message}");
                throw;
            }
        }
    }
}
