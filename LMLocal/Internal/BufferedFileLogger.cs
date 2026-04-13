using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace LMLocal.Internal
{
    public class BufferedFileLogger : IDisposable
    {
        private readonly List<string> _buffer = new List<string>();
        private readonly object _lock = new object();
        private readonly string _filePath;
        private readonly int _flushThreshold;
        private readonly Timer _flushTimer;
        private bool _disposed;

        public BufferedFileLogger(string filePath, int flushThreshold = 100, int flushIntervalMs = 5000)
        {
            _filePath = filePath;
            _flushThreshold = flushThreshold;
            _flushTimer = new Timer(FlushTimerCallback, null, flushIntervalMs, flushIntervalMs);
        }

        private void Log(string level, string message, Exception ex = null)
        {
            var logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
            if (ex != null)
                logLine += $"{Environment.NewLine}{ex}";

            lock (_lock)
            {
                _buffer.Add(logLine);
                if (_buffer.Count >= _flushThreshold)
                    FlushInternal();
            }
        }

        [Conditional("DEBUG")]
        public void Info(string message) => Log("INFO", message);

        [Conditional("DEBUG")]
        public void Warn(string message) => Log("WARN", message);

        [Conditional("DEBUG")]
        public void Error(string message, Exception ex) => Log("ERROR", message, ex);

        [Conditional("DEBUG")]
        public void Flush()
        {
            lock (_lock)
                FlushInternal();
        }

        private void FlushInternal()
        {
            if (_buffer.Count == 0) return;
            File.AppendAllLines(_filePath, _buffer);
            _buffer.Clear();
        }

        private void FlushTimerCallback(object state) => Flush();

        public void Dispose()
        {
            if (_disposed) return;
            _flushTimer?.Dispose();
            Flush(); // Сбросить остатки перед выходом
            _disposed = true;
        }
    }
}
