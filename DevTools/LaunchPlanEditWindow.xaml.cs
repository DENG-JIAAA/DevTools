
using System;
using System.Windows;
using DevTools.Models;

namespace DevTools
{
    public partial class LaunchPlanEditWindow : Window
    {
        public LaunchPlan? Plan { get; private set; }

        private readonly LaunchPlan? _existingPlan;

        public LaunchPlanEditWindow(LaunchPlan? plan = null)
        {
            InitializeComponent();

            _existingPlan = plan;

            if (plan != null)
            {
                WindowTitleText.Text = "编辑启动方案";
                NameTextBox.Text = plan.Name;
                DescriptionTextBox.Text = plan.Description;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string name = NameTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(
                    this,
                    "请输入启动方案名称。",
                    "配置检查",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                NameTextBox.Focus();
                return;
            }

            if (_existingPlan != null)
            {
                Plan = new LaunchPlan
                {
                    Id = _existingPlan.Id,
                    Name = name,
                    Description = DescriptionTextBox.Text.Trim(),
                    Stages = _existingPlan.Stages,
                    CreatedAt = _existingPlan.CreatedAt,
                    UpdatedAt = _existingPlan.UpdatedAt
                };
            }
            else
            {
                Plan = new LaunchPlan
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    Description = DescriptionTextBox.Text.Trim()
                };
            }

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
