// ==============================
// ModLogger - Logging Utilities for Mods
// ==============================
// Provides logging functionality for mods

using System;
using System.IO;

namespace GameSDK.ModHost
{
    /// <summary>
    /// Provides logging functionality for mods.
    /// Each mod gets its own logger instance with the mod name as prefix.
    /// </summary>
    public class ModLogger
    {
        private readonly string _modName;
        private static readonly object _logLock = new object();
        private static StreamWriter _logWriter;
        private static string _logPath;

        /// <summary>
        /// Create a new logger for a mod.
        /// </summary>
        /// <param name="modName">Name of the mod (used as prefix)</param>
        public ModLogger(string modName)
        {
            _modName = modName;
        }

        /// <summary>
        /// Initialize the logging system.
        /// Called by ModManager during startup.
        /// </summary>
        internal static void Initialize(string logDirectory)
        {
            try
            {
                Directory.CreateDirectory(logDirectory);
                _logPath = Path.Combine(logDirectory, "Mods.log");
                
                // Create or append to log file
                _logWriter = new StreamWriter(_logPath, append: true);
                _logWriter.AutoFlush = true;
                
                WriteHeader();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[MDB] Failed to initialize logging: {ex.Message}");
            }
        }

        /// <summary>
        /// Shutdown the logging system.
        /// </summary>
        internal static void Shutdown()
        {
            lock (_logLock)
            {
                _logWriter?.Close();
                _logWriter = null;
            }
        }

        private static void WriteHeader()
        {
            lock (_logLock)
            {
                if (_logWriter != null)
                {
                    _logWriter.WriteLine();
                    _logWriter.WriteLine($"========================================");
                    _logWriter.WriteLine($"MDB Framework - Mod Log");
                    _logWriter.WriteLine($"Session started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    _logWriter.WriteLine($"========================================");
                    _logWriter.WriteLine();
                }
            }
        }

        /// <summary>
        /// Write a log message.
        /// </summary>
        private void Write(string level, string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string formatted = $"[{timestamp}] [{level}] [{_modName}] {message}";

            lock (_logLock)
            {
                // Write to log file
                _logWriter?.WriteLine(formatted);

                // Also write to console/debug output
                System.Console.WriteLine(formatted);
                System.Diagnostics.Debug.WriteLine(formatted);
            }
        }

        /// <summary>
        /// Log an informational message.
        /// </summary>
        public void Info(string message)
        {
            Write("INFO", message);
        }

        /// <summary>
        /// Log a warning message.
        /// </summary>
        public void Warning(string message)
        {
            Write("WARN", message);
        }

        /// <summary>
        /// Log an error message.
        /// </summary>
        public void Error(string message)
        {
            Write("ERROR", message);
        }

        /// <summary>
        /// Log an error with exception details.
        /// </summary>
        public void Error(string message, Exception ex)
        {
            Write("ERROR", $"{message}: {ex.Message}");
            Write("ERROR", $"Stack trace: {ex.StackTrace}");
        }

        /// <summary>
        /// Log a debug message.
        /// Only shown when debug logging is enabled.
        /// </summary>
        public void Debug(string message)
        {
#if DEBUG
            Write("DEBUG", message);
#endif
        }

        /// <summary>
        /// Log a formatted message.
        /// </summary>
        public void Info(string format, params object[] args)
        {
            Info(string.Format(format, args));
        }

        /// <summary>
        /// Log a formatted warning.
        /// </summary>
        public void Warning(string format, params object[] args)
        {
            Warning(string.Format(format, args));
        }

        /// <summary>
        /// Log a formatted error.
        /// </summary>
        public void Error(string format, params object[] args)
        {
            Error(string.Format(format, args));
        }
        
        /// <summary>
        /// Static log method for internal framework use.
        /// </summary>
        internal static void LogInternal(string source, string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string formatted = $"[{timestamp}] [INFO] [{source}] {message}";

            lock (_logLock)
            {
                _logWriter?.WriteLine(formatted);
                System.Console.WriteLine(formatted);
            }
        }
    }
}
