using System.Xml.Linq;

namespace SLC_Package_Converter.Models
{
    // Represents a ScriptExe object. (Automation Script XML)
    public class ScriptExe
    {
        public string? Type { get; set; }
        public bool IsPrecompile { get; set; }
        public string? LibraryName { get; set; }
        public string? CSharpCode { get; set; }
        public List<string> DllReferences { get; set; }

        // Initializes a new instance of the ScriptExe class from an XML element.
        public ScriptExe(XElement exeElement)
        {
            DllReferences = new List<string>();
            
            // Get the namespace from the element
            XNamespace ns = exeElement.Name.Namespace;

            // Extract properties from the XML element, handling both namespaced and non-namespaced elements
            var paramElements = GetDescendants(exeElement, ns, "Param");
            
            Type = paramElements.FirstOrDefault()?.Attribute("type")?.Value;
            IsPrecompile = paramElements.Any(p => p.Attribute("type")?.Value == "preCompile" && p.Value == "true");
            LibraryName = paramElements.FirstOrDefault(p => p.Attribute("type")?.Value == "libraryName")?.Value;
            
            // Extract DLL references from Param elements with type="ref"
            foreach (var param in paramElements.Where(p => p.Attribute("type")?.Value == "ref"))
            {
                string? dllPath = param.Value;
                if (!string.IsNullOrEmpty(dllPath))
                {
                    DllReferences.Add(dllPath);
                }
            }
            
            // Extract C# code from the Value element
            var valueElement = GetFirstElement(exeElement, ns, "Value");
            if (valueElement != null)
            {
                string? fullValue = valueElement.Value;
                if (!string.IsNullOrEmpty(fullValue))
                {
                    // Remove the [Project:...] tag if present
                    if (fullValue.Contains("[Project:"))
                    {
                        int projectEndIndex = fullValue.IndexOf("]");
                        if (projectEndIndex >= 0 && projectEndIndex + 1 < fullValue.Length)
                        {
                            CSharpCode = fullValue.Substring(projectEndIndex + 1).TrimStart('\r', '\n');
                        }
                    }
                    else
                    {
                        CSharpCode = fullValue;
                    }
                }
            }
        }

        // Helper method to get the first child element with the given name, handling both namespaced and non-namespaced elements.
        private static XElement? GetFirstElement(XElement parent, XNamespace ns, string elementName)
        {
            return parent.Element(ns + elementName) ?? parent.Element(elementName);
        }

        // Helper method to get all descendant elements with the given name, handling both namespaced and non-namespaced elements.
        private static IEnumerable<XElement> GetDescendants(XElement parent, XNamespace ns, string elementName)
        {
            var namespacedElements = parent.Descendants(ns + elementName).ToList();
            if (namespacedElements.Any())
            {
                return namespacedElements;
            }
            return parent.Descendants(elementName).ToList();
        }
    }
}
