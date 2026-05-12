namespace Core
{
    public static class Logger
    {
        public static void Log(string moduleName, string message)
        {
            UnityEngine.Debug.Log(FormatMessage(moduleName, message));
        }

        public static void Warning(string moduleName, string message)
        {
            UnityEngine.Debug.LogWarning(FormatMessage(moduleName, message));
        }

        public static void Error(string moduleName, string message)
        {
            UnityEngine.Debug.LogError(FormatMessage(moduleName, message));
        }

        private static string FormatMessage(string moduleName, string message)
        {
            return $"[LearningFramework][{moduleName}] {message}";
        }
    }
}
