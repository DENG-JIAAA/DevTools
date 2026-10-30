
using System;

namespace DevTools.Models
{
    /// <summary>
    /// Java 服务基础配置
    /// </summary>
    public class ServiceInfo
    {
        /// <summary>
        /// 服务唯一标识
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 服务编号，例如 101、201、901
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// 服务名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 项目根目录
        /// </summary>
        public string ProjectPath { get; set; } = string.Empty;

        /// <summary>
        /// 启动方式：Maven / Jar
        /// </summary>
        public string StartMode { get; set; } = "Maven";

        /// <summary>
        /// Maven 模块名称，多模块项目使用
        /// </summary>
        public string ModuleName { get; set; } = string.Empty;

        /// <summary>
        /// Jar 文件路径
        /// </summary>
        public string JarPath { get; set; } = string.Empty;

        /// <summary>
        /// Spring Boot 启动类
        /// </summary>
        public string MainClass { get; set; } = string.Empty;

        /// <summary>
        /// 服务端口，0 表示未配置
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// JVM 参数
        /// </summary>
        public string JvmArguments { get; set; } = string.Empty;

        /// <summary>
        /// Spring Boot 应用参数
        /// </summary>
        public string ApplicationArguments { get; set; } = string.Empty;

        /// <summary>
        /// Spring Profile
        /// </summary>
        public string Profile { get; set; } = string.Empty;

        /// <summary>
        /// 环境变量，后续支持 KEY=VALUE 格式
        /// </summary>
        public string EnvironmentVariables { get; set; } = string.Empty;

        /// <summary>
        /// 就绪检测方式：None / Port / Http
        /// </summary>
        public string ReadinessCheckType { get; set; } = "Port";

        /// <summary>
        /// HTTP 健康检查地址
        /// </summary>
        public string HealthCheckUrl { get; set; } = string.Empty;

        /// <summary>
        /// 启动超时时间（秒）
        /// </summary>
        public int StartupTimeoutSeconds { get; set; } = 120;

        /// <summary>
        /// Java 可执行文件路径，留空时使用系统环境变量
        /// </summary>
        public string JavaExecutablePath { get; set; } = string.Empty;

        /// <summary>
        /// Maven 可执行文件路径，留空时使用项目 mvnw 或系统 Maven
        /// </summary>
        public string MavenExecutablePath { get; set; } = string.Empty;

        /// <summary>
        /// 运行状态（仅用于界面展示，不参与配置持久化）
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string RuntimeStatus { get; set; } = "Stopped";

        /// <summary>
        /// 当前运行进程 ID
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public int RuntimeProcessId { get; set; }

        /// <summary>
        /// 实际启动时间
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string RuntimeStartTime { get; set; } = "-";
    }
}
