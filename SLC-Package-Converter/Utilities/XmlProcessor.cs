using SLC_Package_Converter.Models;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Net.Http;
using System.Text.Json;

namespace SLC_Package_Converter.Utilities
{
    public static class XmlProcessor
    {
        // Constants for DataMiner Files references
        private const string DataMinerFilesPath = @"C:\Skyline DataMiner\Files\";
        private const string AutomationPackageName = "Skyline.DataMiner.Dev.Automation";
        private const string AutomationPackageVersion = "10.4.0.22";
        private const string NewtonsoftJsonPackageName = "Newtonsoft.Json";
        
        // Cache for fetched package versions (fetched once per package)
        private static readonly Dictionary<string, string> PackageVersionCache = new Dictionary<string, string>();

        // Deprecated/Obsolete packages that are automatically replaced:
        // - SLC.Lib.Automation → Skyline.DataMiner.Core.DataMinerSystem.Automation
        // - SLC.Lib.Common → Skyline.DataMiner.Core.DataMinerSystem.Automation
        // - AutomationScript_ClassLibrary (project reference) → Skyline.DataMiner.Core.DataMinerSystem.Automation
        // - Newtonsoft.Json → Newtonsoft.Json NuGet package
        // 
        // Special handling for DataMiner Files references:
        // - References to C:\Skyline DataMiner\Files\ for DLLs included in Dev.Automation package are removed
        //   (SLManagedAutomation, SLNetTypes, SLLoggerUtil, Skyline.DataMiner.Storage.Types)
        // - Other DataMiner Files references (like SLSRMLibrary) are updated to point to ..\Dlls\ folder
        // - The Skyline.DataMiner.Dev.Automation package is added when DataMiner Files references are found
        // - If DLL doesn't exist in Dlls folder, a warning is logged for user to add it manually

