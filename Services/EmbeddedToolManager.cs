using System.Reflection;
using RyzoriaUI.Core;

namespace RyzoriaUI.Services;

public sealed class EmbeddedToolManager : IDisposable
{
    private readonly string _root;
    private readonly Assembly _assembly = Assembly.GetExecutingAssembly();
    public string ToolRoot => _root;
    public string AdbDirectory => Path.Combine(_root, "adb");
    public string ScrcpyDirectory => Path.Combine(_root, "scrcpy");
    public string AdbExe => Path.Combine(AdbDirectory, "adb.exe");
    public string ScrcpyExe => Path.Combine(ScrcpyDirectory, "scrcpy.exe");

    public EmbeddedToolManager()
    {
        _root = Path.Combine(Path.GetTempPath(), "RyzoriaUI", Environment.ProcessId.ToString());
        Directory.CreateDirectory(AdbDirectory);
        Directory.CreateDirectory(ScrcpyDirectory);
        ExtractResources();
    }

    private void ExtractResources()
    {
        var names = _assembly.GetManifestResourceNames();
        foreach (var resource in names)
        {
            string? destination = null;
            if (resource.EndsWith(".adb.exe", StringComparison.OrdinalIgnoreCase)) destination = AdbExe;
            else if (resource.EndsWith(".AdbWinApi.dll", StringComparison.OrdinalIgnoreCase)) destination = Path.Combine(AdbDirectory, "AdbWinApi.dll");
            else if (resource.EndsWith(".AdbWinUsbApi.dll", StringComparison.OrdinalIgnoreCase)) destination = Path.Combine(AdbDirectory, "AdbWinUsbApi.dll");
            else if (resource.EndsWith(".scrcpy.exe", StringComparison.OrdinalIgnoreCase)) destination = ScrcpyExe;
            else if (resource.Contains(".EmbeddedTools.scrcpy.", StringComparison.OrdinalIgnoreCase))
            {
                var marker = ".EmbeddedTools.scrcpy.";
                var file = resource[(resource.IndexOf(marker, StringComparison.OrdinalIgnoreCase) + marker.Length)..];
                destination = Path.Combine(ScrcpyDirectory, file);
            }
            if (destination is null) continue;
            using var stream = _assembly.GetManifestResourceStream(resource);
            if (stream is null) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var output = File.Create(destination);
            stream.CopyTo(output);
        }
        Logger.Info($"Tools extracted to {_root}");
    }

    public bool HasAdb() => File.Exists(AdbExe);
    public bool HasScrcpy() => File.Exists(ScrcpyExe);

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }
}
