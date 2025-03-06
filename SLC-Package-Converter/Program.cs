using System.Xml.Linq;
using System.IO;
using System.Text.RegularExpressions;

string sourceDirectory = @"C:\GIT\SLC-AS-MediaOps";
string destinationDirectory = @"C:\GIT\SLC-MediaOps";

// Search for the .sln file in the destination directory
string[] slnFiles = Directory.GetFiles(destinationDirectory, "*.sln", SearchOption.TopDirectoryOnly);
if (slnFiles.Length > 0)
{
    string slnFile = slnFiles[0];
    string slnContent = File.ReadAllText(slnFile);

    // Extract the project ID of the first project
    string projectIdPattern = @"Project\(""\{(?<id>[A-F0-9\-]+)\}""\)";
    Match match = Regex.Match(slnContent, projectIdPattern);
    if (match.Success)
    {
        string projectId = match.Groups["id"].Value;
        Console.WriteLine($"First project ID: {projectId}");
    }
}

if (Directory.Exists(sourceDirectory))
{
    string[] xmlFiles = Directory.GetFiles(sourceDirectory, "*.xml", SearchOption.TopDirectoryOnly);

    foreach (string file in xmlFiles)
    {
        // Console.WriteLine($"Processing file: {file}");
        XDocument doc = XDocument.Load(file);
        XNamespace ns = "http://www.skyline.be/automation";

        var exeElements = doc.Descendants(ns + "Exe");
        foreach (var exe in exeElements)
        {
            var projectValue = exe.Element(ns + "Value")?.Value;
            if (projectValue != null && projectValue.Contains("[Project:"))
            {
                int startIndex = projectValue.IndexOf("[Project:") + "[Project:".Length;
                int endIndex = projectValue.IndexOf("]", startIndex);
                string projectName = projectValue.Substring(startIndex, endIndex - startIndex);
                // Console.WriteLine($"Found project: {projectName}");

                string projectPath = Path.Combine(sourceDirectory, projectName);
                if (Directory.Exists(projectPath))
                {
                    // Console.WriteLine($"Project directory exists: {projectPath}");
                    string destinationProjectPath = Path.Combine(destinationDirectory, projectName);
                    DirectoryCopy(projectPath, destinationProjectPath, true);

                    string destinationXmlPath = Path.Combine(destinationProjectPath, Path.GetFileName(file));
                    File.Copy(file, destinationXmlPath, true);
                }
                else
                {
                    Console.WriteLine($"Project directory does not exist: {projectPath}");
                }
            }
        }
    }
}
else
{
    Console.WriteLine("Source directory does not exist.");
}

static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
{
    DirectoryInfo dir = new DirectoryInfo(sourceDirName);
    DirectoryInfo[] dirs = dir.GetDirectories();

    if (!dir.Exists)
    {
        throw new DirectoryNotFoundException(
            "Source directory does not exist or could not be found: "
            + sourceDirName);
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