        /// <summary>
        /// Checks if a given path is a Skyline DataMiner path.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns>True if the path contains "Skyline DataMiner", false otherwise.</returns>
        private static bool IsDataMinerPath(string path)
        {
            return path.Contains(@"Skyline DataMiner\", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains("Skyline DataMiner/", StringComparison.OrdinalIgnoreCase);
        }

        // Processes XML files in the source directory.
        public static (HashSet<string> processedFiles, int convertedProjectCount) ProcessXmlFiles(string sourceDir, string destDir, string? slnFile)
        {
            var processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int convertedProjectCount = 0;
            try
            {
                // Get all XML files in the source directory
                string[] xmlFiles = Directory.GetFiles(sourceDir, "*.xml", SearchOption.TopDirectoryOnly);
                foreach (string file in xmlFiles)
                {
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

                        // Check if this XML file exists in the source solution file(s)
                        string xmlFileName = Path.GetFileName(file);
                        if (!SolutionHelper.IsProjectInSolution(sourceDir, xmlFileName))
                        {
                            Logger.LogWarning($"⚠️  EXCLUDED: XML file '{xmlFileName}' not found in any solution file. This is likely a leftover file that was removed from the solution. Skipping this file.");
                            continue;
                        }

                        // Track processed project names to detect conflicts
                        var processedProjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        foreach (var exe in exeElements)
                        {
                            var projectValue = exe.Element(ns + "Value")?.Value;
                            
                            // Skip if there's no value at all
                            if (string.IsNullOrEmpty(projectValue))
                            {
                                continue;
                            }

                            // Determine project name: extract from [Project:] tag if present, otherwise use script name
                            string projectName;
                            bool hasProjectTag = projectValue.Contains("[Project:");
                            
                            if (hasProjectTag)
                            {
                                // Extract the project name from the project value
                                projectName = ExtractProjectName(projectValue);
                            }
                            else
                            {
                                // Use script name when there's no [Project:] tag (e.g., embedded C# code)
                                // scriptName is guaranteed to be non-null here due to the check at line 54-58
                                projectName = scriptName!;
                            }
                            
                            // Skip EXE blocks with _63000 suffix (AutomationScript_ClassLibrary - folder will be excluded)
                            if (projectName.EndsWith("_63000", StringComparison.OrdinalIgnoreCase))
                            {
                                Logger.LogInfo($"Skipping EXE block '{projectName}' - AutomationScript_ClassLibrary references are excluded.");

                                // Mark all files in the _63000 source directory as processed so
                                // DirectoryHelper.CopyOtherDirectories does not copy them over and
                                // overwrite the freshly generated SDK-style .csproj.
                                string skippedDir = Path.Combine(Path.GetDirectoryName(file)!, projectName);
                                if (Directory.Exists(skippedDir))
                                {
                                    foreach (string skippedFile in Directory.EnumerateFiles(skippedDir, "*", SearchOption.AllDirectories))
                                    {
                                        processedFiles.Add(skippedFile);
                                    }
                                }

                                continue;
                            }
                            
                            // Remove all numeric suffixes from project name (_1, _2, _4, _6, etc.)
                            string newName = RemoveNumericSuffixExceptSpecial(projectName);

                            // Check for duplicate project names and auto-append numeric suffix if needed
                            if (processedProjectNames.Contains(newName))
                            {
                                // Find the next available numeric suffix (starting from _2)
                                int counter = 2;
                                string uniqueName;
                                do
                                {
                                    uniqueName = $"{newName}_{counter}";
                                    counter++;
                                } while (processedProjectNames.Contains(uniqueName));
                                
                                Logger.LogInfo($"Name collision detected for '{newName}'. Using '{uniqueName}' instead.");
                                newName = uniqueName;
                            }
                            processedProjectNames.Add(newName);
                            convertedProjectCount++;

                            // Track the processed XML file
                            processedFiles.Add(file);

                            // Track the associated csproj file (only if it exists)
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
                            if (hasProjectTag)
                            {
                                // Replace Project: tag if it existed and remove Param type="ref" elements
                                XDocument xmlDoc = XDocument.Load(file);
                                XNamespace xmlNs = xmlDoc.Root?.Name.Namespace ?? XNamespace.None;
                                
                                // Find all Exe elements
                                var allExeElements = xmlDoc.Descendants(xmlNs + "Exe");
                                if (!allExeElements.Any())
                                {
                                    allExeElements = xmlDoc.Descendants("Exe");
                                }
                                var exeList = allExeElements.ToList();
                                
                                // Remove all EXE blocks with _63000 suffix (AutomationScript_ClassLibrary references)
                                foreach (var exeToCheck in exeList.ToList())
                                {
                                    // Check by id attribute
                                    string? exeIdValue = exeToCheck.Attribute("id")?.Value;
                                    if (exeIdValue == "63000")
                                    {
                                        exeToCheck.Remove();
                                        continue;
                                    }
                                    
                                    // Check by project name in Value element
                                    var checkValueElement = exeToCheck.Element(xmlNs + "Value") ?? exeToCheck.Element("Value");
                                    if (checkValueElement != null)
                                    {
                                        string valueContent = checkValueElement.Value;
                                        if (valueContent.Contains("[Project:") && valueContent.Contains("_63000"))
                                        {
                                            exeToCheck.Remove();
                                        }
                                    }
                                }
                                
                                // Find the matching exe element by comparing the id attribute if present, or by index
                                XElement? targetExe = null;
                                string? exeId = exe.Attribute("id")?.Value;
                                if (!string.IsNullOrEmpty(exeId))
                                {
                                    targetExe = exeList.FirstOrDefault(e => e.Attribute("id")?.Value == exeId);
                                }
                                else
                                {
                                    // If no id, try to match by index
                                    int exeIndex = exeElements.ToList().IndexOf(exe);
                                    if (exeIndex >= 0 && exeIndex < exeList.Count)
                                    {
                                        targetExe = exeList[exeIndex];
                                    }
                                }
                                
                                if (targetExe != null)
                                {
                                    // Replace Project: tag in Value element
                                    var valueElement = targetExe.Element(xmlNs + "Value") ?? targetExe.Element("Value");
                                    if (valueElement != null && valueElement.Value.Contains($"Project:{projectName}"))
                                    {
                                        valueElement.Value = valueElement.Value.Replace($"Project:{projectName}", $"Project:{newName}");
                                    }
                                    
                                    // Remove Param type="ref" elements
                                    var refParams = targetExe.Descendants(xmlNs + "Param")
                                        .Where(p => p.Attribute("type")?.Value == "ref")
                                        .ToList();
                                    if (!refParams.Any())
                                    {
                                        refParams = targetExe.Descendants("Param")
                                            .Where(p => p.Attribute("type")?.Value == "ref")
                                            .ToList();
                                    }
                                    foreach (var refParam in refParams)
                                    {
                                        refParam.Remove();
                                    }
                                }
                                
                                // Save the modified XML with UTF-8 BOM encoding
                                SaveXmlWithUtf8Bom(xmlDoc, Path.Combine(projectDirectory, $"{newName}.xml"));
                            }
                            else
                            {
                                // When there's no [Project:] tag, replace the embedded C# code in Value element with [Project:newName]
                                // Load a fresh copy of the document for modification
                                XDocument xmlDoc = XDocument.Load(file);
                                XNamespace xmlNs = xmlDoc.Root?.Name.Namespace ?? XNamespace.None;
                                
                                // Find all Exe elements
                                var allExeElements = xmlDoc.Descendants(xmlNs + "Exe");
                                if (!allExeElements.Any())
                                {
                                    allExeElements = xmlDoc.Descendants("Exe");
                                }
                                var exeList = allExeElements.ToList();
                                
                                // Remove all EXE blocks with _63000 suffix (AutomationScript_ClassLibrary references)
                                foreach (var exeToCheck in exeList.ToList())
                                {
                                    // Check by id attribute
                                    string? exeIdValue = exeToCheck.Attribute("id")?.Value;
                                    if (exeIdValue == "63000")
                                    {
                                        exeToCheck.Remove();
                                        continue;
                                    }
                                    
                                    // Check by project name in Value element
                                    var checkValueElement = exeToCheck.Element(xmlNs + "Value") ?? exeToCheck.Element("Value");
                                    if (checkValueElement != null)
                                    {
                                        string valueContent = checkValueElement.Value;
                                        if (valueContent.Contains("[Project:") && valueContent.Contains("_63000"))
                                        {
                                            exeToCheck.Remove();
                                        }
                                    }
                                }
                                
                                // Find the matching exe element by comparing the id attribute if present, or by index
                                XElement? targetExe = null;
                                string? exeId = exe.Attribute("id")?.Value;
                                if (!string.IsNullOrEmpty(exeId))
                                {
                                    targetExe = exeList.FirstOrDefault(e => e.Attribute("id")?.Value == exeId);
                                }
                                else
                                {
                                    // If no id, try to match by index
                                    int exeIndex = exeElements.ToList().IndexOf(exe);
                                    if (exeIndex >= 0 && exeIndex < exeList.Count)
                                    {
                                        targetExe = exeList[exeIndex];
                                    }
                                }
                                
                                if (targetExe != null)
                                {
                                    // Find the Value element within the target exe element
                                    var valueElement = targetExe.Element(xmlNs + "Value") ?? targetExe.Element("Value");
                                    if (valueElement != null)
                                    {
                                        // Replace the embedded C# code with [Project:newName]
                                        valueElement.Value = $"[Project:{newName}]";
                                    }
                                    
                                    // Remove Param type="ref" elements
                                    var refParams = targetExe.Descendants(xmlNs + "Param")
                                        .Where(p => p.Attribute("type")?.Value == "ref")
                                        .ToList();
                                    if (!refParams.Any())
                                    {
                                        refParams = targetExe.Descendants("Param")
                                            .Where(p => p.Attribute("type")?.Value == "ref")
                                            .ToList();
                                    }
                                    foreach (var refParam in refParams)
                                    {
                                        refParam.Remove();
                                    }
                                }
                                
                                // Save the modified XML with UTF-8 BOM encoding
                                SaveXmlWithUtf8Bom(xmlDoc, Path.Combine(projectDirectory, $"{newName}.xml"));
                            }

                            // Merge the .csproj files only if the source file exists
                            if (File.Exists(originalCsprojPath))
                            {
                                MergeCsprojFiles(originalCsprojPath, Path.Combine(projectDirectory, $"{newName}.csproj"));
                            }
                            
                            // Add DLL references from XML Param elements to the .csproj
                            AddDllReferencesToCsproj(scriptExe.DllReferences, Path.Combine(projectDirectory, $"{newName}.csproj"));
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
                // Return processed files and the count of actually converted projects
                return (processedFiles, convertedProjectCount);
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

        // Removes all numeric suffixes from names.
        // 
        // Suffix handling rules:
        // - All numeric suffixes (_1, _2, _4, _6, etc.) are removed
        // - _63000 EXE blocks are skipped entirely in XML processing (not processed at all)
        // - When collisions occur after suffix removal, automatic numbering (_2, _3, etc.) is applied
        //
        // This provides consistent handling across the codebase for project names, file names, and directory names.
        public static string RemoveNumericSuffixExceptSpecial(string name)
        {
            // Remove any numeric suffix at the end (e.g., _1, _2, _69, _4, _6, etc.)
            return Regex.Replace(name, @"_\d+$", string.Empty);
        }

        // Checks if a DLL exists in the solution-level Dlls folder.
        // The Dlls folder is always at solution level (same level as .sln file).
        private static bool DllExistsInDllsFolder(string csprojPath, string dllFileName)
        {
            try
            {
                // Get the solution directory (one level up from the project)
                string projectDir = Path.GetDirectoryName(csprojPath) ?? string.Empty;
                string solutionDir = Path.GetDirectoryName(projectDir) ?? string.Empty;
                string dllPath = Path.Combine(solutionDir, "Dlls", dllFileName);
                
                return File.Exists(dllPath);
            }
            catch
            {
                return false;
            }
        }

        // Merges the contents of two .csproj files.
        public static void MergeCsprojFiles(string sourceCsprojPath, string destinationCsprojPath)
        {
            try
            {
                Logger.LogInfo("Merging .csproj files");
                Logger.LogDebug($"Source .csproj: {sourceCsprojPath}");
                Logger.LogDebug($"Destination .csproj: {destinationCsprojPath}");
                
                // Validate files exist
                if (!File.Exists(sourceCsprojPath))
                {
                    Logger.LogError($"Source .csproj file does not exist: {sourceCsprojPath}");
                    throw new FileNotFoundException($"Source .csproj file not found: {sourceCsprojPath}");
                }
                
                if (!File.Exists(destinationCsprojPath))
                {
                    Logger.LogError($"Destination .csproj file does not exist: {destinationCsprojPath}");
                    throw new FileNotFoundException($"Destination .csproj file not found: {destinationCsprojPath}");
                }
                
                // Load the source and destination .csproj files
                XDocument sourceDoc = XDocument.Load(sourceCsprojPath);
                XDocument destinationDoc = XDocument.Load(destinationCsprojPath);

                XNamespace ns = sourceDoc.Root!.GetDefaultNamespace(); // Capture the source namespace
                Logger.LogDebug($"Source namespace: {ns}");

                // Get elements from the source .csproj file
                var sourceImports = sourceDoc.Descendants(ns + "Import")
                    .Where(e => e.Attribute("Project")?.Value != "$(MSBuildToolsPath)\\Microsoft.CSharp.targets"); // Exclude unwanted Import
                var sourcePackageReferences = sourceDoc.Descendants(ns + "PackageReference");
                var sourceProjectReferences = sourceDoc.Descendants(ns + "ProjectReference");
                var sourceReferences = sourceDoc.Descendants(ns + "Reference");
                
                Logger.LogDebug($"Source Import elements: {sourceImports.Count()}");
                Logger.LogDebug($"Source PackageReference elements: {sourcePackageReferences.Count()}");
                Logger.LogDebug($"Source ProjectReference elements: {sourceProjectReferences.Count()}");
                Logger.LogDebug($"Source Reference elements: {sourceReferences.Count()}");

                XElement destinationProject = destinationDoc.Element("Project")!;

                // Remove the last ItemGroup element from the destination .csproj file
                XElement? lastItemGroup = destinationProject.Elements("ItemGroup").LastOrDefault();
                if (lastItemGroup != null)
                {
                    Logger.LogDebug("Removing last ItemGroup from destination");
                    lastItemGroup.Remove();
                }

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

                    // Note: SLSRMLibrary PackageReferences are NOT automatically replaced.
                    // They will be handled as Reference elements (DLLs) in the Reference processing section below.

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

                bool hasNewtonsoftJsonReference = false;
                bool hasDataMinerFilesReferences = false;
                foreach (var reference in sourceReferences)
                {
                    // Check if the reference has a HintPath
                    var hintPath = reference.Element(ns + "HintPath")?.Value ?? reference.Element("HintPath")?.Value;
                    var includeAttribute = reference.Attribute("Include");
                    
                    // Check if this is Newtonsoft.Json from any directory
                    if (!string.IsNullOrEmpty(hintPath) && hintPath.Contains("Newtonsoft.Json", StringComparison.OrdinalIgnoreCase))
                    {
                        string referenceName = includeAttribute?.Value ?? "Unknown";
                        Logger.LogInfo($"Excluding {referenceName} (replaced by NuGet)");
                        Logger.LogDebug($"Excluding reference '{referenceName}' with HintPath '{hintPath}'. It will be replaced by the {NewtonsoftJsonPackageName} NuGet package.");
                        hasNewtonsoftJsonReference = true;
                        continue;
                    }
                    
                    // Check if the reference has an absolute HintPath (e.g., C:\...)
                    if (!string.IsNullOrEmpty(hintPath) && Path.IsPathRooted(hintPath))
                    {
                        string referenceName = includeAttribute?.Value ?? "Unknown";
                        
                        // Check if this DLL is included in the Dev.Automation package (SLManagedAutomation, SLNetTypes, SLLoggerUtil, Skyline.DataMiner.Storage.Types)
                        if (hintPath.Contains("SLManagedAutomation", StringComparison.OrdinalIgnoreCase) ||
                            hintPath.Contains("SLNetTypes", StringComparison.OrdinalIgnoreCase) ||
                            hintPath.Contains("SLLoggerUtil", StringComparison.OrdinalIgnoreCase) ||
                            hintPath.Contains("Skyline.DataMiner.Storage.Types", StringComparison.OrdinalIgnoreCase))
                        {
                            // Skip this reference - it will be replaced by the Dev.Automation package
                            Logger.LogInfo($"Excluding {referenceName} (replaced by {AutomationPackageName})");
                            Logger.LogDebug($"Excluding reference '{referenceName}' with HintPath '{hintPath}'. It will be replaced by the {AutomationPackageName} NuGet package.");
                            hasDataMinerFilesReferences = true;
                            continue;
                        }
                        
                        // For all absolute path DLLs, update the HintPath to point to solution-level Dlls folder
                        string dllFileName = Path.GetFileName(hintPath);
                        string newHintPath = $@"..\Dlls\{dllFileName}";
                        
                        var hintPathElement = reference.Element(ns + "HintPath") ?? reference.Element("HintPath");
                        if (hintPathElement != null)
                        {
                            hintPathElement.Value = newHintPath;
                        }
                        
                        bool dllExists = DllExistsInDllsFolder(destinationCsprojPath, dllFileName);
                        
                        if (dllExists)
                        {
                            Logger.LogInfo($"Updated {referenceName} to {newHintPath}");
                            Logger.LogDebug($"Updated reference '{referenceName}' from absolute path to: {newHintPath}");
                        }
                        else
                        {
                            Logger.LogWarning($"Updated {referenceName} to {newHintPath} (DLL not found, add {dllFileName} manually)");
                            Logger.LogDebug($"Updated reference '{referenceName}' to: {newHintPath}. The DLL file was not found in the repository. Please add {dllFileName} to the Dlls folder manually.");
                        }
                        
                        // Mark that we found DataMiner Files references to add the Dev.Automation NuGet package if it's a Skyline DataMiner path
                        if (IsDataMinerPath(hintPath))
                        {
                            hasDataMinerFilesReferences = true;
                        }
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
                Logger.LogDebug("Saving merged .csproj file");
                destinationDoc.Save(destinationCsprojPath);

                // Remove xmlns attribute from the saved .csproj file
                string xmlContent = File.ReadAllText(destinationCsprojPath);
                xmlContent = Regex.Replace(xmlContent, @"\sxmlns=""[^""]+""", ""); // Remove xmlns attribute
                File.WriteAllText(destinationCsprojPath, xmlContent);
                Logger.LogDebug("Removed xmlns attribute from .csproj file");

                // If Newtonsoft.Json reference was excluded, add the NuGet package using dotnet add
                if (hasNewtonsoftJsonReference)
                {
                    Logger.LogDebug("Newtonsoft.Json reference found - will add NuGet package");
                    AddNewtonsoftJsonPackage(destinationCsprojPath);
                }

                // If DataMiner Files references were found, add the Dev.Automation NuGet package using dotnet add
                if (hasDataMinerFilesReferences)
                {
                    Logger.LogDebug("DataMiner Files references found - will add Dev.Automation NuGet package");
                    AddDevAutomationPackage(destinationCsprojPath);
                }

                // If SLC.Lib.Automation was referenced, add the NuGet package instead using dotnet add
                if (hasSlcLibAutomationReference)
                {
                    Logger.LogDebug("SLC.Lib.Automation reference found - will add replacement NuGet packages");
                    AddDataMinerSystemAutomationPackage(destinationCsprojPath);
                    AddDevAutomationPackage(destinationCsprojPath);
                }

                // If SLC.Lib.Common was referenced, add the NuGet package instead using dotnet add
                if (hasSlcLibCommonReference)
                {
                    Logger.LogDebug("SLC.Lib.Common reference found - will add replacement NuGet package");
                    AddDataMinerSystemAutomationPackage(destinationCsprojPath);
                }

                // If AutomationScript_ClassLibrary was referenced, add the NuGet package instead using dotnet add
                if (hasAutomationScriptClassLibraryReference)
                {
                    Logger.LogDebug("AutomationScript_ClassLibrary reference found - will add replacement NuGet package");
                    AddDataMinerSystemAutomationPackage(destinationCsprojPath);
                }

                // Add Skyline.DataMiner.Utils.SecureCoding.Analyzers package using dotnet command to get latest version
                Logger.LogDebug("Adding SecureCoding.Analyzers package");
                AddSecureCodingAnalyzersPackage(destinationCsprojPath);
                Logger.LogDebug("Completed .csproj merging successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error merging .csproj files: {ex.Message}");
                Logger.LogDebug($"Source: {sourceCsprojPath}");
                Logger.LogDebug($"Destination: {destinationCsprojPath}");
                Logger.LogDebug($"Exception Type: {ex.GetType().Name}");
                Logger.LogDebug($"Stack Trace:{Environment.NewLine}{ex.StackTrace}");
                throw;
            }
        }

        // Fetches the latest version of a package from NuGet API (cached per package)
        private static string? GetLatestPackageVersion(string packageName)
        {
            // Check cache first
            if (PackageVersionCache.TryGetValue(packageName, out string? cachedVersion))
            {
                Logger.LogDebug($"Using cached version for {packageName}: {cachedVersion}");
                return cachedVersion;
            }

            try
            {
                Logger.LogDebug($"Fetching latest version for {packageName} from NuGet API...");
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(10);
                    string url = $"https://api.nuget.org/v3-flatcontainer/{packageName.ToLowerInvariant()}/index.json";
                    
                    var response = httpClient.GetStringAsync(url).Result;
                    var jsonDoc = JsonDocument.Parse(response);
                    
                    // Get the versions array and find the latest stable version
                    if (jsonDoc.RootElement.TryGetProperty("versions", out JsonElement versionsElement))
                    {
                        var versions = versionsElement.EnumerateArray()
                            .Select(v => v.GetString())
                            .Where(v => v != null && !v.Contains("-")) // Filter out pre-release versions
                            .ToList();
                        
                        if (versions.Any())
                        {
                            string? latestVersion = versions.Last(); // Versions are returned in order
                            if (latestVersion != null)
                            {
                                Logger.LogDebug($"Latest version for {packageName}: {latestVersion}");
                                PackageVersionCache[packageName] = latestVersion;
                                return latestVersion;
                            }
                        }
                    }
                }
                
                Logger.LogDebug($"Could not determine latest version for {packageName}, will use without version");
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to fetch version for {packageName}: {ex.Message}");
                Logger.LogDebug("Package will be added without version specification");
                return null;
            }
        }

        // Adds a PackageReference directly to the csproj XML file.
        private static void AddPackageReferenceToXml(string csprojPath, string packageName, string? version = null)
        {
            try
            {
                // Load the csproj file
                XDocument doc = XDocument.Load(csprojPath);
                XElement? root = doc.Root;
                
                if (root == null)
                {
                    Logger.LogError($"Failed to load csproj root element from: {csprojPath}");
                    return;
                }

                // Check if the package already exists in ANY ItemGroup
                var existingPackage = root.Elements("ItemGroup")
                    .SelectMany(ig => ig.Elements("PackageReference"))
                    .FirstOrDefault(pr => pr.Attribute("Include")?.Value == packageName);
                
                // Find or create an ItemGroup for PackageReferences
                XElement? packageReferenceGroup = root.Elements("ItemGroup")
                    .FirstOrDefault(ig => ig.Elements("PackageReference").Any());

                if (packageReferenceGroup == null)
                {
                    packageReferenceGroup = new XElement("ItemGroup");
                    root.Add(packageReferenceGroup);
                }

                if (existingPackage != null)
                {
                    // Update version if specified
                    if (version != null)
                    {
                        // Remove any child Version elements to avoid conflicts
                        existingPackage.Elements("Version").Remove();
                        
                        var versionAttr = existingPackage.Attribute("Version");
                        if (versionAttr != null)
                        {
                            versionAttr.Value = version;
                        }
                        else
                        {
                            existingPackage.Add(new XAttribute("Version", version));
                        }

                        Logger.LogDebug($"Updated existing package reference: {packageName} to version {version}");
                    }
                }
                else
                {
                    // Add new PackageReference
                    XElement packageReference = new XElement("PackageReference",
                        new XAttribute("Include", packageName));
                    
                    if (version != null)
                    {
                        packageReference.Add(new XAttribute("Version", version));
                    }

                    packageReferenceGroup.Add(packageReference);
                    Logger.LogDebug($"Added new package reference: {packageName}" + (version != null ? $" version {version}" : ""));
                }

                // Save the modified csproj file
                SaveXmlWithUtf8Bom(doc, csprojPath);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error adding PackageReference to {csprojPath}: {packageName}, Version: {version ?? "(latest)"}");
                Logger.LogDebug($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        // Adds the SecureCoding.Analyzers package to a project by directly modifying the csproj XML.
        private static void AddSecureCodingAnalyzersPackage(string csprojPath)
        {
            try
            {
                Logger.LogDebug($"Adding SecureCoding.Analyzers package to {csprojPath}");
                string? version = GetLatestPackageVersion("Skyline.DataMiner.Utils.SecureCoding.Analyzers");
                AddPackageReferenceToXml(csprojPath, "Skyline.DataMiner.Utils.SecureCoding.Analyzers", version);
                Logger.LogDebug("SecureCoding.Analyzers package added successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error adding SecureCoding.Analyzers to {csprojPath}: {ex.Message}");
                Logger.LogDebug($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        // Adds the Skyline.DataMiner.Core.DataMinerSystem.Automation package to a project by directly modifying the csproj XML.
        private static void AddDataMinerSystemAutomationPackage(string csprojPath)
        {
            try
            {
                Logger.LogDebug($"Adding DataMinerSystem.Automation package to {csprojPath}");
                string? version = GetLatestPackageVersion("Skyline.DataMiner.Core.DataMinerSystem.Automation");
                AddPackageReferenceToXml(csprojPath, "Skyline.DataMiner.Core.DataMinerSystem.Automation", version);
                Logger.LogDebug("DataMinerSystem.Automation package added successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error adding DataMinerSystem.Automation to {csprojPath}: {ex.Message}");
                Logger.LogDebug($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        // Adds the Skyline.DataMiner.Dev.Automation package to a project by directly modifying the csproj XML.
        private static void AddDevAutomationPackage(string csprojPath)
        {
            try
            {
                Logger.LogDebug($"Adding Dev.Automation package to {csprojPath}");
                AddPackageReferenceToXml(csprojPath, AutomationPackageName, AutomationPackageVersion);
                Logger.LogDebug($"Dev.Automation package version {AutomationPackageVersion} added successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error adding {AutomationPackageName} to {csprojPath}: {ex.Message}");
                Logger.LogDebug($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        // Adds the Newtonsoft.Json package to a project by directly modifying the csproj XML.
        private static void AddNewtonsoftJsonPackage(string csprojPath)
        {
            try
            {
                Logger.LogDebug($"Adding Newtonsoft.Json package to {csprojPath}");
                string? version = GetLatestPackageVersion(NewtonsoftJsonPackageName);
                AddPackageReferenceToXml(csprojPath, NewtonsoftJsonPackageName, version);
                Logger.LogDebug("Newtonsoft.Json package added successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error adding {NewtonsoftJsonPackageName} to {csprojPath}: {ex.Message}");
                Logger.LogDebug($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        // Saves an XDocument to a file with UTF-8 BOM encoding.
        private static void SaveXmlWithUtf8Bom(XDocument xmlDoc, string filePath)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var streamWriter = new StreamWriter(memoryStream, new System.Text.UTF8Encoding(true)))
                {
                    xmlDoc.Save(streamWriter);
                    streamWriter.Flush();
                    File.WriteAllBytes(filePath, memoryStream.ToArray());
                }
            }
        }

        // Adds DLL references from XML Param elements to the .csproj file.
        private static void AddDllReferencesToCsproj(List<string> dllReferences, string csprojPath)
        {
            if (dllReferences == null || dllReferences.Count == 0)
            {
                return;
            }

            try
            {
                XDocument csprojDoc = XDocument.Load(csprojPath);
                XElement projectElement = csprojDoc.Element("Project")!;
                
                // Find or create ItemGroup for References
                var itemGroups = projectElement.Elements("ItemGroup").ToList();
                XElement? referenceGroup = itemGroups.FirstOrDefault(ig => ig.Elements("Reference").Any());
                if (referenceGroup == null)
                {
                    referenceGroup = new XElement("ItemGroup");
                    projectElement.Add(referenceGroup);
                }

                bool hasNewtonsoftJsonReference = false;
                bool hasDataMinerFilesReferences = false;

                foreach (string dllPath in dllReferences)
                {
                    string dllFileName = Path.GetFileNameWithoutExtension(dllPath);
                    
                    // Check if this is Newtonsoft.Json from any directory
                    if (dllPath.Contains("Newtonsoft.Json", StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.LogInfo($"Excluding {dllPath} (replaced by NuGet)");
                        Logger.LogDebug($"Excluding DLL reference '{dllPath}'. It will be replaced by the {NewtonsoftJsonPackageName} NuGet package.");
                        hasNewtonsoftJsonReference = true;
                        continue;
                    }

                    // Apply the same rules as HintPath processing in .csproj merge
                    // Check if the DLL path is absolute (e.g., C:\...)
                    if (Path.IsPathRooted(dllPath))
                    {
                        // Check if this DLL is included in the Dev.Automation package (SLManagedAutomation, SLNetTypes, SLLoggerUtil, Skyline.DataMiner.Storage.Types)
                        if (dllPath.Contains("SLManagedAutomation", StringComparison.OrdinalIgnoreCase) ||
                            dllPath.Contains("SLNetTypes", StringComparison.OrdinalIgnoreCase) ||
                            dllPath.Contains("SLLoggerUtil", StringComparison.OrdinalIgnoreCase) ||
                            dllPath.Contains("Skyline.DataMiner.Storage.Types", StringComparison.OrdinalIgnoreCase))
                        {
                            // Skip this reference - it will be replaced by the Dev.Automation package
                            Logger.LogInfo($"Excluding {dllPath} (replaced by {AutomationPackageName})");
                            Logger.LogDebug($"Excluding DLL reference '{dllPath}'. It will be replaced by the {AutomationPackageName} NuGet package.");
                            hasDataMinerFilesReferences = true;
                            continue;
                        }
                        
                        // For all absolute path DLLs, update path to Dlls folder
                        string fullDllFileName = $"{dllFileName}.dll";
                        string newHintPath = $@"..\Dlls\{fullDllFileName}";
                        
                        bool dllExists = DllExistsInDllsFolder(csprojPath, fullDllFileName);
                        
                        if (dllExists)
                        {
                            Logger.LogInfo($"Adding {dllFileName} to {newHintPath}");
                            Logger.LogDebug($"Adding DLL reference '{dllFileName}' from absolute path to: {newHintPath}");
                        }
                        else
                        {
                            Logger.LogWarning($"Adding {dllFileName} to {newHintPath} (DLL not found, add manually)");
                            Logger.LogDebug($"Adding DLL reference '{dllFileName}' to: {newHintPath}. The DLL file was not found in the repository. Please add {fullDllFileName} to the Dlls folder manually.");
                        }
                        
                        // Check if reference already exists
                        var existingDataMinerRef = referenceGroup.Elements("Reference")
                            .FirstOrDefault(r => r.Attribute("Include")?.Value?.StartsWith(dllFileName, StringComparison.OrdinalIgnoreCase) == true);
                        
                        if (existingDataMinerRef == null)
                        {
                            XElement referenceElement = new XElement("Reference",
                                new XAttribute("Include", dllFileName),
                                new XElement("HintPath", newHintPath)
                            );
                            referenceGroup.Add(referenceElement);
                        }
                        
                        // Mark that we found DataMiner Files references to add the Dev.Automation NuGet package if it's a Skyline DataMiner path
                        if (IsDataMinerPath(dllPath))
                        {
                            hasDataMinerFilesReferences = true;
                        }
                        continue;
                    }

                    // For all other DLLs (including ProtocolScripts), add them as references
                    
                    // Check if reference already exists
                    var existingRef = referenceGroup.Elements("Reference")
                        .FirstOrDefault(r => r.Attribute("Include")?.Value?.StartsWith(dllFileName, StringComparison.OrdinalIgnoreCase) == true);
                    
                    if (existingRef == null)
                    {
                        XElement referenceElement = new XElement("Reference",
                            new XAttribute("Include", dllFileName),
                            new XElement("HintPath", dllPath)
                        );
                        referenceGroup.Add(referenceElement);
                        Logger.LogInfo($"Adding {dllPath}");
                        Logger.LogDebug($"Adding DLL reference: '{dllPath}'");
                    }
                }

                // Save the updated .csproj
                csprojDoc.Save(csprojPath);

                // Remove xmlns attribute from the saved .csproj file
                string xmlContent = File.ReadAllText(csprojPath);
                xmlContent = Regex.Replace(xmlContent, @"\sxmlns=""[^""]+""", ""); // Remove xmlns attribute
                File.WriteAllText(csprojPath, xmlContent);

                // Add NuGet packages if needed
                if (hasNewtonsoftJsonReference)
                {
                    AddNewtonsoftJsonPackage(csprojPath);
                }

                // If DataMiner Files references were found, add the Dev.Automation NuGet package using dotnet add
                if (hasDataMinerFilesReferences)
                {
                    AddDevAutomationPackage(csprojPath);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error adding DLL references to .csproj: {ex.Message}");
                throw;
            }
        }
    }
}
