# 🛠️ SLC-Package-Converter

**SLC-Package-Converter** is a tool that automates the conversion of legacy `Microsoft.Net.Sdk`-based or old-build-style Automation Scripts into a modern **DataMiner Package Project** format.

## ✨ Features

- Parses all `.xml` files and creates corresponding Automation Script Projects.
- **Supports automation scripts with multiple EXE blocks** - processes each EXE block as a separate project with unique naming.
- Automatically adds newly created `Skyline.DataMiner.Sdk` Automation Script Projects to the DataMiner Package Project.
- Merges `.csproj` files with the correct references and dependencies.
- **Automatically replaces deprecated/obsolete packages and references**:
  - `SLC.Lib.Automation` → `Skyline.DataMiner.Core.DataMinerSystem.Automation`
  - `SLC.Lib.Common` → `Skyline.DataMiner.Core.DataMinerSystem.Automation`
  - `AutomationScript_ClassLibrary` project references → `Skyline.DataMiner.Core.DataMinerSystem.Automation`
  - References to `C:\Skyline DataMiner\Files\` → `Skyline.DataMiner.Dev.Automation` (version 10.4.0.22)
- **Conservative handling of SLSRMLibrary**:
  - Does NOT automatically replace with NuGet package (safer for production SRM environments)
  - If `SLSRMLibrary.dll` exists in `Dlls` folder (solution or project level), references are updated to use that file
  - If reference points to `C:\Skyline DataMiner\Files\SLSRMLibrary.dll` and no DLL found in repository, path is updated to point to solution-level `Dlls` folder (user must add file manually)
- Copies necessary files and folders while respecting exclusion rules.
- Automatically creates a new Git branch (`converted-package`) if no destination is specified.
- **Project names are automatically derived from the source**: 
  - Automation Script projects use the project name extracted from the XML file's `[Project:...]` reference
  - The DataMiner Package Project is always named "Package"
  - The Solution name uses the source solution file name by default, or can be customized using `--solutionName`

## 📋 Multiple EXE Blocks Support

The tool **fully supports automation scripts with multiple EXE blocks**. Each EXE block in your XML file will be processed as a separate automation script project.

### How It Works

When an automation script XML file contains multiple `<Exe>` elements, the tool:
1. Creates a separate project for each EXE block
2. Extracts the project name from each EXE block's `[Project:...]` reference
3. Handles numeric suffixes intelligently (see below)

### Numeric Suffix Handling

Project names with numeric suffixes are handled as follows:

- **`_1` suffix** is **removed** (treated as the first/default instance)
  - Example: `MyScript_1` → `MyScript`
  - This allows the first EXE block to use the base name without a suffix

- **Other numeric suffixes** (e.g., `_2`, `_3`, `_4`) are **preserved** to support multiple EXE blocks
  - Example: `MyAutomation_2` → `MyAutomation_2`
  - Example: `MyAutomation_3` → `MyAutomation_3`
  - These suffixes allow you to have multiple EXE blocks in the same XML file

- **EXE blocks with `_63000` suffix** are **skipped entirely** (not processed)
  - Example: `MyLibrary_63000` → skipped (entire EXE block excluded from processing)
  - These reference `AutomationScript_ClassLibrary` projects whose folders are excluded and replaced by NuGet packages
  - The entire reference is removed from the XML output
  
- **Automatic collision handling**: If multiple EXE blocks result in the same project name (after removing `_1`), the tool automatically appends `_2`, `_3`, etc.
  - Example: Two EXE blocks both named `MyScript` → become `MyScript` and `MyScript_2`
  - Example: `MyScript_1` and `MyScript` → both become `MyScript` after suffix removal, so they become `MyScript` and `MyScript_2`

### Important Notes

- ✅ **Multiple EXE blocks are fully supported** - there is no limit on the number of EXE blocks per XML file
- ✅ **Automatic naming conflict resolution**: When name collisions occur, numeric suffixes are automatically added starting from `_2`
- 💡 **Best practice**: Use `_1` for the first instance, `_2`, `_3` for additional instances, or use distinct base names
- ℹ️ **Note about AutomationScript_ClassLibrary**: The `AutomationScript_ClassLibrary` folder is excluded during conversion as its functionality is replaced by the `Skyline.DataMiner.Core.DataMinerSystem.Automation` NuGet package. EXE blocks with `_63000` suffix are skipped entirely and removed from the XML.

## 🚀 Usage

### 1. Download the Latest Release

Download the latest release of the tool from the [**Releases**](https://github.com/SkylineCommunications/SLC-Package-Converter/releases/latest) section of this repository.

### 2. Run the Tool

Run the tool using the following command:

```bash
SLC-Package-Converter.exe --sourceDir <SourceDirectory> [--destDir <DestinationDirectory>] [--solutionName <CustomName>] [--includeGitHubWorkflow <None|Basic|Complete>] [--branchName <BranchName>] [--preserveHistory]
```

- `--sourceDir`: The folder where your current Automation Scripts are located (e.g., the repository folder).
- `--destDir` (optional):  
  - If you already created a new DataMiner Package Project, specify the destination directory.  
  - If omitted, the tool will automatically create a new package project in a new Git branch named `converted-package`.
- `--solutionName <CustomName>` (optional):
  - Specifies a custom name for the solution file (.sln).
  - If not specified, uses the source solution file name.
  - Note: The DataMiner Package Project is always named "Package".
- `--includeGitHubWorkflow` (optional): Type of GitHub workflow to include. Options:
  - `None`: No GitHub workflow
  - `Basic`: Basic GitHub workflow (build, test, publish)
  - `Complete`: Complete GitHub workflow (Skyline Quality Gate) - **default value**
- `--branchName` (optional): Name of the Git branch to create when no destination directory is provided. **Default:** `converted-package`
- `--preserveHistory` (optional): When specified, preserves git history by creating the new branch from the current branch instead of creating an orphan branch. **Default:** Creates orphan branch (no base)

### 3. Examples

#### Basic Usage (with destination directory)
```bash
SLC-Package-Converter.exe --sourceDir "C:\Path\To\Source" --destDir "C:\Path\To\Destination"
```

#### Auto-create new package project (no destination directory)
```bash
SLC-Package-Converter.exe --sourceDir "C:\Path\To\Source"
```

#### Custom branch name (when no destination directory)
```bash
SLC-Package-Converter.exe --sourceDir "C:\Path\To\Source" --branchName "feature/new-package-structure"
```

#### Use custom solution name
```bash
# Use custom solution name "MyCustomSolution" for the .sln file
SLC-Package-Converter.exe --sourceDir "C:\Path\To\Source" --solutionName "MyCustomSolution"
```

#### Create branch preserving git history
```bash
SLC-Package-Converter.exe --sourceDir "C:\Path\To\Source" --branchName "feature/converted-package" --preserveHistory
```

#### Including different GitHub workflows
```bash
# No GitHub workflow
SLC-Package-Converter.exe --sourceDir "C:\Path\To\Source" --includeGitHubWorkflow "None"

# Basic GitHub workflow (build, test, publish)
SLC-Package-Converter.exe --sourceDir "C:\Path\To\Source" --includeGitHubWorkflow "Basic"

# Complete GitHub workflow with Skyline Quality Gate (default)
SLC-Package-Converter.exe --sourceDir "C:\Path\To\Source" --includeGitHubWorkflow "Complete"
```

#### Comprehensive example (multiple arguments)
```bash
SLC-Package-Converter.exe --sourceDir "C:\Path\To\Source" --solutionName "MyPackage" --branchName "feature/package-migration" --includeGitHubWorkflow "Basic" --preserveHistory
```


### 🔧 Customizing Excluded Files

To modify the list of excluded files or folders, clone this repository and update the relevant variables in the `Program.cs` file. After making the changes, rebuild the tool to apply your customizations.

## 🤝 Contributing

Contributions are welcome!  
Feel free to open an issue or submit a pull request with suggestions, improvements, or bug fixes.

## 📬 Contact

Have questions or feedback?  
Reach out to **[mauro.druwel@skyline.be](mailto:mauro.druwel@skyline.be)**.
