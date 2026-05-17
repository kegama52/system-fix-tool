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
        Program.ProcessArgs(e.Args);
        base.OnStartup(e);
    }
}
