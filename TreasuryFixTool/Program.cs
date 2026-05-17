using System;
using System.IO;
using System.Reflection;
using System.Windows;

namespace TreasuryFixTool;

/// <summary>
/// Program.cs — the sole entry point for TreasuryFixTool.
/// Handles /install and /silent-start flags, then delegates to WPF's App.Run().
/// </summary>
public static class Program
{
    /// <summary>Currently processed command-line arguments (for App/MainWindow to read).</summary>
    public static string[] CommandLineArgs { get; private set; } = Array.Empty<string>();

    [STAThread]
    public static void Main(string[] args)
    {
        CommandLineArgs = args;

        bool silent  = Exists(args, "/silent-start");
        bool install = Exists(args, "/install");

        if (install)
        {
            PerformInstall();
            return;
        }

        var app = new App();
        app.InitializeComponent();

        if (!silent)
        {
            app.MainWindow = new MainWindow();
            app.MainWindow.Show();
        }

        app.Run();
    }

    private static bool Exists(string[] args, string flag)
        => Array.Exists(args, a => a.Equals(flag, StringComparison.OrdinalIgnoreCase));

    /// <summary>Copies EXE to C:\TreasurySoftware\Deploy\ and creates data folders.</summary>
    private static void PerformInstall()
    {
        try
        {
            string exePath   = Path.Combine(AppContext.BaseDirectory, "TreasuryFixTool.exe");
            string deployDir = @"C:\TreasurySoftware\Deploy";
            string dest      = Path.Combine(deployDir, "TreasuryFixTool.exe");

            Directory.CreateDirectory(deployDir);
            if (File.Exists(exePath))
                File.Copy(exePath, dest, true);

            Directory.CreateDirectory(@"C:\TreasurySupport\Logs");
            Directory.CreateDirectory(@"C:\TreasurySupport\Escalations");
            Directory.CreateDirectory(@"C:\TreasurySupport\Config");
            Directory.CreateDirectory(@"C:\TreasurySupport\Recipes");

            MessageBox.Show(
                "Installation complete.\n\n" +
                $"EXE  : {dest}\n" +
                $"Data : C:\\TreasurySupport",
                "TreasuryFixTool",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Installation failed:\n{ex.Message}",
                "TreasuryFixTool", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
