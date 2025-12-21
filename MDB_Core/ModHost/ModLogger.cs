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
        
        // Section separator for console output
        private const string SECTION_LINE = "────────────────────────────────────────";

        /// <summary>
        /// Create a new logger for a mod.
        /// </summary>
        /// <param name="modName">Name of the mod (used as prefix)</param>
        public ModLogger(string modName)
        {
            _modName = modName;
        }
        
        /// <summary>
        /// Write a section separator to the console.
        /// </summary>
        public static void Section(string title, ConsoleColor color = ConsoleColor.DarkGray)
        {
            lock (_logLock)
            {
                var prevColor = System.Console.ForegroundColor;
                System.Console.ForegroundColor = color;
                System.Console.WriteLine();
                System.Console.WriteLine($"── {title} ──");
                System.Console.ForegroundColor = prevColor;
                
                _logWriter?.WriteLine();
                _logWriter?.WriteLine($"── {title} ──");
            }
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
                    _logWriter.WriteLine($"=== MDB Framework Session {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                    _logWriter.WriteLine();
                }
            }
        }

        /// <summary>
        /// Write a log message.
        /// </summary>
        private void Write(string level, string message, ConsoleColor color = ConsoleColor.White)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string fileFormatted = $"[{timestamp}] [{level}] [{_modName}] {message}";
            string consoleFormatted = $"[{timestamp}] [{_modName}] {message}";

            lock (_logLock)
            {
                // Write to log file (full format)
                _logWriter?.WriteLine(fileFormatted);

                // Write to console with color
                var prevColor = System.Console.ForegroundColor;
                System.Console.ForegroundColor = color;
                System.Console.WriteLine(consoleFormatted);
                System.Console.ForegroundColor = prevColor;
            }
        }

        /// <summary>
        /// Log an informational message.
        /// </summary>
        public void Info(string message)
        {
            Write("INFO", message, ConsoleColor.Blue);
        }
        
        /// <summary>
        /// Log an informational message with custom color.
        /// </summary>
        public void Info(string message, ConsoleColor color)
        {
            Write("INFO", message, color);
        }

        /// <summary>
        /// Log a warning message.
        /// </summary>
        public void Warning(string message)
        {
            Write("WARN", message, ConsoleColor.Yellow);
        }

        /// <summary>
        /// Log an error message.
        /// </summary>
        public void Error(string message)
        {
            Write("ERROR", message, ConsoleColor.Red);
        }

        /// <summary>
        /// Log an error with exception details.
        /// </summary>
        public void Error(string message, Exception ex)
        {
            Write("ERROR", $"{message}: {ex.Message}", ConsoleColor.Red);
        }

        /// <summary>
        /// Log a debug message.
        /// Only shown when debug logging is enabled.
        /// </summary>
        public void Debug(string message)
        {
#if DEBUG
            Write("DEBUG", message, ConsoleColor.DarkGray);
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
        /// Static log method for use by helpers and static classes that don't have a logger instance.
        /// Auto-detects [INFO], [ERROR], [WARN], [DEBUG] prefixes and applies appropriate colors.
        /// </summary>
        public static void LogInternal(string source, string message)
        {
            // Auto-detect level prefix and apply appropriate color
            ConsoleColor color = ConsoleColor.Blue;
            string cleanMessage = message;
            
            if (message.StartsWith("[ERROR]"))
            {
                color = ConsoleColor.Red;
                cleanMessage = message.Substring(7).TrimStart();
            }
            else if (message.StartsWith("[WARN]"))
            {
                color = ConsoleColor.Yellow;
                cleanMessage = message.Substring(6).TrimStart();
            }
            else if (message.StartsWith("[INFO]"))
            {
                color = ConsoleColor.Blue;
                cleanMessage = message.Substring(6).TrimStart();
            }
            else if (message.StartsWith("[DEBUG]") || message.StartsWith("[TRACE]"))
            {
                color = ConsoleColor.DarkGray;
                cleanMessage = message.Substring(7).TrimStart();
            }
            
            LogInternal(source, cleanMessage, color);
        }
        
        /// <summary>
        /// Static log method with explicit color support.
        /// </summary>
        public static void LogInternal(string source, string message, ConsoleColor color)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string fileFormatted = $"[{timestamp}] [INFO] [{source}] {message}";
            string consoleFormatted = $"[{timestamp}] [{source}] {message}";

            lock (_logLock)
            {
                _logWriter?.WriteLine(fileFormatted);
                var prevColor = System.Console.ForegroundColor;
                System.Console.ForegroundColor = color;
                System.Console.WriteLine(consoleFormatted);
                System.Console.ForegroundColor = prevColor;
            }
        }
    }
}
