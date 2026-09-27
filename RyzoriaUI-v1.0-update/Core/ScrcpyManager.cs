using System.Diagnostics;
using System.Text;

namespace RyzoriaUI.Core;

public sealed class ScrcpyManager
{
    public string ScrcpyPath { get; set; } = "scrcpy";
    private Process? _process;

    public bool IsRunning => _process is { HasExited: false };

    public bool Start(string serial, int maxSize, int fps, int bitrateMbps, bool fullscreen)
    {
        Stop();
        var args = new StringBuilder();
        args.Append($"-s \"{serial}\" --max-size={maxSize} --max-fps={fps} --video-bit-rate={bitrateMbps}M --no-audio");
        if (fullscreen) args.Append(" --fullscreen");
        var psi = new ProcessStartInfo
        {
            FileName = ScrcpyPath,
            Arguments = args.ToString(),
            UseShellExecute = false,
            CreateNoWindow = false
        };
        try { _process = Process.Start(psi); return _process != null; }
        catch { return false; }
    }

    public void Stop()
    {
        try { if (_process is { HasExited: false }) _process.Kill(true); } catch { }
        _process = null;
    }
}
