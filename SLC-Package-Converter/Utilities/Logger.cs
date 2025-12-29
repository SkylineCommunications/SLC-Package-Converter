namespace SLC_Package_Converter.Utilities
{
    public static class Logger
    {
        public static bool DebugMode { get; set; } = false;

        public static void LogInfo(string message) => Console.WriteLine($"INFO: {message}");
        public static void LogWarning(string message) => Console.WriteLine($"WARNING: {message}");
        public static void LogError(string message) => Console.WriteLine($"ERROR: {message}");
        public static void LogDebug(string message)
        {
            if (DebugMode)
            {
                Console.WriteLine($"DEBUG: {message}");
            }
        }
    }
}