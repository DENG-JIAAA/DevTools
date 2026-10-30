using System;
using System.IO;
using System.Text;

namespace DevTools.Services
{
    public static class AppLogger
    {
        private static readonly object LockObject = new();

        private static readonly string LogDirectory =
    AppPaths.LogDirectory;

        public static string LogFilePath => Path.Combine(
            LogDirectory,
            $"DevTools-{DateTime.Now:yyyy-MM-dd}.log");

        public static string LogDirectoryPath => LogDirectory;


        public static int CleanOldLogs(int retentionDays)
        {
            if (retentionDays <= 0)
            {
                return 0;
            }

            int deletedCount = 0;

            try
            {
                lock (LockObject)
                {
                    if (!Directory.Exists(LogDirectory))
                    {
                        return 0;
                    }

                    DateTime cutoffDate = DateTime.Now.AddDays(-retentionDays);

                    foreach (string file in Directory.GetFiles(
                        LogDirectory,
                        "*.log"))
                    {
                        if (File.GetLastWriteTime(file) < cutoffDate)
                        {
                            File.Delete(file);
                            deletedCount++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Error("清理过期日志失败。", ex);
            }

            return deletedCount;
        }


        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Error(string message, Exception? exception = null)
        {
            string content = exception == null
                ? message
                : $"{message}{Environment.NewLine}{exception}";

            Write("ERROR", content);
        }

        private static void Write(string level, string message)
        {
            try
            {
                lock (LockObject)
                {
                    Directory.CreateDirectory(LogDirectory);

                    string logContent =
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] " +
                        $"[{level}] {message}{Environment.NewLine}";

                    File.AppendAllText(
                        LogFilePath,
                        logContent,
                        new UTF8Encoding(false));
                }
            }
            catch
            {
                // 日志写入失败不能影响软件正常运行。
            }
        }
    }
}
