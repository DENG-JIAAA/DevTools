
using System;

namespace DevTools.Models
{
    /// <summary>
    /// Java 服务运行时信息
    /// </summary>
    public class ServiceRuntimeInfo
    {
        /// <summary>
        /// 服务唯一标识
        /// </summary>
        public string ServiceId { get; set; } = string.Empty;

        /// <summary>
        /// 进程 ID
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// 运行状态：
        /// Stopped / Starting / WaitingReady / Running / Failed
        /// </summary>
        public string Status { get; set; } = "Stopped";

        /// <summary>
        /// 启动时间
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// 日志文件路径
        /// </summary>
        public string LogFilePath { get; set; } = string.Empty;

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// 进程退出码
        /// </summary>
        public int? ExitCode { get; set; }
    }
}
