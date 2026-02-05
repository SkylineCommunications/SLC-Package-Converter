# SLC-Package-Converter - Copilot Instructions

## Project Overview

This is a .NET 8 CLI tool that converts legacy DataMiner Automation Scripts into modern DataMiner Package Projects using the `Skyline.DataMiner.Sdk` format. It processes XML-based automation scripts and creates new project structures with updated references and dependencies.

## Build and Test Commands

```bash
# Build the project
dotnet publish SLC-Package-Converter/SLC-Package-Converter.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/windows

# Run the tool (after building)
SLC-Package-Converter.exe <arguments>
```

No test suite exists in this project.

## Architecture

### Core Workflow

1. **Program.cs**: Entry point that orchestrates the conversion process:
   - Parses CLI arguments
   - Creates destination package project if needed (via `dotnet new dataminer-package-project`)
   - Calls processors in sequence: XmlProcessor → DirectoryHelper → SolutionHelper
   - Optionally creates a Git branch for the converted output

2. **XmlProcessor**: Processes automation script XML files:
   - Parses each `<Exe>` element in XML as a separate project
   - Extracts project names from `[Project:...]` tags
   - Handles multiple EXE blocks per XML file with automatic name collision resolution
   - Creates new projects using `dotnet new dataminer-automation-project` or `dataminer-automation-library-project`
   - Applies package replacement rules (see below)

3. **DirectoryHelper**: Copies non-XML files and directories while respecting exclusions
   - Sanitizes `.csproj` files by removing `AssemblyInfo.cs` references

4. **SolutionHelper**: Manages solution file operations:
   - Adds shared project references (`.shproj`) from source to destination
   - Uses `dotnet sln add` to register projects

5. **BranchManager**: Creates Git branches for the converted output (when `--destDir` is not specified)

### Key Data Model

**ScriptExe** (Models/ScriptExe.cs): Represents an automation script EXE block from XML:
- `Type`, `IsPrecompile`, `LibraryName`: Script metadata
- `CSharpCode`: Extracted C# code (after removing `[Project:...]` tag)
- `DllReferences`: List of DLL reference paths from `<Param type="ref">` elements

### Package Replacement Rules

XmlProcessor automatically replaces deprecated/obsolete packages:

| Old Reference | New Reference |
|---------------|---------------|
| `SLC.Lib.Automation` | `Skyline.DataMiner.Core.DataMinerSystem.Automation` NuGet |
| `SLC.Lib.Common` | `Skyline.DataMiner.Core.DataMinerSystem.Automation` NuGet |
| `AutomationScript_ClassLibrary` project | `Skyline.DataMiner.Core.DataMinerSystem.Automation` NuGet |
| Absolute DLL paths to `C:\Skyline DataMiner\Files\` | Relative paths to `..\Dlls\` or removed if included in `Skyline.DataMiner.Dev.Automation` |

**Special handling:**
- DLLs included in `Skyline.DataMiner.Dev.Automation` (SLManagedAutomation, SLNetTypes, SLLoggerUtil, Skyline.DataMiner.Storage.Types) are removed entirely
- `SLSRMLibrary.dll` is NOT automatically replaced with NuGet (conservative approach for production SRM)
- EXE blocks with `_63000` suffix are skipped (reference AutomationScript_ClassLibrary)

## Key Conventions

### Project Naming

- **Numeric suffix removal**: All numeric suffixes (e.g., `_1`, `_2`, `_4`) are removed from project names extracted from XML
- **Collision handling**: When multiple EXE blocks result in the same name, auto-append `_2`, `_3`, etc.
- **Special exclusion**: Projects ending with `_63000` are skipped entirely

### File Exclusions

Hardcoded exclusions in Program.cs:
- **Directories**: `CompanionFiles`, `Internal`, `Documentation`, `AutomationScript_ClassLibrary`
- **Files**: `AssemblyInfo.cs`, `Jenkinsfile`, `Directory.Build.props`, `Directory.Build.targets`

### XML Processing

- Handles both namespaced and non-namespaced XML elements
- Preserves UTF-8 BOM encoding when writing XML/CS files (required by DataMiner)
- Skips XML files not listed in any solution file (warns as "leftover" files)

### Git Branch Creation

When `--destDir` is not provided:
- Creates a temporary directory with generated package project
- Copies converted files to a new Git branch (default: `converted-package`)
- `--preserveHistory`: Creates branch from current branch (default: orphan branch)

## Command-Line Interface

Required:
- `--sourceDir <path>`: Source directory containing automation scripts

Optional:
- `--destDir <path>`: Destination directory (auto-creates package project if omitted)
- `--solutionName <name>`: Custom solution file name (defaults to source solution name)
- `--includeGitHubWorkflow <None|Basic|Complete>`: GitHub workflow type (default: Complete)
- `--branchName <name>`: Git branch name when auto-creating (default: converted-package)
- `--preserveHistory`: Preserve git history in new branch
- `--debug`: Enable verbose logging

## Logging

Uses custom Logger utility with two modes:
- Normal: Info, Warning, Error messages
- Debug mode (`--debug` flag): Includes detailed operation traces with `Logger.LogDebug()`
