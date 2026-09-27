using System.Diagnostics;

namespace RyzoriaUI.Core;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut);

public static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(string fileName, IEnumerable<string> args, string? workingDirectory = null, IDictionary<string, string>? environment = null, int timeoutMs = 10000, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        if (environment is not null)
            foreach (var kv in environment) psi.Environment[kv.Key] = kv.Value;

        using var process = new Process { StartInfo = psi };
        try { process.Start(); }
        catch (Exception ex) { return new ProcessResult(-1, "", ex.Message, false); }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeoutMs);
        try
        {
            var outTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var errTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);
            return new ProcessResult(process.ExitCode, await outTask, await errTask, false);
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(true); } catch { }
            return new ProcessResult(-2, "", "Process timeout or cancellation.", true);
        }
    }
}
