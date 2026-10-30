
using System;
using System.IO;

namespace DevTools.Services
{
    public static class AppPaths
    {
        /// <summary>
        /// 软件运行目录
        /// </summary>
        public static readonly string BaseDirectory =
            AppContext.BaseDirectory;

        /// <summary>
        /// 配置目录
        /// </summary>
        public static readonly string ConfigDirectory =
            Path.Combine(BaseDirectory, "Config");

        /// <summary>
        /// 日志目录
        /// </summary>
        public static readonly string LogDirectory =
            Path.Combine(BaseDirectory, "Logs");

        /// <summary>
        /// 确保必要目录存在
        /// </summary>
        public static void EnsureDirectories()
        {
            Directory.CreateDirectory(ConfigDirectory);
            Directory.CreateDirectory(LogDirectory);
        }
    }
}
