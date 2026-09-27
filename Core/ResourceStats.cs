using System.Diagnostics;

namespace RyzoriaUI.Core;

public sealed class ResourceStats : IDisposable
{
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Process _process = Process.GetCurrentProcess();
    private TimeSpan _lastCpu;
    private DateTime _lastTime = DateTime.UtcNow;
    public double CpuPercent { get; private set; }
    public double AppMemoryMb => Process.GetCurrentProcess().WorkingSet64 / 1024d / 1024d;
    public double AvailableMemoryMb { get; private set; }
    public event Action? Updated;

    public ResourceStats()
    {
        _lastCpu = _process.TotalProcessorTime;
        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += (_, _) => Update();
        _timer.Start();
        Update();
    }

    private void Update()
    {
        try
        {
            var now = DateTime.UtcNow;
            var cpu = _process.TotalProcessorTime;
            var cpuDelta = (cpu - _lastCpu).TotalMilliseconds;
            var wall = (now - _lastTime).TotalMilliseconds;
            CpuPercent = wall <= 0 ? 0 : Math.Clamp(cpuDelta / wall / Environment.ProcessorCount * 100, 0, 100);
            _lastCpu = cpu; _lastTime = now;
            var info = GC.GetGCMemoryInfo();
            AvailableMemoryMb = Math.Max(0, info.TotalAvailableMemoryBytes / 1024d / 1024d);
            Updated?.Invoke();
        }
        catch { }
    }

    public void Dispose() => _timer.Dispose();
}
