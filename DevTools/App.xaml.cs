
using System;
using System.Windows;
using DevTools.Services;

namespace DevTools
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 初始化程序目录
            AppPaths.EnsureDirectories();

            // 注册 UI 线程未处理异常
            DispatcherUnhandledException += (sender, args) =>
            {
                AppLogger.Error("程序发生未处理的 UI 异常。", args.Exception);
                MessageBox.Show(
                    "程序发生异常，请查看操作日志。",
                    "运行异常",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                args.Handled = true;
            };

            // 注册非 UI 线程未处理异常
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                if (args.ExceptionObject is Exception exception)
                {
                    AppLogger.Error("程序发生未处理的非 UI 线程异常。", exception);
                }
            };
        }
    }
}
