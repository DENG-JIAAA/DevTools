
using DevTools.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevTools.Services
{
    /// <summary>
    /// Java 服务进程管理器
    /// 负责单个 Java 服务的启动、停止和运行状态跟踪
    /// </summary>
    public static class JavaProcessManager
    {
        private static readonly ConcurrentDictionary<string, Process> RunningProcesses = new();

        private static readonly ConcurrentDictionary<string, ServiceRuntimeInfo> RuntimeInfos = new();

        private static readonly ConcurrentDictionary<string, string> LogFilePaths = new();

        private static readonly ConcurrentDictionary<string, bool> StoppingServices = new();

        private static readonly object StateLock = new();

        private static readonly object LogLock = new();

        /// <summary>
        /// 启动 Java 服务
        /// </summary>
        public static Task<int> StartAsync(ServiceInfo service)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            if (string.IsNullOrWhiteSpace(service.Id))
            {
                throw new InvalidOperationException("服务 ID 不能为空。");
            }

            lock (StateLock)
            {
                if (RunningProcesses.TryGetValue(service.Id, out Process? existingProcess))
                {
                    try
                    {
                        if (!existingProcess.HasExited)
                        {
                            throw new InvalidOperationException(
                                $"服务「{service.Name}」已经由 DevTools 启动，PID：{existingProcess.Id}。");
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        if (ex.Message.Contains("已经由 DevTools 启动"))
                        {
                            throw;
                        }

                        RunningProcesses.TryRemove(service.Id, out _);
                        existingProcess.Dispose();
                    }
                }

                StoppingServices.TryRemove(service.Id, out _);
            }

            try
            {
                ProcessStartInfo startInfo = BuildStartInfo(service);

                ValidateStartInfo(service, startInfo);

                string logDirectory = Path.Combine(
                    AppLogger.LogDirectoryPath,
                    "Services");

                Directory.CreateDirectory(logDirectory);

                string logFilePath = Path.Combine(
                    logDirectory,
                    $"{SanitizeFileName(service.Name)}-{DateTime.Now:yyyyMMdd-HHmmss-fff}.log");

                LogFilePaths[service.Id] = logFilePath;

                var process = new Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true
                };

                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                    {
                        WriteLog(logFilePath, e.Data);
                    }
                };

                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                    {
                        WriteLog(logFilePath, "[ERROR] " + e.Data);
                    }
                };

                process.Exited += (_, _) =>
                {
                    HandleProcessExited(service, process, logFilePath);
                };

                lock (StateLock)
                {
                    RuntimeInfos[service.Id] = new ServiceRuntimeInfo
                    {
                        ServiceId = service.Id,
                        Status = "Starting",
                        StartTime = DateTime.Now,
                        LogFilePath = logFilePath
                    };

                    if (!process.Start())
                    {
                        process.Dispose();
                        throw new InvalidOperationException("Java 服务进程启动失败。");
                    }

                    RunningProcesses[service.Id] = process;

                    RuntimeInfos[service.Id].ProcessId = process.Id;
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                WriteLog(
                    logFilePath,
                    $"[DevTools] 服务启动成功，名称：{service.Name}，PID：{process.Id}");

                AppLogger.Info(
                    $"Java 服务启动：{service.Name}，PID：{process.Id}");

                return Task.FromResult(process.Id);
            }
            catch (Exception ex)
            {
                UpdateRuntimeStatus(
                    service.Id,
                    "Failed",
                    ex.Message,
                    null);

                AppLogger.Error(
                    $"Java 服务启动失败：{service.Name}，原因：{ex.Message}");

                throw;
            }
        }

        /// <summary>
        /// 停止指定服务
        /// 只停止 DevTools 自己启动并记录的进程
        /// </summary>
        public static async Task<bool> StopAsync(string serviceId)
        {
            if (string.IsNullOrWhiteSpace(serviceId))
            {
                return false;
            }

            Process? process;

            lock (StateLock)
            {
                if (!RunningProcesses.TryGetValue(serviceId, out process))
                {
                    return false;
                }

                StoppingServices[serviceId] = true;
            }

            int processId = 0;

            try
            {
                if (process.HasExited)
                {
                    return true;
                }

                processId = process.Id;

                // 结束 DevTools 启动的进程及其子进程
                process.Kill(true);

                await process.WaitForExitAsync();

                AppLogger.Info(
                    $"Java 服务停止：ServiceId={serviceId}，PID={processId}");

                return true;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
            finally
            {
                lock (StateLock)
                {
                    if (RunningProcesses.TryGetValue(serviceId, out Process? currentProcess)
                        && ReferenceEquals(currentProcess, process))
                    {
                        RunningProcesses.TryRemove(serviceId, out _);
                    }

                    StoppingServices.TryRemove(serviceId, out _);
                }

                UpdateRuntimeStatus(
                    serviceId,
                    "Stopped",
                    string.Empty,
                    null);

                process.Dispose();
            }
        }

        /// <summary>
        /// 获取服务运行时信息
        /// </summary>
        public static ServiceRuntimeInfo GetRuntimeInfo(string serviceId)
        {
            if (string.IsNullOrWhiteSpace(serviceId))
            {
                return new ServiceRuntimeInfo();
            }

            if (!RuntimeInfos.TryGetValue(serviceId, out ServiceRuntimeInfo? runtime))
            {
                return new ServiceRuntimeInfo
                {
                    ServiceId = serviceId,
                    Status = "Stopped"
                };
            }

            if (RunningProcesses.TryGetValue(serviceId, out Process? process))
            {
                try
                {
                    if (process.HasExited)
                    {
                        int exitCode = GetExitCode(process);

                        HandleProcessExited(
                            serviceId,
                            process,
                            GetLogFilePath(serviceId),
                            exitCode);
                    }
                }
                catch
                {
                    // 进程状态读取失败时保留已有运行状态
                }
            }

            return CopyRuntimeInfo(
                RuntimeInfos.TryGetValue(serviceId, out ServiceRuntimeInfo? latest)
                    ? latest
                    : runtime);
        }

        /// <summary>
        /// 获取服务日志文件路径
        /// </summary>
        public static string GetLogFilePath(string serviceId)
        {
            return LogFilePaths.TryGetValue(serviceId, out string? path)
                ? path
                : string.Empty;
        }

        /// <summary>
        /// 处理进程退出事件
        /// </summary>
        private static void HandleProcessExited(
            ServiceInfo service,
            Process process,
            string logFilePath)
        {
            HandleProcessExited(
                service.Id,
                process,
                logFilePath,
                GetExitCode(process));
        }

        private static void HandleProcessExited(
            string serviceId,
            Process process,
            string logFilePath,
            int exitCode)
        {
            bool isStopping;

            lock (StateLock)
            {
                if (RunningProcesses.TryGetValue(serviceId, out Process? currentProcess)
                    && ReferenceEquals(currentProcess, process))
                {
                    RunningProcesses.TryRemove(serviceId, out _);
                }

                isStopping = StoppingServices.ContainsKey(serviceId);
            }

            string status;
            string errorMessage;

            if (isStopping)
            {
                status = "Stopped";
                errorMessage = string.Empty;
            }
            else if (exitCode == 0)
            {
                status = "Stopped";
                errorMessage = string.Empty;
            }
            else
            {
                status = "Failed";
                errorMessage = $"服务进程异常退出，退出码：{exitCode}";
            }

            UpdateRuntimeStatus(
                serviceId,
                status,
                errorMessage,
                exitCode);

            WriteLog(
                logFilePath,
                $"[DevTools] 进程已退出，退出时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}，退出码：{exitCode}，状态：{status}");

            if (status == "Failed")
            {
                AppLogger.Error(
                    $"Java 服务异常退出：ServiceId={serviceId}，退出码：{exitCode}");
            }

            // 异步释放进程资源，避免阻塞退出事件
            _ = Task.Run(() =>
            {
                try
                {
                    process.Dispose();
                }
                catch
                {
                    // 忽略资源释放异常
                }
            });
        }

        /// <summary>
        /// 更新运行状态
        /// </summary>
        private static void UpdateRuntimeStatus(
            string serviceId,
            string status,
            string errorMessage,
            int? exitCode)
        {
            RuntimeInfos.AddOrUpdate(
                serviceId,
                _ => new ServiceRuntimeInfo
                {
                    ServiceId = serviceId,
                    Status = status,
                    ErrorMessage = errorMessage,
                    ExitCode = exitCode,
                    LogFilePath = GetLogFilePath(serviceId)
                },
                (_, old) => new ServiceRuntimeInfo
                {
                    ServiceId = old.ServiceId,
                    ProcessId = old.ProcessId,
                    Status = status,
                    StartTime = old.StartTime,
                    LogFilePath = old.LogFilePath,
                    ErrorMessage = errorMessage,
                    ExitCode = exitCode
                });
        }

        /// <summary>
        /// 复制运行状态，避免外部修改内部对象
        /// </summary>
        private static ServiceRuntimeInfo CopyRuntimeInfo(ServiceRuntimeInfo runtime)
        {
            return new ServiceRuntimeInfo
            {
                ServiceId = runtime.ServiceId,
                ProcessId = runtime.ProcessId,
                Status = runtime.Status,
                StartTime = runtime.StartTime,
                LogFilePath = runtime.LogFilePath,
                ErrorMessage = runtime.ErrorMessage,
                ExitCode = runtime.ExitCode
            };
        }

        /// <summary>
        /// 构建启动参数
        /// </summary>
        private static ProcessStartInfo BuildStartInfo(ServiceInfo service)
        {
            var startInfo = new ProcessStartInfo
            {
                WorkingDirectory = service.ProjectPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            if (service.StartMode.Equals(
                "Jar",
                StringComparison.OrdinalIgnoreCase))
            {
                string javaPath = ResolveJavaPath(service);

                startInfo.FileName = javaPath;

                AddArguments(startInfo, service.JvmArguments);

                startInfo.ArgumentList.Add("-jar");
                startInfo.ArgumentList.Add(service.JarPath);

                if (!string.IsNullOrWhiteSpace(service.Profile))
                {
                    startInfo.ArgumentList.Add(
                        $"--spring.profiles.active={service.Profile}");
                }

                AddArguments(startInfo, service.ApplicationArguments);
            }
            else if (service.StartMode.Equals(
                "Maven",
                StringComparison.OrdinalIgnoreCase))
            {
                string mavenPath = ResolveMavenPath(service);

                startInfo.FileName = "cmd.exe";

                startInfo.ArgumentList.Add("/c");
                startInfo.ArgumentList.Add(mavenPath);

                startInfo.ArgumentList.Add("spring-boot:run");

                if (!string.IsNullOrWhiteSpace(service.ModuleName))
                {
                    startInfo.ArgumentList.Add("-pl");
                    startInfo.ArgumentList.Add(service.ModuleName);
                    startInfo.ArgumentList.Add("-am");
                }

                if (!string.IsNullOrWhiteSpace(service.MainClass))
                {
                    startInfo.ArgumentList.Add(
                        $"-Dspring-boot.run.main-class={service.MainClass}");
                }

                if (!string.IsNullOrWhiteSpace(service.JvmArguments))
                {
                    startInfo.ArgumentList.Add(
                        $"-Dspring-boot.run.jvmArguments={service.JvmArguments}");
                }

                if (!string.IsNullOrWhiteSpace(service.Profile))
                {
                    startInfo.ArgumentList.Add(
                        $"-Dspring-boot.run.profiles={service.Profile}");
                }

                AddArguments(startInfo, service.ApplicationArguments);
            }
            else
            {
                throw new InvalidOperationException(
                    $"不支持的启动方式：{service.StartMode}");
            }

            ApplyEnvironmentVariables(startInfo, service.EnvironmentVariables);

            return startInfo;
        }

        /// <summary>
        /// 检查启动配置
        /// </summary>
        private static void ValidateStartInfo(
            ServiceInfo service,
            ProcessStartInfo startInfo)
        {
            if (string.IsNullOrWhiteSpace(service.ProjectPath) ||
                !Directory.Exists(service.ProjectPath))
            {
                throw new DirectoryNotFoundException(
                    $"项目目录不存在：{service.ProjectPath}");
            }

            if (service.StartMode.Equals(
                "Jar",
                StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(service.JarPath) ||
                    !File.Exists(service.JarPath))
                {
                    throw new FileNotFoundException(
                        "JAR 文件不存在。",
                        service.JarPath);
                }

                ValidateExecutable(
                    startInfo.FileName,
                    "Java 可执行文件不存在。");
            }
            else
            {
                string mavenPath = startInfo.ArgumentList[1];

                ValidateExecutable(
                    mavenPath,
                    "Maven 可执行文件不存在。");
            }
        }

        /// <summary>
        /// 校验可执行文件
        /// 支持绝对路径、相对路径以及 PATH 环境变量中的命令
        /// </summary>
        private static void ValidateExecutable(string path, string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new FileNotFoundException(errorMessage);
            }

            // 没有目录部分，视为通过 PATH 查找的命令
            if (!Path.IsPathRooted(path) &&
                string.IsNullOrWhiteSpace(Path.GetDirectoryName(path)))
            {
                return;
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(errorMessage, path);
            }
        }

        /// <summary>
        /// 解析 Java 可执行文件
        /// </summary>
        private static string ResolveJavaPath(ServiceInfo service)
        {
            if (!string.IsNullOrWhiteSpace(service.JavaExecutablePath))
            {
                return service.JavaExecutablePath.Trim().Trim('"');
            }

            string javaHome = Environment.GetEnvironmentVariable("JAVA_HOME")
                              ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(javaHome))
            {
                string javaPath = Path.Combine(
                    javaHome,
                    "bin",
                    "java.exe");

                if (File.Exists(javaPath))
                {
                    return javaPath;
                }
            }

            return "java.exe";
        }

        /// <summary>
        /// 解析 Maven 可执行文件
        /// </summary>
        private static string ResolveMavenPath(ServiceInfo service)
        {
            if (!string.IsNullOrWhiteSpace(service.MavenExecutablePath))
            {
                return service.MavenExecutablePath.Trim().Trim('"');
            }

            string wrapperPath = Path.Combine(
                service.ProjectPath,
                "mvnw.cmd");

            if (File.Exists(wrapperPath))
            {
                return wrapperPath;
            }

            string mavenHome = Environment.GetEnvironmentVariable("MAVEN_HOME")
                               ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(mavenHome))
            {
                string mavenPath = Path.Combine(
                    mavenHome,
                    "bin",
                    "mvn.cmd");

                if (File.Exists(mavenPath))
                {
                    return mavenPath;
                }
            }

            return "mvn.cmd";
        }

        /// <summary>
        /// 添加命令行参数
        /// </summary>
        private static void AddArguments(
            ProcessStartInfo startInfo,
            string arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments))
            {
                return;
            }

            foreach (string argument in SplitArguments(arguments))
            {
                startInfo.ArgumentList.Add(argument);
            }
        }

        /// <summary>
        /// 简单解析带引号的命令行参数
        /// </summary>
        private static string[] SplitArguments(string arguments)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            foreach (char c in arguments)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }

                    continue;
                }

                current.Append(c);
            }

            if (current.Length > 0)
            {
                result.Add(current.ToString());
            }

            return result.ToArray();
        }

        /// <summary>
        /// 设置环境变量
        /// </summary>
        private static void ApplyEnvironmentVariables(
            ProcessStartInfo startInfo,
            string variables)
        {
            if (string.IsNullOrWhiteSpace(variables))
            {
                return;
            }

            foreach (string line in variables.Split(
                new[] { '\r', '\n', ';' },
                StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = line.IndexOf('=');

                if (separator <= 0)
                {
                    continue;
                }

                string key = line[..separator].Trim();
                string value = line[(separator + 1)..].Trim();

                if (!string.IsNullOrWhiteSpace(key))
                {
                    startInfo.Environment[key] = value;
                }
            }
        }

        /// <summary>
        /// 写入服务日志
        /// </summary>
        private static void WriteLog(string path, string message)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                lock (LogLock)
                {
                    File.AppendAllText(
                        path,
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}",
                        Encoding.UTF8);
                }
            }
            catch
            {
                // 日志写入失败不应影响服务运行
            }
        }

        /// <summary>
        /// 获取进程退出码
        /// </summary>
        private static int GetExitCode(Process process)
        {
            try
            {
                return process.ExitCode;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// 清理文件名中的非法字符
        /// </summary>
        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            return string.IsNullOrWhiteSpace(name)
                ? "JavaService"
                : name;
        }
    }
}
