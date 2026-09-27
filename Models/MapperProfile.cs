namespace RyzoriaUI.Models;

public sealed class MapperProfile
{
    public string Name { get; set; } = "Custom";
    public Dictionary<string, string> Bindings { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["W"] = "UP", ["A"] = "LEFT", ["S"] = "DOWN", ["D"] = "RIGHT",
        ["SPACE"] = "SPACE", ["LMB"] = "TAP:500:500"
    };
}
