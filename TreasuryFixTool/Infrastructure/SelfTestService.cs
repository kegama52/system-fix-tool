using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using TreasuryFixTool.Infrastructure.Logging;

namespace TreasuryFixTool.Infrastructure;

/// <summary>
/// Represents the result of a shell / PowerShell command execution.
/// </summary>
public sealed class ShellResult
{
    public int    ExitCode       { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError  { get; init; } = string.Empty;

    public bool Success => ExitCode == 0;
}

/// <summary>
/// Executes arbitrary shell commands and PowerShell snippets asynchronously,
/// capturing stdout, stderr, and exit code.
/// </summary>
public static class ShellExecutor
{
    private const int DefaultTimeoutMs = 60_000;

    public static async Task<ShellResult> RunCmdAsync(
        string command,
        int timeoutMs = DefaultTimeoutMs)
    {
        return await ExecuteAsync("cmd.exe", $"/c {command}", timeoutMs);
    }

    public static async Task<ShellResult> RunPowerShellAsync(
        string command,
        int timeoutMs = DefaultTimeoutMs)
    {
        return await ExecuteAsync(
            "powershell.exe",
            $"-NoProfile -NonInteractive -Command \"{command}\"",
            timeoutMs);
    }

    private static async Task<ShellResult> ExecuteAsync(
        string fileName,
        string arguments,
        int timeoutMs)
    {
        var tcs       = new TaskCompletionSource<ShellResult>();
        var process   = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName               = fileName,
                Arguments              = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            },
            EnableRaisingEvents = true,
        };

        var output = new StringBuilder();
        var error  = new StringBuilder();

        process.OutputDataReceived += (s, e) =>
        {
            if (e.Data != null) output.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (s, e) =>
        {
            if (e.Data != null) error.AppendLine(e.Data);
        };

        process.Exited += (s, e) =>
        {
            var result = new ShellResult
            {
                ExitCode       = process.ExitCode,
                StandardOutput = output.ToString().TrimEnd(),
                StandardError  = error.ToString().TrimEnd(),
            };
            process.Dispose();
            tcs.TrySetResult(result);
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            process.Dispose();
            return new ShellResult
            {
                ExitCode       = -1,
                StandardError  = ex.Message,
            };
        }

        var timeoutTask = Task.Delay(timeoutMs);
        var completed    = await Task.WhenAny(tcs.Task, timeoutTask);

        if (completed == timeoutTask)
        {
            try { process.Kill(true); } catch { /* ignore */ }
            process.Dispose();
            return new ShellResult
            {
                ExitCode       = -1,
                StandardError  = $"Command timed out after {timeoutMs} ms.",
            };
        }

        return await tcs.Task;
    }
}

/// <summary>
/// Runs a battery of self-test shell commands against the local machine and
/// streams human-readable progress lines back through a callback.
/// </summary>
public class SelfTestService
{
    private readonly FileLogger _logger;

    public SelfTestService(FileLogger logger) => _logger = logger;

    public async Task RunSelfTestsAsync(Action<string> progressReporter)
    {
        var tests = new Dictionary<string, Func<Task<ShellResult>>>
        {
            ["Network Loopback"]       = () => ShellExecutor.RunCmdAsync("ping -n 2 127.0.0.1"),
            ["DNS Resolution"]         = () => ShellExecutor.RunCmdAsync("nslookup 1.1.1.1"),
            ["Windows Update Service"] = () => ShellExecutor.RunPowerShellAsync("Get-Service wuauserv | Select-Object Status, Name"),
            ["Disk Status"]            = () => ShellExecutor.RunCmdAsync("wmic diskdrive get status"),
            ["TCP/IP Reset Check"]     = () => ShellExecutor.RunCmdAsync("netsh interface ipv4 show interfaces"),
        };

        foreach (var test in tests)
        {
            progressReporter($"Running: {test.Key}...");
            var result = await test.Value();

            if (result.Success)
                _logger.Info($"SelfTest: {test.Key} | ExitCode={result.ExitCode}");
            else
                _logger.Error($"SelfTest: {test.Key} | ExitCode={result.ExitCode} | Error={result.StandardError}");

            progressReporter(result.Success
                ? $"{test.Key}: Passed"
                : $"{test.Key}: Failed (Code {result.ExitCode})");

            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
                progressReporter($"Output: {Truncate(result.StandardOutput, 150)}");
        }

        progressReporter("Self-tests complete.");
    }

    public async Task RunAutoResolveAsync(
        string             fixCommand,
        Action<string>     progressReporter,
        int                timeoutMs = 120_000)
    {
        progressReporter($"Executing fix: {fixCommand}...");
        var result = await ShellExecutor.RunCmdAsync(fixCommand, timeoutMs);

        _logger.Info($"AutoResolve: {fixCommand} | ExitCode={result.ExitCode}");

        progressReporter(result.Success
            ? "Fix applied successfully."
            : $"Fix failed: {result.StandardError}");
    }

    private static string Truncate(string value, int maxChars) =>
        value.Length <= maxChars ? value : value[..maxChars] + "...";
}
