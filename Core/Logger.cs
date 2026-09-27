using RyzoriaUI.Config;

namespace RyzoriaUI.Core;

public static class Logger
{
    private static readonly object Gate = new();
    private static string FilePath => Path.Combine(SettingsStore.LogDirectory, "ryzoria.log");

    public static void Info(string message) => Write("INFO", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(SettingsStore.LogDirectory);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > 5 * 1024 * 1024)
                    File.Delete(FilePath);
                File.AppendAllText(FilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {level} {message}{Environment.NewLine}");
            }
        }
        catch { }
    }
}
