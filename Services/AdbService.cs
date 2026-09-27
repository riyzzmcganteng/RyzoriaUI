using System.Text.RegularExpressions;
using RyzoriaUI.Core;
using RyzoriaUI.Models;

namespace RyzoriaUI.Services;

public sealed class AdbService
{
    private readonly EmbeddedToolManager _tools;
    public AdbService(EmbeddedToolManager tools) => _tools = tools;

    private Task<ProcessResult> RunAsync(IEnumerable<string> args, int timeout = 10000, CancellationToken ct = default)
    {
        var env = new Dictionary<string, string> { ["PATH"] = _tools.AdbDirectory + ";" + (Environment.GetEnvironmentVariable("PATH") ?? "") };
        return ProcessRunner.RunAsync(_tools.AdbExe, args, _tools.AdbDirectory, env, timeout, ct);
    }

    public async Task<string> VersionAsync(CancellationToken ct = default)
    {
        if (!_tools.HasAdb()) return "ADB not embedded";
        var r = await RunAsync(["version"], ct: ct);
        return string.IsNullOrWhiteSpace(r.StandardOutput) ? r.StandardError.Trim() : r.StandardOutput.Trim();
    }

    public async Task<IReadOnlyList<AndroidDevice>> GetDevicesAsync(CancellationToken ct = default)
    {
        if (!_tools.HasAdb()) return Array.Empty<AndroidDevice>();
        var r = await RunAsync(["devices", "-l"], ct: ct);
        var result = new List<AndroidDevice>();
        foreach (var raw in r.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase)) continue;
            var m = Regex.Match(line, @"^(\S+)\s+(\S+)(.*)$");
            if (!m.Success) continue;
            var serial = m.Groups[1].Value;
            var state = m.Groups[2].Value;
            var rest = m.Groups[3].Value;
            var model = Kvp(rest, "model");
            var code = Kvp(rest, "device");
            var authorized = state.Equals("device", StringComparison.OrdinalIgnoreCase);
            if (!authorized)
            {
                result.Add(new AndroidDevice(serial, state, "", model, code, "", "", "", "", "", "", "", DetectConnection(serial), false));
                continue;
            }
            var props = await PropertiesAsync(serial, ct);
            result.Add(new AndroidDevice(
                serial, state,
                props.GetValueOrDefault("manufacturer", "Unknown"),
                string.IsNullOrWhiteSpace(model) ? props.GetValueOrDefault("model", "Unknown") : model,
                string.IsNullOrWhiteSpace(code) ? props.GetValueOrDefault("device", "Unknown") : code,
                props.GetValueOrDefault("android", "Unknown"),
                props.GetValueOrDefault("sdk", "Unknown"),
                await BatteryAsync(serial, ct),
                await MemoryAsync(serial, ct),
                await StorageAsync(serial, ct),
                props.GetValueOrDefault("resolution", "Unknown"),
                props.GetValueOrDefault("dpi", "Unknown"),
                DetectConnection(serial), true));
        }
        return result;
    }

    private async Task<Dictionary<string,string>> PropertiesAsync(string serial, CancellationToken ct)
    {
        var r = await RunAsync(["-s", serial, "shell", "getprop"], ct: ct);
        var map = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in r.StandardOutput.Split('\n'))
        {
            var m = Regex.Match(raw, @"\[(?<k>[^\]]+)\]: \[(?<v>[^\]]*)\]");
            if (!m.Success) continue;
            switch (m.Groups["k"].Value)
            {
                case "ro.product.manufacturer": map["manufacturer"] = m.Groups["v"].Value; break;
                case "ro.product.model": map["model"] = m.Groups["v"].Value; break;
                case "ro.product.device": map["device"] = m.Groups["v"].Value; break;
                case "ro.build.version.release": map["android"] = m.Groups["v"].Value; break;
                case "ro.build.version.sdk": map["sdk"] = m.Groups["v"].Value; break;
                case "ro.sf.lcd_density": map["dpi"] = m.Groups["v"].Value; break;
            }
        }
        var size = await RunAsync(["-s", serial, "shell", "wm", "size"], ct: ct);
        map["resolution"] = ParseSize(size.StandardOutput);
        return map;
    }

    private async Task<string> BatteryAsync(string serial, CancellationToken ct) { var r = await RunAsync(["-s",serial,"shell","dumpsys","battery"], ct: ct); var m = Regex.Match(r.StandardOutput,@"level:\s*(\d+)"); return m.Success ? m.Groups[1].Value+"%" : "Unknown"; }
    private async Task<string> MemoryAsync(string serial, CancellationToken ct) { var r = await RunAsync(["-s",serial,"shell","cat","/proc/meminfo"], ct: ct); var m=Regex.Match(r.StandardOutput,@"MemTotal:\s*(\d+)"); return m.Success ? (long.Parse(m.Groups[1].Value)/1024)+" MB" : "Unknown"; }
    private async Task<string> StorageAsync(string serial, CancellationToken ct) { var r=await RunAsync(["-s",serial,"shell","df","-h","/data"], ct: ct); var line=r.StandardOutput.Split('\n').LastOrDefault(x=>x.Contains("/data")); return string.IsNullOrWhiteSpace(line)?"Unknown":line.Trim(); }
    private static string ParseSize(string output) => output.Split('\n').Reverse().Select(x=>x.Trim()).FirstOrDefault(x=>x.Contains("size:",StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(x,@"\d+x\d+"))?.Split(':').Last().Trim() ?? "Unknown";
    private static string Kvp(string text,string key) { var m=Regex.Match(text,$@"\b{Regex.Escape(key)}:(\S+)"); return m.Success?m.Groups[1].Value.Replace('_',' '):""; }
    private static string DetectConnection(string serial) => serial.Contains(':') ? "Wireless" : "USB";

    public Task ShellAsync(string serial, params string[] args) => RunAsync(new[]{"-s",serial,"shell"}.Concat(args), 12000);
    private Task KeyAsync(string serial, string key) => ShellAsync(serial, "input", "keyevent", key);
    public Task HomeAsync(string s)=>KeyAsync(s,"3"); public Task BackAsync(string s)=>KeyAsync(s,"4"); public Task RecentAsync(string s)=>KeyAsync(s,"187");
    public Task VolumeUpAsync(string s)=>KeyAsync(s,"24"); public Task VolumeDownAsync(string s)=>KeyAsync(s,"25"); public Task MuteAsync(string s)=>KeyAsync(s,"164"); public Task WakeAsync(string s)=>KeyAsync(s,"224"); public Task SleepAsync(string s)=>KeyAsync(s,"223");
    public async Task<bool> ScreenshotAsync(string serial,string destination,CancellationToken ct=default)
    {
        var temp = Path.Combine(Path.GetTempPath(), "RyzoriaUI", Environment.ProcessId.ToString(), "shot.png");
        Directory.CreateDirectory(Path.GetDirectoryName(temp)!);
        var r = await RunAsync(["-s", serial, "shell", "screencap", "-p", "/sdcard/ryzoriaui-shot.png"], 15000, ct);
        if (r.ExitCode != 0) return false;
        var pulled = await PullAsync(serial, "/sdcard/ryzoriaui-shot.png", temp, ct);
        try { await RunAsync(["-s", serial, "shell", "rm", "/sdcard/ryzoriaui-shot.png"], 5000, ct); } catch { }
        if (!pulled || !File.Exists(temp)) return false;
        File.Copy(temp, destination, true);
        try { File.Delete(temp); } catch { }
        return true;
    }
    public async Task<bool> PushAsync(string serial,string local,string remote,CancellationToken ct=default)=> (await RunAsync(["-s",serial,"push",local,remote],60000,ct)).ExitCode==0;
    public async Task<bool> PullAsync(string serial,string remote,string local,CancellationToken ct=default)=> (await RunAsync(["-s",serial,"pull",remote,local],60000,ct)).ExitCode==0;
    public async Task<bool> RebootAsync(string serial,CancellationToken ct=default)=> (await RunAsync(["-s",serial,"reboot"],15000,ct)).ExitCode==0;
}
