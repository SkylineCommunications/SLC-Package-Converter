using System.Xml.Linq;

namespace SLC_Package_Converter.Models
{
    // Represents a ScriptExe object. (Automation Script XML)
    public class ScriptExe
    {
        public string? Type { get; set; }
        public bool IsPrecompile { get; set; }
        public string? LibraryName { get; set; }

        // Initializes a new instance of the ScriptExe class from an XML element.
        public ScriptExe(XElement exeElement)
        {
            // Extract properties from the XML element
            Type = exeElement.Element("Param")?.Attribute("type")?.Value;
            IsPrecompile = exeElement.Descendants("Param")
                                      .Any(p => p.Attribute("type")?.Value == "preCompile" && p.Value == "true");
            LibraryName = exeElement.Descendants("Param")
                                    .FirstOrDefault(p => p.Attribute("type")?.Value == "libraryName")?.Value;
        }
    }
}
