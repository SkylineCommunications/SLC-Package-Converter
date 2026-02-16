# 🛠️ SLC-Package-Converter

**Automate legacy DataMiner Automation Script conversion to modern Package Projects.**

---

## 🚀 Quick Start

Download the latest release from the [Releases page](https://github.com/SkylineCommunications/SLC-Package-Converter/releases/latest) and extract the executable.

| Command | Description |
|---------|-------------|
| `SLC-Package-Converter.exe --sourceDir <SourceDirectory>` | Convert scripts in folder |
| `--destDir <DestinationDirectory>` | Output to custom folder |
| `--solutionName <CustomName>` | Specifies a custom solution name following the [naming conventions](https://docs.dataminer.services/develop/CICD/Skyline%20Communications/Github/Use_Github_Guidelines.html) (e.g., `SLC-AS-MediaOps`). |
| `--solutionFormat <slnx|sln>` | Solution format (default: `slnx`). Use `sln` for legacy format. |
| `--includeGitHubWorkflow <None|Basic|Complete>` | Add GitHub workflow |
| `--branchName <BranchName>` | The newly created branch name |
| `--preserveHistory` | Preserve git history |
| `--debug` | Verbose logging |

**Best production example:**
```bash
SLC-Package-Converter.exe --sourceDir "C:\Path\To\Source" --includeGitHubWorkflow None --branchName main --preserveHistory --solutionName "SLC-AS-MediaOps"
```

> **Note:** In this example, the new branch name is `main` because the old one followed the pattern `1.0.0.X`.
> If your branch name is already `main`, choose a different name.

**Other examples:**
```bash
# Basic conversion
SLC-Package-Converter.exe --sourceDir "C:\Source"

# Specify destination directory
SLC-Package-Converter.exe --sourceDir "C:\Source" --destDir "C:\Dest"

# Add GitHub workflow
SLC-Package-Converter.exe --sourceDir "C:\Source" --includeGitHubWorkflow "Basic"
```

---

## ✨ Key Features

- Converts legacy Automation Scripts to DataMiner Package Projects
- Handles multiple EXE blocks per XML (auto naming, collision resolution)
- Updates references, replaces obsolete packages, fixes paths
- Creates new Git branch if no destination specified
- Supports custom solution/project/branch names
- GitHub workflow integration (optional)

---

## 📦 EXE Block Handling

| Case | Result |
|------|--------|
| `MyScript_1`, `MyScript_2` | Both become `MyScript` (auto suffixes if needed) |
| `MyLibrary_63000` | Skipped (ClassLibrary replaced by NuGet) |
| Name collision | Auto appends `_2`, `_3`, etc. |

---

## 🛠️ DLL & Reference Replacement

| Legacy Reference/Package                | Replacement/Action                                      |
|-----------------------------------------|---------------------------------------------------------|
| SLC.Lib.Automation, SLC.Lib.Common      | Skyline.DataMiner.Core.DataMinerSystem.Automation       |
| AutomationScript_ClassLibrary           | Replaced by NuGet, EXE blocks with _63000 skipped       |
| SLManagedAutomation.dll, SLNetTypes.dll, SLLoggerUtil.dll, Skyline.DataMiner.Storage.Types.dll | Removed (use Skyline.DataMiner.Dev.Automation NuGet) |
| SLSRMLibrary.dll, DataMinerSolutions.dll, custom DLLs | Updated to point to ..\Dlls\ folder (manual placement if missing) |

- Absolute paths are converted to relative paths (..\Dlls\)
- SLSRMLibrary: Conservative handling, manual DLL placement if needed
- If DLL not found, reference updated to solution-level Dlls folder (user must add file)
- Exclude files/folders: Edit `Program.cs` and rebuild

---

## 🤝 Contributing & Contact

- Issues and PRs welcome!
- Contact: [mauro.druwel@skyline.be](mailto:mauro.druwel@skyline.be)

---

[Latest Release](https://github.com/SkylineCommunications/SLC-Package-Converter/releases/latest)
