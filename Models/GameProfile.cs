namespace RyzoriaUI.Models;

public sealed class GameProfile
{
    public string Name { get; set; } = "New Game";
    public string LaunchPath { get; set; } = "";
    public string ScrcpyPreset { get; set; } = "Ultra Low";
    public bool Fullscreen { get; set; }
}
