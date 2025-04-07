# 🛠️ SLC-Package-Converter

**SLC-Package-Converter** is a tool that automates the conversion of all your legacy `Microsoft.Net.Sdk`-based or old-build-style Automation Scripts into a new modern **DataMiner Package Project** format.


## ✨ Features

- Parses `.xml` files to extract project metadata.
- Automatically creates `Skyline.DataMiner.Sdk` projects.
- Merges `.csproj` files with the correct references and dependencies.
- Copies necessary files and folders while respecting exclusion rules.



## 🚀 Usage

### 1. Create a New DataMiner Package Project

1. Open **Visual Studio** and create a new **DataMiner Package Project**.
2. Uncheck **"Place solution and project in the same directory"**.
3. Enter your name as the **author**.
4. Leave **"Create DataMiner package"** checked.
5. Optionally add a **GitHub CI/CD workflow**.
6. Create the project.


### 2. Set Source and Destination Directories

In `Program.cs`, update the paths:

```csharp
const string SourceDirectory = @"C:\Path\To\Source";
const string DestinationDirectory = @"C:\Path\To\Destination";
```

- `SourceDirectory`: the folder where your current Automation Scripts are located (typically, the `.xml` files are in the root).
- `DestinationDirectory`: the path to the new project created in step 1.



### 3. Configure Exclusions (Optional)

To skip copying specific directories or files, customize the arrays below:

```csharp
string[] ExcludedDirs = { "CompanionFiles", "Internal", "Documentation", "Dlls" };
string[] ExcludedSubDirs = { };
string[] ExcludedFiles = { "AssemblyInfo.cs" };
```

You can use the default values or modify them as needed.


### 4. Run the Tool

Build and run the project using **Visual Studio** or the **.NET CLI**.

The tool will process the Automation Scripts and organize them into the new project structure.


## 🤝 Contributing

Contributions are welcome!  
Feel free to open an issue or submit a pull request with suggestions, improvements, or bug fixes.


## 📬 Contact

Have questions or feedback?  
Reach out to **[mauro.druwel@skyline.be](mailto:mauro.druwel@skyline.be)**