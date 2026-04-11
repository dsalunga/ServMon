using System;
using System.IO;
using System.Text;

namespace WCMS.Common.Utilities
{
    public static class FileHelper
    {
        public static bool WriteFile(string content, string fileName)
        {
            return WriteFile(content, fileName, Encoding.UTF8);
        }

        public static bool WriteFile(string content, string fileName, Encoding encoding)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            try
            {
                var targetPath = EvalPath(fileName, true);
                var folder = GetFolder(targetPath);
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                File.WriteAllText(targetPath, content ?? string.Empty, encoding ?? Encoding.UTF8);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string ReadFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                var evalPath = EvalPath(path, false);
                if (!File.Exists(evalPath))
                {
                    return string.Empty;
                }

                return File.ReadAllText(evalPath);
            }
            catch
            {
                return string.Empty;
            }
        }

        public static bool IsAbsolutePath(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path);
        }

        public static string EvalPath(string path, bool createDirectory = false)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            var expandedPath = Environment.ExpandEnvironmentVariables(path);
            var fullPath = IsAbsolutePath(expandedPath)
                ? Path.GetFullPath(expandedPath)
                : Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, expandedPath));

            if (createDirectory)
            {
                var directory = IsFolder(fullPath) ? fullPath : Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }

            return fullPath;
        }

        public static bool IsFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return true;
            }

            if (Directory.Exists(path))
            {
                return true;
            }

            return string.IsNullOrEmpty(Path.GetExtension(path));
        }

        public static string GetFolder(string folder, char separator = '\\')
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                return string.Empty;
            }

            var normalized = folder.Replace('\\', '/').Replace('/', '/');
            if (normalized.EndsWith("/", StringComparison.Ordinal))
            {
                return normalized.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar);
            }

            var index = normalized.LastIndexOf('/');
            if (index < 0)
            {
                return string.Empty;
            }

            return normalized.Substring(0, index).Replace('/', Path.DirectorySeparatorChar);
        }
    }
}
