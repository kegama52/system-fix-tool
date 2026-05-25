using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TreasuryFixTool.Diagnostics;

namespace TreasuryFixTool.Controls
{
    public partial class EscalationItem : UserControl
    {
        public static readonly DependencyProperty CheckResultProperty =
            DependencyProperty.Register("CheckResult", typeof(CheckResult), typeof(EscalationItem), 
                new PropertyMetadata(null, OnCheckResultChanged));

        public CheckResult CheckResult
        {
            get => (CheckResult)GetValue(CheckResultProperty);
            set => SetValue(CheckResultProperty, value);
        }

        public EscalationItem()
        {
            InitializeComponent();
        }

        private static void OnCheckResultChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EscalationItem item && e.NewValue is CheckResult result)
            {
                item.ApplySeverityStyling(result.Status);
            }
        }

        private void ApplySeverityStyling(CheckStatus status)
        {
            if (LeftBorder == null) return;
            
            string severityColor = status switch
            {
                CheckStatus.Error => "#DC3545",
                CheckStatus.Critical => "#DC3545",
                CheckStatus.Warning => "#FD7E14",
                CheckStatus.Info => "#0097A7",
                CheckStatus.Ok => "#4CAF50",
                CheckStatus.Healthy => "#4CAF50",
                _ => "#6C757D"
            };

            LeftBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(severityColor));
        }
    }
}