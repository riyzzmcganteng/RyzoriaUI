namespace RyzoriaUI.Models;

public sealed record PerformancePreset(string Name, int MaxSize, int MaxFps, int BitrateMbps, bool NoAudio);
