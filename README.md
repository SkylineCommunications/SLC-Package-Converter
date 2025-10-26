# 🛠️ SLC-Package-Converter

**SLC-Package-Converter** is a tool that automates the conversion of legacy `Microsoft.Net.Sdk`-based or old-build-style Automation Scripts into a modern **DataMiner Package Project** format.

## ✨ Features

- Parses all `.xml` files and creates corresponding Automation Script Projects.
- Automatically adds newly created `Skyline.DataMiner.Sdk` Automation Script Projects to the DataMiner Package Project.
- Merges `.csproj` files with the correct references and dependencies.
- Copies necessary files and folders while respecting exclusion rules.
- Automatically creates a new Git branch (`converted-package`) if no destination is specified.
- **Project names are automatically derived from the source**: 
  - Automation Script projects use the project name extracted from the XML file's `[Project:...]` reference
  - The DataMiner Package Project uses the source solution file name by default, or "Package" when using `--usePackageNaming` with an "AutomationScript" solution

## 🚀 Usage

### 1. Download the Latest Release

Download the latest release of the tool from the [**Releases**](https://github.com/SkylineCommunications/SLC-Package-Converter/releases/latest) section of this repository.

### 2. Run the Tool

Run the tool using the following command:

```bash
SLC-Package-Converter.exe --sourceDir <SourceDirectory> [--destDir <DestinationDirectory>] [--usePackageNaming] [--includeGitHubWorkflow <None|Basic|Complete>] [--branchName <BranchName>] [--preserveHistory]
```

- `--sourceDir`: The folder where your current Automation Scripts are located (e.g., the repository folder).
- `--destDir` (optional):  
  - If you already created a new DataMiner Package Project, specify the destination directory.  
  - If omitted, the tool will automatically create a new package project in a new Git branch named `converted-package`.
- `--usePackageNaming` (optional): When specified and the source solution is named "AutomationScript", uses "Package" as the DataMiner Package Project name instead of "AutomationScript".
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

#### Use Package naming convention
```bash
SLC-Package-Converter.exe --sourceDir "C:\Path\To\Source" --usePackageNaming
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
SLC-Package-Converter.exe --sourceDir "C:\Path\To\Source" --usePackageNaming --branchName "feature/package-migration" --includeGitHubWorkflow "Basic" --preserveHistory
```


### 🔧 Customizing Excluded Files

To modify the list of excluded files or folders, clone this repository and update the relevant variables in the `Program.cs` file. After making the changes, rebuild the tool to apply your customizations.

## 🤝 Contributing

Contributions are welcome!  
Feel free to open an issue or submit a pull request with suggestions, improvements, or bug fixes.

## 📬 Contact

Have questions or feedback?  
Reach out to **[mauro.druwel@skyline.be](mailto:mauro.druwel@skyline.be)**.
