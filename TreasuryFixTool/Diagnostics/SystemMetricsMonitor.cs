using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace TreasuryFixTool.Diagnostics
{
    public sealed class SystemMetricsMonitor : IDisposable
    {
        private readonly DispatcherTimer _timer;
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _ramCounter;
        private readonly Canvas _cpuCanvas;
        private readonly Canvas _ramCanvas;
        private readonly TextBlock _cpuText;
        private readonly TextBlock _ramText;
        
        private readonly double[] _cpuHistory;
        private readonly double[] _ramHistory;
        private int _historyIndex;
        private int _historyCount;
        private readonly object _lock = new();
        private bool _disposed;

        private const int MaxDataPoints = 60;
        private const double DefaultTotalRamGb = 16.0;

        public SystemMetricsMonitor(Canvas cpuCanvas, Canvas ramCanvas, TextBlock cpuText, TextBlock ramText)
        {
            _cpuCanvas = cpuCanvas ?? throw new ArgumentNullException(nameof(cpuCanvas));
            _ramCanvas = ramCanvas ?? throw new ArgumentNullException(nameof(ramCanvas));
            _cpuText = cpuText ?? throw new ArgumentNullException(nameof(cpuText));
            _ramText = ramText ?? throw new ArgumentNullException(nameof(ramText));

            _cpuHistory = new double[MaxDataPoints];
            _ramHistory = new double[MaxDataPoints];

            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _ramCounter = new PerformanceCounter("Memory", "Available MBytes");

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += OnTimerTick;
            _timer.Start();
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            try
            {
                UpdateMetrics();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to update metrics: {ex.Message}");
            }
        }

        private void UpdateMetrics()
        {
            float cpuValue = _cpuCounter.NextValue();
            float availableMBytes = _ramCounter.NextValue();

            double totalRamGb = GetTotalPhysicalMemoryGb();
            double usedRamGb = Math.Max(0.0, totalRamGb - (availableMBytes / 1024.0));
            double ramPercentage = totalRamGb > 0 ? (usedRamGb / totalRamGb) * 100.0 : 0.0;

            _cpuText.Text = $"CPU: {cpuValue:0.0}%";
            _ramText.Text = $"RAM: {usedRamGb:0.1} GB / {totalRamGb:0.0} GB";

            lock (_lock)
            {
                _cpuHistory[_historyIndex] = cpuValue;
                _ramHistory[_historyIndex] = ramPercentage;
                _historyIndex = (_historyIndex + 1) % MaxDataPoints;
                _historyCount = Math.Min(_historyCount + 1, MaxDataPoints);

                RenderGraph(_cpuCanvas, _cpuHistory, _historyCount, _historyIndex, Colors.ForestGreen);
                RenderGraph(_ramCanvas, _ramHistory, _historyCount, _historyIndex, Colors.DodgerBlue);
            }
        }

        private static double GetTotalPhysicalMemoryGb()
        {
            try
            {
                const string typeName = "Microsoft.VisualBasic.Devices.ComputerInfo, Microsoft.VisualBasic";
                var computerInfoType = Type.GetType(typeName);
                if (computerInfoType == null)
                    return DefaultTotalRamGb;

                var instance = Activator.CreateInstance(computerInfoType);
                var property = computerInfoType.GetProperty("TotalPhysicalMemory");
                if (instance == null || property == null)
                    return DefaultTotalRamGb;

                var value = property.GetValue(instance);
                if (value is ulong totalPhysicalMemory)
                {
                    return Math.Round(totalPhysicalMemory / (1024.0 * 1024.0 * 1024.0), 1);
                }
            }
            catch
            {
                // Fall through to default
            }
            return DefaultTotalRamGb;
        }

        private static void RenderGraph(Canvas canvas, double[] history, int count, int nextIndex, Color lineColor)
        {
            if (canvas.ActualWidth <= 0 || canvas.ActualHeight <= 0 || count < 2)
                return;

            double canvasHeight = canvas.ActualHeight;
            double widthStep = canvas.ActualWidth / Math.Max(1, count - 1);
            var orderedHistory = GetOrderedHistory(history, count, nextIndex);

            var polyline = new Polyline
            {
                Stroke = new SolidColorBrush(lineColor),
                StrokeThickness = 2
            };

            for (int i = 0; i < orderedHistory.Length; i++)
            {
                double normalizedValue = Math.Max(0.0, Math.Min(1.0, orderedHistory[i] / 100.0));
                double xPos = i * widthStep;
                double yPos = canvasHeight - (normalizedValue * canvasHeight);
                polyline.Points.Add(new Point(xPos, Math.Max(0, Math.Min(canvasHeight, yPos))));
            }

            canvas.Children.Clear();
            canvas.Children.Add(polyline);
        }

        private static double[] GetOrderedHistory(double[] history, int count, int nextIndex)
        {
            if (count <= 0)
                return Array.Empty<double>();

            if (count < history.Length)
            {
                var sliced = new double[count];
                Array.Copy(history, 0, sliced, 0, count);
                return sliced;
            }

            int start = nextIndex;
            if (start < 0 || start >= count)
                start = 0;

            var orderedHistory = new double[count];
            Array.Copy(history, start, orderedHistory, 0, count - start);
            Array.Copy(history, 0, orderedHistory, count - start, start);
            return orderedHistory;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _timer?.Stop();
            _cpuCounter?.Dispose();
            _ramCounter?.Dispose();
        }
    }
}