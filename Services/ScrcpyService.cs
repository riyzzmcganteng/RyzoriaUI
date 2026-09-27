using RyzoriaUI.Core;
using RyzoriaUI.Models;

namespace RyzoriaUI.Services;

public sealed class ScrcpyService : IDisposable
{
    private readonly EmbeddedToolManager _tools;
    private Process? _process;
    public ScrcpyService(EmbeddedToolManager tools) => _tools = tools;
    public bool IsRunning => _process is { HasExited: false };

    public Task StartAsync(AndroidDevice device, PerformancePreset preset, bool fullscreen, bool turnOffScreen, bool noAudio)
    {
        Stop();
        var args = new List<string> { "--serial", device.Serial, "--max-size", preset.MaxSize.ToString(), "--max-fps", preset.MaxFps.ToString(), "--video-bit-rate", preset.BitrateMbps + "M", "--window-title", "RyzoriaUI • " + device.Model };
        if (fullscreen) args.Add("--fullscreen");
        if (turnOffScreen) args.Add("--turn-screen-off");
        if (noAudio) args.Add("--no-audio");
        var psi = new ProcessStartInfo(_tools.ScrcpyExe) { WorkingDirectory = _tools.ScrcpyDirectory, UseShellExecute = false, CreateNoWindow = false };
        foreach(var a in args) psi.ArgumentList.Add(a);
        psi.Environment["PATH"] = _tools.ScrcpyDirectory + ";" + _tools.AdbDirectory + ";" + (Environment.GetEnvironmentVariable("PATH") ?? "");
        _process = Process.Start(psi);
        if (_process is null) throw new InvalidOperationException("scrcpy failed to start.");
        return Task.CompletedTask;
    }
    public void Stop(){ try{ if(_process is {HasExited:false}) _process.Kill(true); }catch{} _process?.Dispose(); _process=null; }
    public void Dispose()=>Stop();
}
