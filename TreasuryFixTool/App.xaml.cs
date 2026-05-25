using System;
using System.Windows;

namespace TreasuryFixTool;

/// <summary>
/// App.xaml.cs — WPF Application partial class.
/// The [STAThread] entry point is auto-generated in App.g.cs from App.xaml.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            Program.ProcessArgs(e.Args);
            
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Application failed to start: {ex.Message}\n\n{ex.StackTrace}", 
                "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        
        base.OnStartup(e);
    }
}
