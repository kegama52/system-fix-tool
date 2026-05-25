using System;
using System.IO;
using System.Windows;

namespace TreasuryFixTool;

/// <summary>
/// Program.cs — handles /install flag at startup window.
/// No [STAThread] Main() here — that belongs in App.g.cs (generated from App.xaml).
/// </summary>
public static class Program
{
    /// <summary>Command-line args from application launch (read by MainWindow, TrayManager).</summary>
    public static string[] CommandLineArgs { get; private set; } = Array.Empty<string>();

    /// <summary>True when the app was launched with /silent-start.</summary>
    public static bool SilentStart { get; private set; }

    /// <summary>Entries that forward to App's Main (called by App Startup event).</summary>
    /// <summary>
    /// Initialises command-line flags before App.Run().
    /// Called from App.xaml Startup handler.
    /// </summary>
    public static void ProcessArgs(string[] args)
    {
        CommandLineArgs = args;
        SilentStart     = Array.Exists(args, a => a.Equals("/silent-start", StringComparison.OrdinalIgnoreCase));
    }
}