# 🛠️ SLC-Package-Converter

**SLC-Package-Converter** is a tool that automates the conversion of all your legacy `Microsoft.Net.Sdk`-based or old-build-style Automation Scripts into a modern **DataMiner Package Project** format.


## ✨ Features

- Parses `.xml` files to extract project metadata.
- Automatically creates `Skyline.DataMiner.Sdk` projects.
- Merges `.csproj` files with the correct references and dependencies.
- Copies necessary files and folders while respecting exclusion rules.
- Automatically creates a new Git branch (`converted-package`) if no destination is specified.


## 🚀 Usage

### 1. Set Source and Destination Directories

Open `Program.cs` and update the paths:

```csharp
const string SourceDirectory = @"C:\Path\To\Source";
const string DestinationDirectory = ""; // Leave empty to auto-create project
```

- `SourceDirectory`: the folder where your current Automation Scripts are located (typically, the `.xml` files are in the root).
- `DestinationDirectory`:  
  - If you already created a new DataMiner Package Project, point to that directory.  
  - **Leave empty** to let the tool automatically create a new package project ( in a new Git branch called `converted-package`.


### 2. Configure Exclusions (Optional)

To skip copying specific directories or files, customize the arrays below:

```csharp
string[] ExcludedDirs = { "CompanionFiles", "Internal", "Documentation", "Dlls" };
string[] ExcludedSubDirs = { };
string[] ExcludedFiles = { "AssemblyInfo.cs" };
```

Modify these lists to suit your project needs.


### 3. Run the Tool

Build and run the project using **Visual Studio** or the **.NET CLI**.

The tool will:
- Extract project metadata.
- Generate a valid `Skyline.DataMiner.Sdk` package project.
- Organize and copy files.
- If no destination was provided, automatically:
  - Create a new package project.
  - Switch to a Git branch named `converted-package`.
  - Place the converted content there.


## 🤝 Contributing

Contributions are welcome!  
Feel free to open an issue or submit a pull request with suggestions, improvements, or bug fixes.

## 📬 Contact

Have questions or feedback?  
Reach out to **[mauro.druwel@skyline.be](mailto:mauro.druwel@skyline.be)**
