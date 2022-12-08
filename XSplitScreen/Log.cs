using BepInEx.Logging;

namespace DoDad.XSplitScreen
{
    internal static class Log
    {
        internal static LogLevel logLevel = LogLevel.None;
        internal static ManualLogSource _logSource;

        internal static void Init(ManualLogSource logSource)
        {
            _logSource = logSource;
        }

        internal static void LogOutput(object data, LogLevel level = LogLevel.Debug)
        {
            if (level > logLevel || logLevel == LogLevel.None)
                return;

            switch (level)
            {
                case LogLevel.Message:
                    _logSource.LogMessage(data);
                    break;
                case LogLevel.Info:
                    _logSource.LogInfo(data);
                    break;
                case LogLevel.Warning:
                    _logSource.LogWarning(data);
                    break;
                case LogLevel.Error:
                    _logSource.LogError(data);
                    break;
                case LogLevel.Fatal:
                    _logSource.LogFatal(data);
                    break;
                case LogLevel.Debug:
                    _logSource.LogDebug(data);
                    break;
            }
        }
        internal static void LogDebug(object data) => _logSource.LogDebug(data);
        internal static void LogError(object data) => _logSource.LogError(data);
        internal static void LogFatal(object data) => _logSource.LogFatal(data);
        internal static void LogInfo(object data) => _logSource.LogInfo(data);
        internal static void LogMessage(object data) => _logSource.LogMessage(data);
        internal static void LogWarning(object data) => _logSource.LogWarning(data);

        internal enum LogLevel
        { 
            None = 0,
            Message = 1,
            Info = 2,
            Warning = 3,
            Error = 4,
            Fatal = 5,
            Debug = 6,
            All = 7
        }

    }
}