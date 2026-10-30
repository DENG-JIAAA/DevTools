
using DevTools.Models;
using DevTools.Services;
using System;
using System.Threading;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace DevTools
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<PortInfo> _visiblePorts = new();

        private List<ProcessInfo> _allProcesses = new();
        private List<PortInfo> _allPorts = new();

        private readonly ObservableCollection<ProcessInfo> _visibleProcesses = new();

        private readonly List<string> _allLogLines = new();

        private AppSettings _settings = new();

        private readonly ObservableCollection<ServiceInfo> _javaServices = new();

        private readonly ObservableCollection<LaunchPlan> _launchPlans = new();

        private const int PageSize = 50;

        private int _processCurrentPage = 1;
        private int _portCurrentPage = 1;

        // 防止旧的异步加载结果覆盖新结果
        private int _portLoadVersion;
        private int _processLoadVersion;
        private int _dashboardLoadVersion;

        // 用于取消上一次尚未完成的读取任务
        private CancellationTokenSource? _portLoadCts;
        private CancellationTokenSource? _processLoadCts;
        private CancellationTokenSource? _dashboardLoadCts;


        public MainWindow()
        {
            InitializeComponent();

            // 设置页面：动态显示程序目录
            LogDirectoryText.Text = AppPaths.LogDirectory;

            ConfigFilePathText.Text = Path.Combine(
                AppPaths.ConfigDirectory,
                "settings.json");

            // 关于页面：动态显示程序版本
            Version? version = System.Reflection.Assembly
                .GetExecutingAssembly()
                .GetName()
                .Version;

            VersionText.Text = $"版本 {version?.Major}.{version?.Minor}.{version?.Build}";

            // 显示当前程序运行平台
            PlatformText.Text = Environment.Is64BitProcess
                ? "Windows x64"
                : "Windows x86";

            JavaServiceDataGrid.ItemsSource = _javaServices;
            LaunchPlanDataGrid.ItemsSource = _launchPlans;

            _settings = SettingsManager.Load();
            ApplySettingsToUI();

            AppLogger.Info("DevTools 启动。");

            if (_settings.AutoCleanLogs)
            {
                int deletedCount =
                    AppLogger.CleanOldLogs(_settings.LogRetentionDays);

                AppLogger.Info($"启动时日志清理完成，删除 {deletedCount} 个过期日志文件。");
            }

            _ = LoadDashboardAsync();
        }



        private async void DashboardNav_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(DashboardPage, "工作台", DashboardNav);
            await LoadDashboardAsync();
        }


        private async void PortNav_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(PortPage, "端口管理", PortNav);
            await LoadPortsAsync();
        }

        private async void ProcessNav_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(ProcessPage, "进程管理", ProcessNav);
            await LoadProcessesAsync();
        }

        private void ScriptNav_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(ScriptPage, "脚本编码工具", ScriptNav);
        }

        private void AboutNav_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(AboutPage, "关于软件", AboutNav);
        }

        private void LogsNav_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(LogsPage, "操作日志", LogsNav);
            LoadLogs();
        }

        private void SettingsNav_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(SettingsPage, "软件设置", SettingsNav);
            ApplySettingsToUI();
        }

        private void JavaServiceNav_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(JavaServicePage, "本地开发环境", JavaServiceNav);

            LoadJavaServices();
            LoadLaunchPlans();
        }

        private void ShowPage(
            UIElement selectedPage,
            string title,
            Button selectedButton)
        {
            DashboardPage.Visibility = Visibility.Collapsed;
            PortPage.Visibility = Visibility.Collapsed;
            ProcessPage.Visibility = Visibility.Collapsed;
            ScriptPage.Visibility = Visibility.Collapsed;
            AboutPage.Visibility = Visibility.Collapsed;
            LogsPage.Visibility = Visibility.Collapsed;
            SettingsPage.Visibility = Visibility.Collapsed;
            JavaServicePage.Visibility = Visibility.Collapsed;

            selectedPage.Visibility = Visibility.Visible;
            PageTitle.Text = title;

            Button[] buttons =
            {
                DashboardNav,
                PortNav,
                ProcessNav,
                ScriptNav,
                AboutNav,
                LogsNav,
                SettingsNav,
                JavaServiceNav
            };

            foreach (Button button in buttons)
            {
                button.ClearValue(BackgroundProperty);
                button.ClearValue(ForegroundProperty);
            }

            selectedButton.Background =
                new SolidColorBrush(Color.FromRgb(29, 53, 88));

            selectedButton.Foreground =
                new SolidColorBrush(Color.FromRgb(147, 197, 253));
        }

        // ==================== 端口管理 ====================

        private async void RefreshPorts_Click(object sender, RoutedEventArgs e)
        {
            await LoadPortsAsync();
        }


        private void CopyPortResult_Click(object sender, RoutedEventArgs e)
        {
            if (_visiblePorts.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "当前没有可复制的端口记录。",
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                var lines = new List<string>
                {
                    "本地地址\t端口\tPID\t进程名称\t状态"
                };

                lines.AddRange(_visiblePorts.Select(p =>
                    $"{p.LocalAddress}\t{p.Port}\t{p.ProcessId}\t{p.ProcessName}\t{p.Status}"));

                Clipboard.SetText(string.Join(Environment.NewLine, lines));
                PortMessage.Text = $"已复制 {_visiblePorts.Count} 条端口记录。";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "复制失败：" + ex.Message,
                    "操作失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private async void KillPortProcess_Click(object sender, RoutedEventArgs e)
        {
            if (PortDataGrid.SelectedItem is not PortInfo selected)
            {
                MessageBox.Show(
                    this,
                    "请先在端口列表中选中一条记录。",
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (selected.ProcessId <= 4 ||
                selected.ProcessId == Environment.ProcessId)
            {
                MessageBox.Show(
                    this,
                    "为避免影响 Windows 或关闭本工具自身，已阻止此操作。",
                    "操作已阻止",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                this,
                $"确定要结束该端口对应的整个进程吗？\n\n" +
                $"本地地址：{selected.LocalAddress}\n" +
                $"端口：{selected.Port}\n" +
                $"进程名称：{selected.ProcessName}\n" +
                $"PID：{selected.ProcessId}\n\n" +
                "注意：结束的是整个进程，而不只是这个端口。该进程的其他服务也可能受到影响。",
                "确认结束进程",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                using Process process = Process.GetProcessById(selected.ProcessId);

                // 再次检查 PID 对应的进程，避免进程退出后 PID 被其他进程复用。
                if (!process.ProcessName.Equals(
                    selected.ProcessName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        this,
                        "进程信息已经发生变化，请刷新端口列表后重试。",
                        "操作已取消",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    await LoadPortsAsync();
                    return;
                }

                process.Kill();
                process.WaitForExit(3000);

                AppLogger.Info(
                    $"通过端口结束进程：{selected.ProcessName}，PID：{selected.ProcessId}，端口：{selected.Port}。");

                MessageBox.Show(
                    this,
                    $"已发送结束进程指令：{selected.ProcessName}（PID：{selected.ProcessId}）。",
                    "操作完成",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await LoadPortsAsync();
            }
            catch (ArgumentException)
            {
                MessageBox.Show(
                    this,
                    "该进程已经退出，请刷新端口列表。",
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                await LoadPortsAsync();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                MessageBox.Show(
                    this,
                    "结束进程失败，可能是权限不足。\n\n" + ex.Message,
                    "操作失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(
                    this,
                    "操作失败，进程可能已经退出。\n\n" + ex.Message,
                    "操作失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                await LoadPortsAsync();
            }
        }

        private void PortSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _portCurrentPage = 1;
            RefreshPortPage();
        }


        private async Task LoadPortsAsync()
        {
            int loadVersion = ++_portLoadVersion;

            var cts = new CancellationTokenSource();
            CancellationTokenSource? previousCts = _portLoadCts;
            _portLoadCts = cts;

            // 取消上一次端口读取
            previousCts?.Cancel();

            AppLogger.Info("开始查询 TCP 监听端口。");
            PortMessage.Text = "正在读取系统 TCP 监听信息……";

            try
            {
                List<PortInfo> ports = await FetchPortsAsync(cts.Token);

                if (loadVersion != _portLoadVersion)
                {
                    return;
                }

                _allPorts = ports;
                _portCurrentPage = 1;
                RefreshPortPage();

                AppLogger.Info($"TCP 端口查询完成，共 {_allPorts.Count} 条记录。");
                PortMessage.Text = $"查询完成，读取到 {_allPorts.Count} 条 TCP 监听记录。";
            }
            catch (OperationCanceledException)
            {
                // 兼容其他可能产生的取消异常
                return;
            }
            catch (Exception ex)
            {
                if (loadVersion != _portLoadVersion)
                {
                    return;
                }

                AppLogger.Error("TCP 端口查询失败。", ex);
                PortMessage.Text = "查询失败：" + ex.Message;

                MessageBox.Show(
                    this,
                    "读取 TCP 监听端口失败。\n\n" + ex.Message,
                    "端口查询错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                if (ReferenceEquals(_portLoadCts, cts))
                {
                    _portLoadCts = null;
                }

                cts.Dispose();
            }
        }


        private async Task<List<PortInfo>> FetchPortsAsync(
    CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var processNames = new Dictionary<int, string>();

                // 获取系统进程名称
                Process[] processes = Process.GetProcesses();

                try
                {
                    foreach (Process process in processes)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return new List<PortInfo>();
                        }

                        try
                        {
                            processNames[process.Id] = process.ProcessName;
                        }
                        catch (System.ComponentModel.Win32Exception)
                        {
                        }
                        catch (InvalidOperationException)
                        {
                        }
                    }
                }
                finally
                {
                    foreach (Process process in processes)
                    {
                        process.Dispose();
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return new List<PortInfo>();
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = "netstat.exe",
                    Arguments = "-ano -p tcp",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var netstatProcess = Process.Start(startInfo);

                if (netstatProcess == null)
                {
                    throw new InvalidOperationException("无法启动 netstat.exe。");
                }

                // 取消任务时，同时结束 netstat 子进程
                using CancellationTokenRegistration registration =
                    cancellationToken.Register(() =>
                    {
                        try
                        {
                            if (!netstatProcess.HasExited)
                            {
                                netstatProcess.Kill(true);
                            }
                        }
                        catch
                        {
                            // 进程可能已经退出
                        }
                    });

                string output = netstatProcess.StandardOutput.ReadToEnd();
                string error = netstatProcess.StandardError.ReadToEnd();

                netstatProcess.WaitForExit();

                if (cancellationToken.IsCancellationRequested)
                {
                    return new List<PortInfo>();
                }

                if (netstatProcess.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(error)
                            ? "netstat 执行失败。"
                            : error.Trim());
                }

                var result = new List<PortInfo>();

                foreach (string line in output.Split(
                    new[] { '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return new List<PortInfo>();
                    }

                    string[] parts = line.Split(
                        (char[]?)null,
                        StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length < 5 ||
                        !parts[0].Equals("TCP", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string status = parts[^2];

                    if (!status.Equals("LISTENING", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string localEndpoint = parts[1];
                    int separator = localEndpoint.LastIndexOf(':');

                    if (separator < 0 ||
                        !int.TryParse(localEndpoint[(separator + 1)..], out int port) ||
                        !int.TryParse(parts[^1], out int processId))
                    {
                        continue;
                    }

                    string address = localEndpoint[..separator];

                    if (address.StartsWith("[") && address.EndsWith("]"))
                    {
                        address = address[1..^1];
                    }

                    string processName = processNames.TryGetValue(
                        processId,
                        out string? name)
                        ? name
                        : "未知进程";

                    result.Add(new PortInfo
                    {
                        LocalAddress = address,
                        Port = port,
                        ProcessId = processId,
                        ProcessName = processName,
                        Status = status
                    });
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return new List<PortInfo>();
                }

                return result
                    .OrderBy(p => p.Port)
                    .ThenBy(p => p.LocalAddress)
                    .ToList();

            });
        }


        private void ApplyPortFilter()
        {
            string keyword = PortSearchBox.Text.Trim();

            IEnumerable<PortInfo> filtered = _allPorts;

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                filtered = filtered.Where(p =>
                    p.Port.ToString().Contains(
                        keyword, StringComparison.OrdinalIgnoreCase) ||
                    p.ProcessId.ToString().Contains(
                        keyword, StringComparison.OrdinalIgnoreCase) ||
                    p.LocalAddress.Contains(
                        keyword, StringComparison.OrdinalIgnoreCase) ||
                    p.ProcessName.Contains(
                        keyword, StringComparison.OrdinalIgnoreCase));
            }

            List<PortInfo> result = filtered.ToList();

            PortDataGrid.ItemsSource = result;

            PortSummary.Text =
                $"共 {_allPorts.Count} 条监听记录，当前显示 {result.Count} 条";
        }

        // ==================== 进程管理 ====================

        private async void RefreshProcesses_Click(object sender, RoutedEventArgs e)
        {
            await LoadProcessesAsync();
        }

        private void ProcessSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _processCurrentPage = 1;
            RefreshProcessPage();
        }


        private async Task LoadProcessesAsync()
        {
            int loadVersion = ++_processLoadVersion;

            var cts = new CancellationTokenSource();
            CancellationTokenSource? previousCts = _processLoadCts;
            _processLoadCts = cts;

            // 取消上一次进程读取
            previousCts?.Cancel();

            AppLogger.Info("开始查询系统进程。");
            ProcessMessage.Text = "正在读取系统进程……";

            try
            {
                List<ProcessInfo> processes = await FetchProcessesAsync(cts.Token);

                if (loadVersion != _processLoadVersion)
                {
                    return;
                }

                _allProcesses = processes;
                _processCurrentPage = 1;
                RefreshProcessPage();

                ProcessMessage.Text = $"查询完成，共读取到 {_allProcesses.Count} 个进程。";

                AppLogger.Info($"系统进程查询完成，共 {_allProcesses.Count} 个进程。");
            }
            catch (OperationCanceledException)
            {
                // 兼容其他可能产生的取消异常
                return;
            }
            catch (Exception ex)
            {
                if (loadVersion != _processLoadVersion)
                {
                    return;
                }

                AppLogger.Error("系统进程查询失败。", ex);
                ProcessMessage.Text = "查询失败：" + ex.Message;

                MessageBox.Show(
                    this,
                    "读取系统进程失败。\n\n" + ex.Message,
                    "进程查询错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                if (ReferenceEquals(_processLoadCts, cts))
                {
                    _processLoadCts = null;
                }

                cts.Dispose();
            }
        }

        private async Task<List<ProcessInfo>> FetchProcessesAsync(
    CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var result = new List<ProcessInfo>();

                Process[] processes = Process.GetProcesses();

                try
                {
                    foreach (Process process in processes)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return new List<ProcessInfo>();
                        }

                        try
                        {
                            int processId = process.Id;
                            string processName = process.ProcessName;

                            double memoryMb = 0;

                            try
                            {
                                memoryMb = process.WorkingSet64 / 1024d / 1024d;
                            }
                            catch (System.ComponentModel.Win32Exception)
                            {
                            }
                            catch (InvalidOperationException)
                            {
                            }

                            string windowTitle = string.Empty;

                            try
                            {
                                windowTitle = process.MainWindowTitle;
                            }
                            catch (System.ComponentModel.Win32Exception)
                            {
                            }
                            catch (InvalidOperationException)
                            {
                            }

                            result.Add(new ProcessInfo
                            {
                                ProcessName = processName,
                                ProcessId = processId,
                                MemoryMb = Math.Round(memoryMb, 2),
                                WindowTitle = windowTitle
                            });
                        }
                        catch (System.ComponentModel.Win32Exception)
                        {
                        }
                        catch (InvalidOperationException)
                        {
                        }
                    }
                }
                finally
                {
                    foreach (Process process in processes)
                    {
                        process.Dispose();
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return new List<ProcessInfo>();
                }

                return result
                    .OrderBy(p => p.ProcessName)
                    .ThenBy(p => p.ProcessId)
                    .ToList();

            });
        }



        private void ApplyProcessFilter()
        {
            string keyword = ProcessSearchBox.Text.Trim();

            IEnumerable<ProcessInfo> filtered = _allProcesses;

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                filtered = filtered.Where(p =>
                    p.ProcessName.Contains(
                        keyword, StringComparison.OrdinalIgnoreCase) ||
                    p.ProcessId.ToString().Contains(
                        keyword, StringComparison.OrdinalIgnoreCase));
            }

            List<ProcessInfo> result = filtered.ToList();

            ProcessDataGrid.ItemsSource = result;

            ProcessSummary.Text =
                $"共 {_allProcesses.Count} 个进程，当前显示 {result.Count} 个";
        }

        private async void KillProcess_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessDataGrid.SelectedItem is not ProcessInfo selected)
            {
                MessageBox.Show(
                    this,
                    "请先在列表中选中一个进程。",
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            // 防止结束本工具自身，以及 Windows 的关键系统进程。
            if (selected.ProcessId == Environment.ProcessId ||
                selected.ProcessId <= 4)
            {
                MessageBox.Show(
                    this,
                    "为避免关闭本工具或影响 Windows 运行，不能结束该进程。",
                    "操作已阻止",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            MessageBoxResult result = MessageBox.Show(
                this,
                $"确定要结束以下进程吗？\n\n" +
                $"进程名称：{selected.ProcessName}\n" +
                $"PID：{selected.ProcessId}\n\n" +
                "结束进程可能导致未保存的数据丢失。",
                "确认结束进程",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                using Process process =
                    Process.GetProcessById(selected.ProcessId);

                process.Kill();
                process.WaitForExit(3000);

                AppLogger.Info(
                    $"结束进程操作：{selected.ProcessName}，PID：{selected.ProcessId}。");

                MessageBox.Show(
                    this,
                    $"已发送结束进程指令：{selected.ProcessName}（PID：{selected.ProcessId}）。",
                    "操作完成",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await LoadProcessesAsync();
            }
            catch (ArgumentException)
            {
                MessageBox.Show(
                    this,
                    "该进程已经退出。",
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await LoadProcessesAsync();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                MessageBox.Show(
                    this,
                    "结束进程失败，可能是权限不足。\n\n" + ex.Message,
                    "操作失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(
                    this,
                    "结束进程失败，进程可能已经退出。\n\n" + ex.Message,
                    "操作失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                await LoadProcessesAsync();
            }
        }


        // ==================== 脚本编码工具 ====================

        private void ConvertEncoding_Click(object sender, RoutedEventArgs e)
        {
            string input = EncodingInputBox.Text;
            string mode = (EncodingModeBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
                          ?? string.Empty;

            try
            {
                string output;

                switch (mode)
                {
                    case "Base64 编码":
                        output = Convert.ToBase64String(
                            Encoding.UTF8.GetBytes(input));
                        break;

                    case "Base64 解码":
                        output = new UTF8Encoding(false, true).GetString(
                            Convert.FromBase64String(input.Trim()));
                        break;

                    case "URL 编码":
                        output = WebUtility.UrlEncode(input);
                        break;

                    case "URL 解码":
                        output = WebUtility.UrlDecode(input);
                        break;

                    case "文本转十六进制":
                        output = Convert.ToHexString(
                            Encoding.UTF8.GetBytes(input));
                        break;

                    case "十六进制转文本":
                        string hex = input
                            .Replace(" ", "")
                            .Replace("\r", "")
                            .Replace("\n", "")
                            .Replace("\t", "");

                        if (hex.Length % 2 != 0)
                        {
                            throw new FormatException(
                                "十六进制内容必须由偶数个字符组成。");
                        }

                        output = new UTF8Encoding(false, true).GetString(
                            Convert.FromHexString(hex));
                        break;

                    default:
                        throw new InvalidOperationException("请选择有效的转换类型。");
                }

                EncodingOutputBox.Text = output;
                EncodingMessage.Text = "转换成功。";

                AppLogger.Info($"编码转换成功，转换类型：{mode}。");
            }
            catch (FormatException ex)
            {
                AppLogger.Error($"编码转换失败，转换类型：{mode}。", ex);
                EncodingOutputBox.Clear();
                EncodingMessage.Text = "转换失败：" + ex.Message;

                MessageBox.Show(
                    this,
                    "输入内容格式不正确，请检查后重试。\n\n" + ex.Message,
                    "转换失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (DecoderFallbackException ex)
            {
                AppLogger.Error($"编码转换失败，转换类型：{mode}。", ex);
                EncodingOutputBox.Clear();
                EncodingMessage.Text = "解码失败：" + ex.Message;

                MessageBox.Show(
                    this,
                    "解码后的字节不是有效的 UTF-8 文本。\n\n" + ex.Message,
                    "解码失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"编码转换失败，转换类型：{mode}。", ex);
                EncodingOutputBox.Clear();
                EncodingMessage.Text = "转换失败：" + ex.Message;

                MessageBox.Show(
                    this,
                    "转换过程中发生错误。\n\n" + ex.Message,
                    "转换错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SwapEncoding_Click(object sender, RoutedEventArgs e)
        {
            string input = EncodingInputBox.Text;
            string output = EncodingOutputBox.Text;

            EncodingInputBox.Text = output;
            EncodingOutputBox.Text = input;

            EncodingMessage.Text = "已交换输入和输出内容。";
        }

        private void ClearEncoding_Click(object sender, RoutedEventArgs e)
        {
            EncodingInputBox.Clear();
            EncodingOutputBox.Clear();

            EncodingMessage.Text = "内容已清空。";
            EncodingInputBox.Focus();
        }

        private void CopyEncoding_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(EncodingOutputBox.Text))
            {
                EncodingMessage.Text = "没有可复制的转换结果。";
                return;
            }

            try
            {
                Clipboard.SetText(EncodingOutputBox.Text);
                EncodingMessage.Text = "结果已复制到剪贴板。";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "复制失败，请重试。\n\n" + ex.Message,
                    "复制失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }



        // ==================== 操作日志 ====================

        private void RefreshLogs_Click(object sender, RoutedEventArgs e)
        {
            LoadLogs();
        }

        private void LogsSearchBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            ApplyLogFilter();
        }

        private void LoadLogs()
        {
            try
            {
                LogsPathText.Text = "日志目录：" + AppLogger.LogDirectoryPath;

                Directory.CreateDirectory(AppLogger.LogDirectoryPath);

                _allLogLines.Clear();

                string[] files = Directory.GetFiles(
                    AppLogger.LogDirectoryPath,
                    "*.log");

                foreach (string file in files.OrderByDescending(
                    File.GetLastWriteTime))
                {
                    _allLogLines.Add(
                        $"========== {Path.GetFileName(file)} ==========");

                    _allLogLines.AddRange(
                        File.ReadAllLines(file, Encoding.UTF8));
                }

                ApplyLogFilter();

                LogsMessage.Text =
                    $"读取完成，共 {files.Length} 个日志文件，{_allLogLines.Count} 行内容。";
            }
            catch (Exception ex)
            {
                LogsMessage.Text = "读取日志失败：" + ex.Message;
                AppLogger.Error("读取操作日志失败。", ex);
            }
        }

        private void ApplyLogFilter()
        {
            string keyword = LogsSearchBox.Text.Trim();

            IEnumerable<string> lines = _allLogLines;

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                lines = _allLogLines.Where(line =>
                    line.Contains(
                        keyword,
                        StringComparison.OrdinalIgnoreCase));
            }

            LogsTextBox.Text = string.Join(Environment.NewLine, lines);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                LogsMessage.Text =
                    $"搜索完成，共匹配 {lines.Count()} 行。";
            }
        }

        private void OpenLogDirectory_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(AppLogger.LogDirectoryPath);

                Process.Start(new ProcessStartInfo
                {
                    FileName = AppLogger.LogDirectoryPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "打开日志目录失败。\n\n" + ex.Message,
                    "操作失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                this,
                "确定要删除日志目录中的所有 .log 文件吗？\n\n此操作不可恢复。",
                "确认清空日志",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                string[] files = Directory.GetFiles(
                    AppLogger.LogDirectoryPath,
                    "*.log");

                foreach (string file in files)
                {
                    File.Delete(file);
                }

                _allLogLines.Clear();
                LogsTextBox.Clear();

                LogsMessage.Text = $"已删除 {files.Length} 个日志文件。";

                AppLogger.Info("用户清空了全部日志文件。");
            }
            catch (Exception ex)
            {
                LogsMessage.Text = "清空日志失败：" + ex.Message;
                AppLogger.Error("清空日志失败。", ex);

                MessageBox.Show(
                    this,
                    "清空日志失败。\n\n" + ex.Message,
                    "操作失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }



        // ==================== 工作台 ====================

        private async void RefreshDashboard_Click(object sender, RoutedEventArgs e)
        {
            await LoadDashboardAsync();
        }


        private async Task LoadDashboardAsync()
        {
            int loadVersion = ++_dashboardLoadVersion;

            var cts = new CancellationTokenSource();
            CancellationTokenSource? previousCts = _dashboardLoadCts;
            _dashboardLoadCts = cts;

            // 取消上一次工作台读取
            previousCts?.Cancel();

            DashboardStatus.Text = "正在获取系统概况……";

            try
            {
                Task<List<ProcessInfo>> processTask =
                    FetchProcessesAsync(cts.Token);

                Task<List<PortInfo>> portTask =
                    FetchPortsAsync(cts.Token);

                await Task.WhenAll(processTask, portTask);

                if (loadVersion != _dashboardLoadVersion)
                {
                    return;
                }

                List<ProcessInfo> processes = await processTask;
                List<PortInfo> ports = await portTask;

                DashboardProcessCount.Text = processes.Count.ToString();
                DashboardPortCount.Text = ports.Count.ToString();

                DashboardStatus.Text =
                    $"数据刷新完成。当前读取到 {processes.Count} 个进程，" +
                    $"{ports.Count} 条 TCP 监听记录。";

                DashboardUpdatedAt.Text =
                    $"最近刷新：{DateTime.Now:yyyy-MM-dd HH:mm:ss}";

                AppLogger.Info(
                    $"工作台刷新完成：进程数={processes.Count}，TCP 监听数={ports.Count}。");
            }
            catch (OperationCanceledException)
            {
                // 用户发起了更新的工作台读取请求，无需提示错误
            }
            catch (Exception ex)
            {
                if (loadVersion != _dashboardLoadVersion)
                {
                    return;
                }

                AppLogger.Error("刷新工作台失败。", ex);
                DashboardStatus.Text = "刷新工作台失败：" + ex.Message;
            }
            finally
            {
                if (ReferenceEquals(_dashboardLoadCts, cts))
                {
                    _dashboardLoadCts = null;
                }

                cts.Dispose();
            }
        }



        // ==================== 软件设置 ====================

        private void ApplySettingsToUI()
        {
            string retentionDays = _settings.LogRetentionDays.ToString();

            foreach (ComboBoxItem item in LogRetentionBox.Items)
            {
                if (item.Tag?.ToString() == retentionDays)
                {
                    LogRetentionBox.SelectedItem = item;
                    break;
                }
            }

            AutoCleanLogsCheckBox.IsChecked = _settings.AutoCleanLogs;
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (LogRetentionBox.SelectedItem is not ComboBoxItem selectedItem ||
                    !int.TryParse(selectedItem.Tag?.ToString(), out int retentionDays))
                {
                    MessageBox.Show(
                        this,
                        "请选择有效的日志保留天数。",
                        "设置错误",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                _settings.LogRetentionDays = retentionDays;
                _settings.AutoCleanLogs =
                    AutoCleanLogsCheckBox.IsChecked == true;

                SettingsManager.Save(_settings);

                AppLogger.Info(
                    $"软件设置已保存：日志保留天数={retentionDays}，自动清理={_settings.AutoCleanLogs}。");

                SettingsMessage.Text =
                    "设置保存成功，下次启动软件时将自动读取。";

                MessageBox.Show(
                    this,
                    "设置保存成功！",
                    "操作成功",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppLogger.Error("保存软件设置失败。", ex);

                SettingsMessage.Text = "保存失败：" + ex.Message;

                MessageBox.Show(
                    this,
                    "保存设置失败。\n\n" + ex.Message,
                    "操作失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                this,
                "确定要恢复默认设置吗？\n\n默认日志保留 30 天，并开启启动时自动清理。",
                "恢复默认设置",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            _settings = new AppSettings();

            try
            {
                SettingsManager.Save(_settings);
                ApplySettingsToUI();

                SettingsMessage.Text = "已恢复默认设置并保存。";

                AppLogger.Info("软件设置已恢复默认值。");
            }
            catch (Exception ex)
            {
                AppLogger.Error("恢复默认设置失败。", ex);

                SettingsMessage.Text = "恢复失败：" + ex.Message;
            }
        }

        // ==================== Java 服务管理 ====================

        private void LoadJavaServices()
        {
            try
            {
                List<ServiceInfo> services = ServiceManager.LoadAll();

                _javaServices.Clear();

                foreach (ServiceInfo service in services)
                {
                    RefreshJavaServiceRuntimeInfo(service);
                    _javaServices.Add(service);
                }

                JavaServiceCountText.Text = $"共 {_javaServices.Count} 个服务";
                JavaServiceMessage.Text = "服务配置加载成功。";
            }
            catch (Exception ex)
            {
                AppLogger.Error("加载 Java 服务列表失败。", ex);
                JavaServiceMessage.Text = "加载失败：" + ex.Message;
            }
        }



        /// <summary>
        /// 刷新单个 Java 服务的运行时信息
        /// </summary>
        private void RefreshJavaServiceRuntimeInfo(ServiceInfo service)
        {
            ServiceRuntimeInfo? runtimeInfo =
                JavaProcessManager.GetRuntimeInfo(service.Id);

            if (runtimeInfo == null)
            {
                service.RuntimeStatus = "Stopped";
                service.RuntimeProcessId = 0;
                service.RuntimeStartTime = "-";
                return;
            }

            service.RuntimeStatus = runtimeInfo.Status;
            service.RuntimeProcessId = runtimeInfo.ProcessId;

            service.RuntimeStartTime = runtimeInfo.StartTime.HasValue
                ? runtimeInfo.StartTime.Value.ToString("yyyy-MM-dd HH:mm:ss")
                : "-";
        }

        /// <summary>
        /// 根据服务 ID 获取服务配置
        /// </summary>
        private ServiceInfo? FindJavaService(string serviceId)
        {
            return _javaServices.FirstOrDefault(x => x.Id == serviceId);
        }


        private async void StartJavaService_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.Tag == null)
            {
                return;
            }

            string serviceId = button.Tag.ToString() ?? string.Empty;
            ServiceInfo? service = FindJavaService(serviceId);

            if (service == null)
            {
                MessageBox.Show(this, "找不到对应的服务配置，请刷新列表。",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ServiceRuntimeInfo? runtimeInfo =
                JavaProcessManager.GetRuntimeInfo(serviceId);

            if (runtimeInfo?.Status is "Starting" or "WaitingReady" or "Running")
            {
                MessageBox.Show(this, "该服务已经启动或正在启动中。",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                JavaServiceMessage.Text = $"正在启动服务：{service.Name}……";

                AppLogger.Info($"开始启动 Java 服务：{service.Name}");

                int processId = await JavaProcessManager.StartAsync(service);

                RefreshJavaServiceRuntimeInfo(service);
                JavaServiceDataGrid.Items.Refresh();

                JavaServiceMessage.Text =
                    $"服务启动进程已创建：{service.Name}，PID：{processId}";

                AppLogger.Info(
                    $"Java 服务启动进程已创建：{service.Name}，PID：{processId}");
            }
            catch (Exception ex)
            {
                RefreshJavaServiceRuntimeInfo(service);
                JavaServiceDataGrid.Items.Refresh();

                JavaServiceMessage.Text = $"启动失败：{service.Name}";

                AppLogger.Error($"启动 Java 服务失败：{service.Name}", ex);

                MessageBox.Show(
                    this,
                    $"启动服务失败：{service.Name}\n\n{ex.Message}",
                    "启动失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }



        private async void StopJavaService_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.Tag == null)
            {
                return;
            }

            string serviceId = button.Tag.ToString() ?? string.Empty;
            ServiceInfo? service = FindJavaService(serviceId);

            if (service == null)
            {
                MessageBox.Show(this, "找不到对应的服务配置，请刷新列表。",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ServiceRuntimeInfo? runtimeInfo =
                JavaProcessManager.GetRuntimeInfo(serviceId);

            if (runtimeInfo == null ||
                runtimeInfo.Status == "Stopped")
            {
                MessageBox.Show(this, "该服务当前没有运行。",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                this,
                $"确定要停止服务「{service.Name}」吗？\n\n" +
                $"PID：{runtimeInfo.ProcessId}",
                "停止 Java 服务",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                JavaServiceMessage.Text = $"正在停止服务：{service.Name}……";

                await JavaProcessManager.StopAsync(serviceId);

                RefreshJavaServiceRuntimeInfo(service);
                JavaServiceDataGrid.Items.Refresh();

                JavaServiceMessage.Text = $"服务已停止：{service.Name}";

                AppLogger.Info($"停止 Java 服务：{service.Name}");
            }
            catch (Exception ex)
            {
                RefreshJavaServiceRuntimeInfo(service);
                JavaServiceDataGrid.Items.Refresh();

                JavaServiceMessage.Text = $"停止失败：{service.Name}";

                AppLogger.Error($"停止 Java 服务失败：{service.Name}", ex);

                MessageBox.Show(
                    this,
                    $"停止服务失败：{service.Name}\n\n{ex.Message}",
                    "停止失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }



        private void ViewJavaServiceLog_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.Tag == null)
            {
                return;
            }

            string serviceId = button.Tag.ToString() ?? string.Empty;
            ServiceInfo? service = FindJavaService(serviceId);

            if (service == null)
            {
                MessageBox.Show(this, "找不到对应的服务配置。",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                string logFilePath =
                    JavaProcessManager.GetLogFilePath(serviceId);

                if (string.IsNullOrWhiteSpace(logFilePath) ||
                    !File.Exists(logFilePath))
                {
                    MessageBox.Show(
                        this,
                        "该服务暂时没有日志文件。\n\n请先启动服务。",
                        "日志不存在",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = $"\"{logFilePath}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLogger.Error($"打开 Java 服务日志失败：{service.Name}", ex);

                MessageBox.Show(
                    this,
                    "打开日志失败：\n\n" + ex.Message,
                    "操作失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }




        // ==================== 启动方案管理 ====================

        private void LoadLaunchPlans()
        {
            try
            {
                List<LaunchPlan> plans = LaunchPlanManager.LoadAll();

                _launchPlans.Clear();

                foreach (LaunchPlan plan in plans.OrderBy(x => x.CreatedAt))
                {
                    _launchPlans.Add(plan);
                }

                LaunchPlanCountText.Text = $"共 {_launchPlans.Count} 个方案";
                LaunchPlanMessage.Text = "启动方案加载成功。";
            }
            catch (Exception ex)
            {
                AppLogger.Error("加载启动方案失败。", ex);
                LaunchPlanMessage.Text = "加载失败：" + ex.Message;
            }
        }

        private void AddLaunchPlan_Click(object sender, RoutedEventArgs e)
        {
            var window = new LaunchPlanEditWindow
            {
                Owner = this
            };

            if (window.ShowDialog() != true || window.Plan == null)
            {
                return;
            }

            try
            {
                bool nameExists = _launchPlans.Any(x =>
                    string.Equals(
                        x.Name,
                        window.Plan.Name,
                        StringComparison.OrdinalIgnoreCase));

                if (nameExists)
                {
                    MessageBox.Show(
                        this,
                        "已经存在同名启动方案，请修改方案名称。",
                        "重复配置",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                LaunchPlanManager.AddOrUpdate(window.Plan);

                LoadLaunchPlans();

                AppLogger.Info($"新增启动方案：{window.Plan.Name}");
                LaunchPlanMessage.Text = $"已新增启动方案：{window.Plan.Name}";
            }
            catch (Exception ex)
            {
                AppLogger.Error("新增启动方案失败。", ex);

                MessageBox.Show(
                    this,
                    "新增启动方案失败：\n\n" + ex.Message,
                    "操作失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void EditLaunchPlan_Click(object sender, RoutedEventArgs e)
        {
            if (LaunchPlanDataGrid.SelectedItem is not LaunchPlan selectedPlan)
            {
                MessageBox.Show(
                    this,
                    "请先选择需要编辑的启动方案。",
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            var window = new LaunchPlanEditWindow(selectedPlan)
            {
                Owner = this
            };

            if (window.ShowDialog() != true || window.Plan == null)
            {
                return;
            }

            try
            {
                bool nameExists = _launchPlans.Any(x =>
                    x.Id != window.Plan.Id &&
                    string.Equals(
                        x.Name,
                        window.Plan.Name,
                        StringComparison.OrdinalIgnoreCase));

                if (nameExists)
                {
                    MessageBox.Show(
                        this,
                        "已经存在同名启动方案，请修改方案名称。",
                        "重复配置",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                LaunchPlanManager.AddOrUpdate(window.Plan);

                LoadLaunchPlans();

                AppLogger.Info($"编辑启动方案：{window.Plan.Name}");
                LaunchPlanMessage.Text = $"已更新启动方案：{window.Plan.Name}";
            }
            catch (Exception ex)
            {
                AppLogger.Error("编辑启动方案失败。", ex);

                MessageBox.Show(
                    this,
                    "编辑启动方案失败：\n\n" + ex.Message,
                    "操作失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void DeleteLaunchPlan_Click(object sender, RoutedEventArgs e)
        {
            if (LaunchPlanDataGrid.SelectedItem is not LaunchPlan selectedPlan)
            {
                MessageBox.Show(
                    this,
                    "请先选择需要删除的启动方案。",
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            MessageBoxResult result = MessageBox.Show(
                this,
                $"确定要删除启动方案「{selectedPlan.Name}」吗？",
                "删除启动方案",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                LaunchPlanManager.Delete(selectedPlan.Id);

                LoadLaunchPlans();

                AppLogger.Info($"删除启动方案：{selectedPlan.Name}");
                LaunchPlanMessage.Text = $"已删除启动方案：{selectedPlan.Name}";
            }
            catch (Exception ex)
            {
                AppLogger.Error("删除启动方案失败。", ex);

                MessageBox.Show(
                    this,
                    "删除启动方案失败：\n\n" + ex.Message,
                    "操作失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        private void RefreshJavaServices_Click(object sender, RoutedEventArgs e)
        {
            LoadJavaServices();
        }

        private void DeleteJavaService_Click(object sender, RoutedEventArgs e)
        {
            if (JavaServiceDataGrid.SelectedItem is not ServiceInfo selectedService)
            {
                MessageBox.Show(
                    this,
                    "请先选择需要删除的服务。",
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            MessageBoxResult result = MessageBox.Show(
                this,
                $"确定要删除服务「{selectedService.Name}」吗？\n\n删除后不会影响项目源代码。",
                "删除服务",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                ServiceManager.Delete(selectedService.Id);

                LoadJavaServices();

                AppLogger.Info($"删除 Java 服务配置：{selectedService.Name}");

                JavaServiceMessage.Text = $"已删除服务：{selectedService.Name}";
            }
            catch (Exception ex)
            {
                AppLogger.Error("删除 Java 服务配置失败。", ex);

                MessageBox.Show(
                    this,
                    "删除失败：" + ex.Message,
                    "操作失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        private void ConfigureLaunchPlanStages_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (LaunchPlanDataGrid.SelectedItem is not LaunchPlan selectedPlan)
            {
                MessageBox.Show(
                    this,
                    "请先选择需要配置阶段的启动方案。",
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            try
            {
                List<ServiceInfo> services = ServiceManager.LoadAll();

                var window = new LaunchPlanStagesWindow(
                    selectedPlan,
                    services)
                {
                    Owner = this
                };

                if (window.ShowDialog() != true)
                {
                    return;
                }

                selectedPlan.Stages = window.Stages
                    .OrderBy(x => x.Order)
                    .ToList();

                LaunchPlanManager.AddOrUpdate(selectedPlan);

                LoadLaunchPlans();

                AppLogger.Info(
                    $"更新启动方案阶段：{selectedPlan.Name}，阶段数={selectedPlan.Stages.Count}");

                LaunchPlanMessage.Text =
                    $"已保存方案「{selectedPlan.Name}」的阶段配置。";
            }
            catch (Exception ex)
            {
                AppLogger.Error("配置启动方案阶段失败。", ex);

                MessageBox.Show(
                    this,
                    "配置启动方案阶段失败：\n\n" + ex.Message,
                    "操作失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }



        // ==================== 添加 Java 服务 ====================

        private void AddJavaService_Click(object sender, RoutedEventArgs e)
        {
            var window = new ServiceEditWindow
            {
                Owner = this
            };

            if (window.ShowDialog() != true || window.Service == null)
            {
                return;
            }

            try
            {
                bool nameExists = _javaServices.Any(
                    x => string.Equals(
                        x.Name,
                        window.Service.Name,
                        StringComparison.OrdinalIgnoreCase));

                if (nameExists)
                {
                    MessageBox.Show(
                        this,
                        "已经存在同名服务，请修改服务名称。",
                        "重复配置",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                ServiceManager.Add(window.Service);

                LoadJavaServices();

                AppLogger.Info($"添加 Java 服务配置：{window.Service.Name}");

                JavaServiceMessage.Text = $"已添加服务：{window.Service.Name}";
            }
            catch (Exception ex)
            {
                AppLogger.Error("添加 Java 服务配置失败。", ex);

                MessageBox.Show(
                    this,
                    "添加服务失败：" + ex.Message,
                    "操作失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ==================== 编辑 Java 服务 ====================

        private void EditJavaService_Click(object sender, RoutedEventArgs e)
        {
            if (JavaServiceDataGrid.SelectedItem is not ServiceInfo selectedService)
            {
                MessageBox.Show(
                    this,
                    "请先选择需要编辑的服务。",
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            var window = new ServiceEditWindow(selectedService)
            {
                Owner = this
            };

            if (window.ShowDialog() != true || window.Service == null)
            {
                return;
            }

            try
            {
                bool nameExists = _javaServices.Any(
                    x => x.Id != window.Service.Id &&
                         string.Equals(
                             x.Name,
                             window.Service.Name,
                             StringComparison.OrdinalIgnoreCase));

                if (nameExists)
                {
                    MessageBox.Show(
                        this,
                        "已经存在同名服务，请修改服务名称。",
                        "重复配置",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                ServiceManager.Update(window.Service);

                LoadJavaServices();

                AppLogger.Info($"编辑 Java 服务配置：{window.Service.Name}");

                JavaServiceMessage.Text = $"已更新服务：{window.Service.Name}";
            }
            catch (Exception ex)
            {
                AppLogger.Error("编辑 Java 服务配置失败。", ex);

                MessageBox.Show(
                    this,
                    "编辑服务失败：" + ex.Message,
                    "操作失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        private void RefreshProcessPage()
        {
            string keyword = ProcessSearchBox.Text.Trim();

            IEnumerable<ProcessInfo> filtered = _allProcesses;

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                filtered = filtered.Where(p =>
                    p.ProcessName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    p.ProcessId.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase));
            }

            List<ProcessInfo> result = filtered.ToList();

            int totalCount = result.Count;
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));

            _processCurrentPage = Math.Clamp(_processCurrentPage, 1, totalPages);

            ProcessDataGrid.ItemsSource = result
                .Skip((_processCurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            ProcessSummary.Text =
                $"共 {_allProcesses.Count} 个进程，当前显示 {totalCount} 个";

            ProcessPageInfo.Text =
                $"第 {_processCurrentPage} / {totalPages} 页，共 {totalCount} 条";
        }

        private void RefreshPortPage()
        {
            string keyword = PortSearchBox.Text.Trim();

            IEnumerable<PortInfo> filtered = _allPorts;

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                filtered = filtered.Where(p =>
                    p.Port.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    p.ProcessId.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    p.LocalAddress.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    p.ProcessName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            }

            List<PortInfo> result = filtered.ToList();

            int totalCount = result.Count;
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));

            _portCurrentPage = Math.Clamp(_portCurrentPage, 1, totalPages);

            PortDataGrid.ItemsSource = result
                .Skip((_portCurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            PortSummary.Text =
                $"共 {_allPorts.Count} 条监听记录，当前显示 {totalCount} 条";

            PortPageInfo.Text =
                $"第 {_portCurrentPage} / {totalPages} 页，共 {totalCount} 条";
        }


        private void ProcessFirstPage_Click(object sender, RoutedEventArgs e)
        {
            _processCurrentPage = 1;
            RefreshProcessPage();
        }

        private void ProcessPreviousPage_Click(object sender, RoutedEventArgs e)
        {
            _processCurrentPage--;
            RefreshProcessPage();
        }

        private void ProcessNextPage_Click(object sender, RoutedEventArgs e)
        {
            _processCurrentPage++;
            RefreshProcessPage();
        }

        private void ProcessLastPage_Click(object sender, RoutedEventArgs e)
        {
            string keyword = ProcessSearchBox.Text.Trim();

            int count = _allProcesses.Count(p =>
                string.IsNullOrWhiteSpace(keyword) ||
                p.ProcessName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                p.ProcessId.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase));

            _processCurrentPage = Math.Max(1, (int)Math.Ceiling(count / (double)PageSize));

            RefreshProcessPage();
        }

        private void PortFirstPage_Click(object sender, RoutedEventArgs e)
        {
            _portCurrentPage = 1;
            RefreshPortPage();
        }

        private void PortPreviousPage_Click(object sender, RoutedEventArgs e)
        {
            _portCurrentPage--;
            RefreshPortPage();
        }

        private void PortNextPage_Click(object sender, RoutedEventArgs e)
        {
            _portCurrentPage++;
            RefreshPortPage();
        }

        private void PortLastPage_Click(object sender, RoutedEventArgs e)
        {
            string keyword = PortSearchBox.Text.Trim();

            int count = _allPorts.Count(p =>
                string.IsNullOrWhiteSpace(keyword) ||
                p.Port.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                p.ProcessId.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                p.LocalAddress.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                p.ProcessName.Contains(keyword, StringComparison.OrdinalIgnoreCase));

            _portCurrentPage = Math.Max(1, (int)Math.Ceiling(count / (double)PageSize));

            RefreshPortPage();
        }

    }
}
