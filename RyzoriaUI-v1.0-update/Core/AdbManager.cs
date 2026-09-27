using System.Diagnostics;
using System.Text;

namespace RyzoriaUI.Core;

public sealed record AndroidDevice(string Serial, string State);

public sealed class AdbManager
{
    public string AdbPath { get; set; } = "adb";

    private async Task<(int Code, string Output)> RunAsync(string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = AdbPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        using var p = new Process { StartInfo = psi };
        var sb = new StringBuilder();
        p.OutputDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
        try { p.Start(); } catch (Exception ex) { return (-1, ex.Message); }
        p.BeginOutputReadLine(); p.BeginErrorReadLine();
        await p.WaitForExitAsync();
        return (p.ExitCode, sb.ToString().Trim());
    }

    public async Task<bool> IsAvailableAsync() => (await RunAsync("version")).Code == 0;

    public async Task<List<AndroidDevice>> GetDevicesAsync()
    {
        var r = await RunAsync("devices");
        var list = new List<AndroidDevice>();
        foreach (var line in r.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var t = line.Trim();
            if (t.StartsWith("List of devices") || t.Contains("daemon")) continue;
            var parts = t.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) list.Add(new AndroidDevice(parts[0], parts[1]));
        }
        return list;
    }

    public async Task<string> ShellAsync(string serial, string command)
    {
        var r = await RunAsync($"-s \"{serial}\" shell {command}");
        return r.Output;
    }

    public Task<string> GetPropAsync(string serial, string prop) => ShellAsync(serial, $"getprop {prop}");
    public Task<string> GetBatteryAsync(string serial) => ShellAsync(serial, "dumpsys battery | grep level");
    public Task<string> GetResolutionAsync(string serial) => ShellAsync(serial, "wm size");

    public Task<string> LockScreenAsync(string serial) => ShellAsync(serial, "input keyevent 26");
    public Task<string> HomeAsync(string serial) => ShellAsync(serial, "input keyevent 3");
    public Task<string> BackAsync(string serial) => ShellAsync(serial, "input keyevent 4");
    public Task<string> RecentAsync(string serial) => ShellAsync(serial, "input keyevent 187");
    public Task<string> VolumeUpAsync(string serial) => ShellAsync(serial, "input keyevent 24");
    public Task<string> VolumeDownAsync(string serial) => ShellAsync(serial, "input keyevent 25");
    public Task<string> WakeAsync(string serial) => ShellAsync(serial, "input keyevent 224");
}
