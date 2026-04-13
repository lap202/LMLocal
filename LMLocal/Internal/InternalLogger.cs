using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace LMLocal.Internal
{
    /// <summary>
    /// Lightweight internal logger. Calls are conditional on DEBUG so they are omitted in Release builds.
    /// Also exposes an interface for future DI if needed.
    /// </summary>
    internal interface IInternalLogger
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message, Exception ex = null);
    }

    internal class DebugLogger : IInternalLogger
    {
        public void Info(string message) => Write("INFO", message);
        public void Warn(string message) => Write("WARN", message);
        public void Error(string message, Exception ex = null) => Write("ERROR", message + (ex != null ? " | " + ex : string.Empty));

        private void Write(string level, string message)
        {
            try
            {
                var text = $"- LML - [{DateTime.UtcNow:O}] {level}: {message}";
                Debug.WriteLine(text);
            }
            catch
            {
                // Swallow any logging errors
            }
        }
    }

    internal static class InternalLogger
    {
        // Default instance — can be overridden in tests if needed.
        private static IInternalLogger _instance = new DebugLogger();

        public static void SetLogger(IInternalLogger logger)
        {
            _instance = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [Conditional("DEBUG")]
        public static void Info(string message)
        {
            try { _instance.Info(message); } catch { }
        }

        [Conditional("DEBUG")]
        public static void Warn(string message)
        {
            try { _instance.Warn(message); } catch { }
        }

        [Conditional("DEBUG")]
        public static void Error(string message, Exception ex = null)
        {
            try { _instance.Error(message, ex); } catch { }
        }
    }
}
