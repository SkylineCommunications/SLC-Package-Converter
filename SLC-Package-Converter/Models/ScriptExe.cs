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
            var paramElements = GetDescendants(exeElement, ns, "Param");
            
            Type = paramElements.FirstOrDefault()?.Attribute("type")?.Value;
            IsPrecompile = paramElements.Any(p => p.Attribute("type")?.Value == "preCompile" && p.Value == "true");
            LibraryName = paramElements.FirstOrDefault(p => p.Attribute("type")?.Value == "libraryName")?.Value;
        }

        // Helper method to get all descendant elements with the given name, handling both namespaced and non-namespaced elements.
        private static IEnumerable<XElement> GetDescendants(XElement parent, XNamespace ns, string elementName)
        {
            var namespacedElements = parent.Descendants(ns + elementName).ToList();
            return namespacedElements.Any() ? namespacedElements : parent.Descendants(elementName);
        }
    }
}
