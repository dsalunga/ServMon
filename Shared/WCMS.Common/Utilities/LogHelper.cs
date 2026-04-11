using System;
using System.IO;

namespace WCMS.Common.Utilities
{
    public static class LogHelper
    {
        private static readonly object Sync = new object();
        private static readonly string LogDirectory = Path.Combine(AppContext.BaseDirectory, "logs");

        public static string CurrentLogFile => Path.Combine(LogDirectory, $"servmon-{DateTime.UtcNow:yyyyMMdd}.log");

        static LogHelper()
        {
            CreateLogDirectory();
        }

        public static void CreateLogDirectory()
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
            }
            catch
            {
                // Intentionally swallow. Logging should never crash caller.
            }
        }

        public static void WriteLog(bool writeConsole, bool shout, string format, params object[] arg)
        {
            var message = SafeFormat(format, arg);
            Write(writeConsole, message);
        }

        public static void WriteLog(string filePath, string format, params object[] args)
        {
            var message = SafeFormat(format, args);
            WriteToFile(string.IsNullOrWhiteSpace(filePath) ? CurrentLogFile : filePath, message);
        }

        public static void WriteLog(bool writeConsole, string error, params object[] arg)
        {
            var message = SafeFormat(error, arg);
            Write(writeConsole, message);
        }

        public static void WriteLog(string format, params object[] arg)
        {
            var message = SafeFormat(format, arg);
            Write(false, message);
        }

        public static void WriteLog(bool writeConsole, Exception ex)
        {
            var message = ex == null ? "(null exception)" : ex.ToString();
            Write(writeConsole, message);
        }

        public static void WriteLog(Exception ex)
        {
            WriteLog(false, ex);
        }

        private static void Write(bool writeConsole, string message)
        {
            var entry = $"[{DateTime.UtcNow:O}] {message}";

            if (writeConsole)
            {
                Console.WriteLine(entry);
            }

            WriteToFile(CurrentLogFile, entry);
        }

        private static void WriteToFile(string filePath, string message)
        {
            try
            {
                lock (Sync)
                {
                    var directory = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.AppendAllText(filePath, message + Environment.NewLine);
                }
            }
            catch
            {
                // Best effort only.
            }
        }

        private static string SafeFormat(string format, object[] args)
        {
            if (string.IsNullOrEmpty(format))
            {
                return string.Empty;
            }

            try
            {
                return args == null || args.Length == 0 ? format : string.Format(format, args);
            }
            catch
            {
                return format;
            }
        }
    }
}
