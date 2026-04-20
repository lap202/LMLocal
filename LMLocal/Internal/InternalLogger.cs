using System;
using System.Diagnostics;
using System.IO;
using System.Text;

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

    /// <summary>
    /// Writes log messages directly to Debug output (synchronous, no buffering).
    /// </summary>
    internal class DebugLogger : IInternalLogger
    {
        public void Info(string message) => Write("INFO", message);
        public void Warn(string message) => Write("WARN", message);
        public void Error(string message, Exception ex = null)
            => Write("ERROR", message + (ex != null ? $" | {ex.Message}" : ""));

        private void Write(string level, string message)
        {
            string line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
            Debug.WriteLine(line);
        }
    }

    /// <summary>
    /// Writes log messages directly to a file (synchronous, thread-safe).
    /// </summary>
    internal class FileLogger : IInternalLogger
    {
        private readonly string _filePath;
        private readonly object _lock = new object();

        public FileLogger(string filePath = null)
        {
            _filePath = string.IsNullOrWhiteSpace(filePath)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt")
                : filePath;

            // Ensure directory exists
            string dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        public void Info(string message) => Write("INFO", message);
        public void Warn(string message) => Write("WARN", message);
        public void Error(string message, Exception ex = null)
            => Write("ERROR", message + (ex != null ? $" | {ex.Message}" : ""));

        private void Write(string level, string message)
        {
            string line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
            lock (_lock)
            {
                try
                {
                    File.AppendAllText(_filePath, line + Environment.NewLine, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    // Fallback to debug if file write fails
                    Debug.WriteLine($"[FileLogger Fallback] {ex.Message}: {line}");
                }
            }
        }
    }

    /// <summary>
    /// Static entry point for logging. All methods are removed in Release builds ([Conditional("DEBUG")]).
    /// </summary>
    internal static class InternalLogger
    {
        private static IInternalLogger _instance = new DebugLogger();
        private static readonly object _lock = new object();

        public static void SetLogger(IInternalLogger logger)
        {
            lock (_lock)
                _instance = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [Conditional("DEBUG")]
        public static void Info(string message) => SafeLog(l => l.Info(message));

        [Conditional("DEBUG")]
        public static void Warn(string message) => SafeLog(l => l.Warn(message));

        [Conditional("DEBUG")]
        public static void Error(string message, Exception ex = null) => SafeLog(l => l.Error(message, ex));

        private static void SafeLog(Action<IInternalLogger> action)
        {
            try { action(_instance); }
            catch (Exception ex) { Debug.WriteLine($"[InternalLogger] {ex.Message}"); }
        }
    }
}
