namespace SLC_Package_Converter.Utilities
{
    public static class Logger
    {
        private static readonly List<string> ReproductionSteps = new List<string>();
        private static int stepCounter = 0;
        
        public static void LogInfo(string message) => Console.WriteLine($"INFO: {message}");
        public static void LogWarning(string message) => Console.WriteLine($"WARNING: {message}");
        public static void LogError(string message) => Console.WriteLine($"ERROR: {message}");
        
        // Add a step to the manual reproduction guide
        public static void AddReproductionStep(string step)
        {
            stepCounter++;
            string formattedStep = $"Step {stepCounter}: {step}";
            ReproductionSteps.Add(formattedStep);
        }
        
        // Output the complete manual reproduction guide
        public static void LogReproductionGuide()
        {
            if (ReproductionSteps.Count == 0)
            {
                return;
            }
            
            LogInfo("=== Manual Reproduction Guide ===");
            LogInfo("To manually reproduce what this tool did, follow these steps:");
            LogInfo("");
            
            foreach (var step in ReproductionSteps)
            {
                LogInfo(step);
            }
            
            LogInfo("");
            LogInfo("=== End of Manual Reproduction Guide ===");
        }
    }
}