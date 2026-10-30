
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DevTools.Models;

namespace DevTools
{
    public partial class LaunchStageEditWindow : Window
    {
        public LaunchStage? Stage { get; private set; }

        private readonly List<ServiceInfo> _services;
        private readonly LaunchStage? _existingStage;

        public LaunchStageEditWindow(
            List<ServiceInfo> services,
            LaunchStage? stage = null)
        {
            InitializeComponent();

            _services = services ?? new List<ServiceInfo>();
            _existingStage = stage;

            if (stage != null)
            {
                WindowTitleText.Text = "编辑启动阶段";
                StageNameTextBox.Text = stage.Name;
            }

            LoadServices();
        }

        private void LoadServices()
        {
            ServicesPanel.Children.Clear();

            if (_services.Count == 0)
            {
                EmptyServiceText.Visibility = Visibility.Visible;
                UpdateServiceCount();
                return;
            }

            EmptyServiceText.Visibility = Visibility.Collapsed;

            foreach (ServiceInfo service in _services.OrderBy(x => x.Name))
            {
                bool isSelected = _existingStage != null &&
                                  _existingStage.ServiceIds.Contains(service.Id);

                var checkBox = new CheckBox
                {
                    Content = $"{service.Name}    [{service.StartMode}]    端口：{service.Port}",
                    Tag = service.Id,
                    IsChecked = isSelected
                };

                checkBox.Checked += ServiceCheckBox_Changed;
                checkBox.Unchecked += ServiceCheckBox_Changed;

                ServicesPanel.Children.Add(checkBox);
            }

            UpdateServiceCount();
        }

        private void ServiceCheckBox_Changed(
            object sender,
            RoutedEventArgs e)
        {
            UpdateServiceCount();
        }

        private void UpdateServiceCount()
        {
            int count = ServicesPanel.Children
                .OfType<CheckBox>()
                .Count(x => x.IsChecked == true);

            ServiceCountText.Text = $"已选择 {count} 个服务";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string name = StageNameTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(
                    this,
                    "请输入阶段名称。",
                    "配置检查",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                StageNameTextBox.Focus();
                return;
            }

            List<string> serviceIds = ServicesPanel.Children
                .OfType<CheckBox>()
                .Where(x => x.IsChecked == true)
                .Select(x => x.Tag?.ToString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToList();

            if (serviceIds.Count == 0)
            {
                MessageBoxResult result = MessageBox.Show(
                    this,
                    "当前阶段没有选择任何服务，仍然要保存吗？",
                    "确认保存",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            Stage = new LaunchStage
            {
                Id = _existingStage?.Id ?? Guid.NewGuid().ToString(),
                Name = name,
                Order = _existingStage?.Order ?? 0,
                ExecutionMode = _existingStage?.ExecutionMode ?? "Parallel",
                ServiceIds = serviceIds
            };

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
