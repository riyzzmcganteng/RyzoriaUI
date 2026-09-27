using RyzoriaUI.Models;

namespace RyzoriaUI.Services;

public sealed class MapperService
{
    public IReadOnlyDictionary<string,string> KeyEventMap { get; } = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
    { ["UP"]="19",["DOWN"]="20",["LEFT"]="21",["RIGHT"]="22",["SPACE"]="62",["ENTER"]="66",["ESC"]="111" };
    public async Task<bool> SendBindingAsync(AdbService adb,string serial,string binding)
    {
        if(KeyEventMap.TryGetValue(binding,out var code)) return (await adb.ShellAsync(serial,"input","keyevent",code)).ExitCode==0;
        if(binding.StartsWith("TAP:",StringComparison.OrdinalIgnoreCase))
        {
            var p=binding.Split(':'); if(p.Length==3 && int.TryParse(p[1],out var x)&&int.TryParse(p[2],out var y)) return (await adb.ShellAsync(serial,"input","tap",x.ToString(),y.ToString())).ExitCode==0;
        }
        return false;
    }
}
