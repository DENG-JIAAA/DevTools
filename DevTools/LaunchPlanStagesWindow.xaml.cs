
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using DevTools.Models;

namespace DevTools
{
    public partial class LaunchPlanStagesWindow : Window
    {
        public List<LaunchStage> Stages { get; private set; } = new();

        private readonly List<ServiceInfo> _services;
        private readonly ObservableCollection<LaunchStage> _stages = new();

        public LaunchPlanStagesWindow(
            LaunchPlan plan,
            List<ServiceInfo> services)
        {
            InitializeComponent();

            _services = services ?? new List<ServiceInfo>();

            WindowTitleText.Text = $"启动阶段编排 - {plan.Name}";

            // 使用副本编辑，取消时不影响原方案
            Stages = plan.Stages
                .Select(x => new LaunchStage
                {
                    Id = x.Id,
                    Name = x.Name,
                    Order = x.Order,
                    ExecutionMode = x.ExecutionMode,
                    ServiceIds = new List<string>(x.ServiceIds)
                })
                .OrderBy(x => x.Order)
                .ToList();

            foreach (LaunchStage stage in Stages)
            {
                _stages.Add(stage);
            }

            StageDataGrid.ItemsSource = _stages;
            UpdateStageInfo();
        }

        private void AddStage_Click(object sender, RoutedEventArgs e)
        {
            var window = new LaunchStageEditWindow(_services)
            {
                Owner = this
            };

            if (window.ShowDialog() != true || window.Stage == null)
            {
                return;
            }

            _stages.Add(window.Stage);
            RefreshStageOrder();

            StageDataGrid.SelectedItem = window.Stage;
            UpdateStageInfo();
        }

        private void EditStage_Click(object sender, RoutedEventArgs e)
        {
            if (StageDataGrid.SelectedItem is not LaunchStage selected)
            {
                ShowMessage("请先选择需要编辑的阶段。");
                return;
            }

            var window = new LaunchStageEditWindow(
                _services,
                selected)
            {
                Owner = this
            };

            if (window.ShowDialog() != true || window.Stage == null)
            {
                return;
            }

            int index = _stages.IndexOf(selected);
            _stages[index] = window.Stage;

            RefreshStageOrder();
            StageDataGrid.SelectedItem = window.Stage;
            UpdateStageInfo();
        }

        private void DeleteStage_Click(object sender, RoutedEventArgs e)
        {
            if (StageDataGrid.SelectedItem is not LaunchStage selected)
            {
                ShowMessage("请先选择需要删除的阶段。");
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                this,
                $"确定删除阶段「{selected.Name}」吗？",
                "删除阶段",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            _stages.Remove(selected);
            RefreshStageOrder();
            UpdateStageInfo();
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (StageDataGrid.SelectedItem is not LaunchStage selected)
            {
                ShowMessage("请先选择一个阶段。");
                return;
            }

            int index = _stages.IndexOf(selected);

            if (index <= 0)
            {
                return;
            }

            _stages.Move(index, index - 1);
            RefreshStageOrder();
            StageDataGrid.SelectedItem = selected;
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (StageDataGrid.SelectedItem is not LaunchStage selected)
            {
                ShowMessage("请先选择一个阶段。");
                return;
            }

            int index = _stages.IndexOf(selected);

            if (index < 0 || index >= _stages.Count - 1)
            {
                return;
            }

            _stages.Move(index, index + 1);
            RefreshStageOrder();
            StageDataGrid.SelectedItem = selected;
        }

        private void RefreshStageOrder()
        {
            for (int i = 0; i < _stages.Count; i++)
            {
                _stages[i].Order = i + 1;
            }

            StageDataGrid.Items.Refresh();
            UpdateStageInfo();
        }

        private void UpdateStageInfo()
        {
            StageCountText.Text = $"共 {_stages.Count} 个阶段";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_stages.Count == 0)
            {
                MessageBoxResult result = MessageBox.Show(
                    this,
                    "当前方案没有配置任何阶段，仍然要保存吗？",
                    "确认保存",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            Stages = _stages.ToList();
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ShowMessage(string message)
        {
            MessageBox.Show(
                this,
                message,
                "提示",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
