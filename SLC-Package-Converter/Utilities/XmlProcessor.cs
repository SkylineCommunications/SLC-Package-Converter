using SLC_Package_Converter.Models;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace SLC_Package_Converter.Utilities
{
    public static class XmlProcessor
    {
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

                        // Skip files with multiple Exe elements
                        if (exeElements.Count() > 1)
                        {
                            Logger.LogWarning($"Multiple Exe elements found in {file}. Skipping the file.");
                            continue;
                        }

                        if (string.IsNullOrEmpty(scriptName))
                        {
                            Logger.LogWarning($"No script name found in {file}. Skipping the file.");
                            continue;
                        }

                        foreach (var exe in exeElements)
                        {
                            var projectValue = exe.Element(ns + "Value")?.Value;
                            if (projectValue != null && projectValue.Contains("[Project:"))
                            {
                                // Extract the project name from the project value
                                string projectName = ExtractProjectName(projectValue);
                                string newName = Regex.Replace(
                                    projectName,
                                    @"_\d+$", // Matches an underscore followed by one or more digits at the end of the string
                                    string.Empty // Replaces the match with an empty string
                                );

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

                                // Remove the scriptName.xml and scriptName.cs files
                                string projectDirectory = Path.Combine(destDir, newName);
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

                foreach (var packageReference in sourcePackageReferences)
                {
                    var existingPackageReference = packageReferenceGroup.Elements("PackageReference")
                        .FirstOrDefault(e => e.Attribute("Include")?.Value == packageReference.Attribute("Include")?.Value);
                    
                    if (existingPackageReference == null)
                    {
                        packageReferenceGroup.Add(new XElement(packageReference));
                    }
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

                // If AutomationScript_ClassLibrary was referenced, add the NuGet package instead
                if (hasAutomationScriptClassLibraryReference)
                {
                    var nugetPackageName = "Skyline.DataMiner.Core.DataMinerSystem.Automation";
                    var existingNugetReference = packageReferenceGroup.Elements("PackageReference")
                        .FirstOrDefault(e => e.Attribute("Include")?.Value == nugetPackageName);
                    
                    if (existingNugetReference == null)
                    {
                        var nugetReference = new XElement("PackageReference", new XAttribute("Include", nugetPackageName));
                        packageReferenceGroup.Add(nugetReference);
                        Logger.LogInfo($"Replaced AutomationScript_ClassLibrary reference with NuGet package {nugetPackageName}");
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
                Logger.LogError($"Error merging .csproj files: {ex.Message}");
                throw;
            }
        }
    }
}
