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
            // Get the namespace from the element
            XNamespace ns = exeElement.Name.Namespace;

            // Extract properties from the XML element, handling both namespaced and non-namespaced elements
            Type = exeElement.Element(ns + "Param")?.Attribute("type")?.Value 
                   ?? exeElement.Element("Param")?.Attribute("type")?.Value;
            
            IsPrecompile = exeElement.Descendants(ns + "Param")
                                      .Any(p => p.Attribute("type")?.Value == "preCompile" && p.Value == "true")
                           || exeElement.Descendants("Param")
                                      .Any(p => p.Attribute("type")?.Value == "preCompile" && p.Value == "true");
            
            LibraryName = exeElement.Descendants(ns + "Param")
                                    .FirstOrDefault(p => p.Attribute("type")?.Value == "libraryName")?.Value
                          ?? exeElement.Descendants("Param")
                                    .FirstOrDefault(p => p.Attribute("type")?.Value == "libraryName")?.Value;
        }
    }
}
