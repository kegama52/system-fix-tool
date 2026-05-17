using System;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Media.Effects;
using System.Windows.Documents;

namespace TreasuryFixTool.Notifications
{
    public enum ToastIcon
    {
        Info,
        Warning,
        Error
    }

    public static class ToastManager
    {
        private const string ToolTipTitle = "TreasuryFixTool";

        public static void ShowToast(string title, string message, int timeoutMs, ToastIcon icon)
        {
            var thread = new Thread(() =>
            {
                try
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        var win = Application.Current?.MainWindow;
                        if (win == null) return;

                        // Get the adorner layer for the window
                        var layer = AdornerLayer.GetAdornerLayer(win);
                        if (layer == null) return;

                        var banner = new ToastBanner(title, message, icon, win);
                        layer.Add(banner);

                        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(timeoutMs) };
                        timer.Tick += (_, __) => { timer.Stop(); layer.Remove(banner); };
                        timer.Start();
                    });
                }
                catch { /* silent */ }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private class ToastBanner : Adorner
        {
            private readonly Border _border;

            public ToastBanner(string title, string message, ToastIcon icon, UIElement adornedElement)
                : base(adornedElement)
            {
                _border = new Border
                {
                    Background = icon switch
                    {
                        ToastIcon.Warning => Brushes.DarkOrange,
                        ToastIcon.Error => Brushes.Red,
                        _ => Brushes.DarkBlue
                    },
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(12),
                    Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 3, Opacity = 0.5 }
                };

                var stack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 0, 0, 4)
                };

                var iconText = new TextBlock
                {
                    Text = icon switch
                    {
                        ToastIcon.Warning => "⚠",
                        ToastIcon.Error => "✖",
                        _ => "ℹ"
                    },
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 6, 0)
                };

                var textPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical
                };

                textPanel.Children.Add(new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                });

                textPanel.Children.Add(new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 300,
                    Foreground = Brushes.White
                });

                stack.Children.Add(iconText);
                stack.Children.Add(textPanel);
                _border.Child = stack;
            }

            protected override int VisualChildrenCount => 1;

            protected override Visual GetVisualChild(int index) => _border;

            protected override Size MeasureOverride(Size constraint)
            {
                _border.Measure(constraint);
                return _border.DesiredSize;
            }

            protected override Size ArrangeOverride(Size finalSize)
            {
                // Position at bottom-right of the adorned element with 20px margin
                double x = finalSize.Width - _border.DesiredSize.Width - 20;
                double y = finalSize.Height - _border.DesiredSize.Height - 20;
                _border.Arrange(new Rect(x, y, _border.DesiredSize.Width, _border.DesiredSize.Height));
                return finalSize;
            }
        }
    }
}