using System;
using System.IO;

namespace TreasuryFixTool.Infrastructure.Logging
{
    /// <summary>
    /// A simple file logger that writes log messages to a specified file.
    /// </>
    public class FileLogger
    {
        private readonly string _logFilePath;
        private static readonly object _lock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="FileLogger"/> class.
        /// </summary>
        /// <param name="logFilePath">The full path to the log file.</param>
        public FileLogger(string logFilePath)
        {
            if (string.IsNullOrWhiteSpace(logFilePath))
                throw new ArgumentException("Log file path cannot be null or empty.", nameof(logFilePath));

            _logFilePath = logFilePath;

            // Ensure the directory exists
            string directory = Path.GetDirectoryName(_logFilePath)!;
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void Info(string message)
        {
            WriteLog("INFO", message);
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void Warning(string message)
        {
            WriteLog("WARNING", message);
        }

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void Error(string message)
        {
            WriteLog("ERROR", message);
        }

        /// <summary>
        /// Logs an error message with an exception.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="exception">The exception to log.</param>
        public void Error(string message, Exception exception)
        {
            WriteLog("ERROR", $"{message} - Exception: {exception}");
        }

        private void WriteLog(string level, string message)
        {
            string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";

            lock (_lock)
            {
                try
                {
                    File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
                }
                catch
                {
                    // If we can't write to the log file, we might want to fallback or ignore.
                    // For simplicity, we'll just ignore logging errors to avoid infinite loops.
                }
            }
        }
    }
}