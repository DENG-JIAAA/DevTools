
using System;
using System.IO;
using System.Windows;
using DevTools.Models;
using Microsoft.Win32;

namespace DevTools
{
    public partial class ServiceEditWindow : Window
    {
        public ServiceInfo? Service { get; private set; }

        private readonly bool _isEdit;
        private readonly string _serviceId;

        public ServiceEditWindow(ServiceInfo? service = null)
        {
            InitializeComponent();

            _isEdit = service != null;
            _serviceId = service?.Id ?? Guid.NewGuid().ToString();

            if (_isEdit)
            {
                WindowTitleText.Text = "编辑 Java 服务";
                LoadService(service!);
            }
        }

        /// <summary>
        /// 将已有配置加载到表单
        /// </summary>
        private void LoadService(ServiceInfo service)
        {
            NameTextBox.Text = service.Name;
            ProjectPathTextBox.Text = service.ProjectPath;
            ModuleNameTextBox.Text = service.ModuleName;
            JarPathTextBox.Text = service.JarPath;
            MainClassTextBox.Text = service.MainClass;
            PortTextBox.Text = service.Port.ToString();
            ProfileTextBox.Text = service.Profile;
            JvmArgumentsTextBox.Text = service.JvmArguments;
            ApplicationArgumentsTextBox.Text = service.ApplicationArguments;
            EnvironmentVariablesTextBox.Text = service.EnvironmentVariables;

            MavenRadioButton.IsChecked = service.StartMode == "Maven";
            JarRadioButton.IsChecked = service.StartMode == "Jar";
        }

        /// <summary>
        /// 选择 Maven 项目目录
        /// </summary>
        private void BrowseProjectPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "选择 Java 项目目录",
                Multiselect = false
            };

            if (dialog.ShowDialog(this) == true)
            {
                ProjectPathTextBox.Text = dialog.FolderName;
            }
        }

        /// <summary>
        /// 选择 JAR 文件
        /// </summary>
        private void BrowseJarPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择 Java JAR 文件",
                Filter = "JAR 文件 (*.jar)|*.jar|所有文件 (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog(this) == true)
            {
                JarPathTextBox.Text = dialog.FileName;
            }
        }

        /// <summary>
        /// 保存服务配置
        /// </summary>
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string name = NameTextBox.Text.Trim();
            string projectPath = ProjectPathTextBox.Text.Trim();
            string jarPath = JarPathTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                ShowWarning("请输入服务名称。");
                NameTextBox.Focus();
                return;
            }

            bool isMaven = MavenRadioButton.IsChecked == true;

            if (isMaven)
            {
                if (string.IsNullOrWhiteSpace(projectPath))
                {
                    ShowWarning("请选择 Maven 项目目录。");
                    return;
                }

                if (!Directory.Exists(projectPath))
                {
                    ShowWarning("Maven 项目目录不存在。");
                    return;
                }

                if (!File.Exists(Path.Combine(projectPath, "pom.xml")))
                {
                    ShowWarning("所选目录下没有找到 pom.xml 文件。");
                    return;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(jarPath))
                {
                    ShowWarning("请选择 JAR 文件。");
                    return;
                }

                if (!File.Exists(jarPath))
                {
                    ShowWarning("指定的 JAR 文件不存在。");
                    return;
                }
            }

            if (!int.TryParse(PortTextBox.Text.Trim(), out int port) ||
                port < 1 || port > 65535)
            {
                ShowWarning("请输入有效的服务端口（1-65535）。");
                PortTextBox.Focus();
                return;
            }

            Service = new ServiceInfo
            {
                Id = _serviceId,

                Name = name,
                ProjectPath = projectPath,
                StartMode = isMaven ? "Maven" : "Jar",
                ModuleName = ModuleNameTextBox.Text.Trim(),
                JarPath = jarPath,
                MainClass = MainClassTextBox.Text.Trim(),
                Port = port,
                Profile = ProfileTextBox.Text.Trim(),
                JvmArguments = JvmArgumentsTextBox.Text.Trim(),
                ApplicationArguments = ApplicationArgumentsTextBox.Text.Trim(),
                EnvironmentVariables = EnvironmentVariablesTextBox.Text.Trim()
            };

            DialogResult = true;
        }

        private void ShowWarning(string message)
        {
            MessageBox.Show(
                this,
                message,
                "配置检查",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
