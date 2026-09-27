namespace RyzoriaUI.Services;

public sealed class DiagnosticsService
{
    private readonly EmbeddedToolManager _tools; private readonly AdbService _adb;
    public DiagnosticsService(EmbeddedToolManager tools,AdbService adb){_tools=tools;_adb=adb;}
    public async Task<Dictionary<string,string>> RunAsync()
    {
        var map=new Dictionary<string,string>();
        map["Windows x64"] = Environment.Is64BitOperatingSystem ? "✓" : "✗ 32-bit Windows";
        map["Required Files"] = _tools.HasAdb() && _tools.HasScrcpy() ? "✓" : "✗ missing embedded tools";
        map["ADB"] = _tools.HasAdb() ? "✓" : "✗";
        map["scrcpy"] = _tools.HasScrcpy() ? "✓" : "✗";
        try { var v=await _adb.VersionAsync(); map["ADB Engine"] = v.StartsWith("Android Debug Bridge",StringComparison.OrdinalIgnoreCase)?"✓":"✗"; }
        catch(Exception ex){map["ADB Engine"]="✗ "+ex.Message;}
        try { var ds=await _adb.GetDevicesAsync(); map["Android Device"] = ds.Count>0?"✓ "+ds.Count+" connected":"! none connected"; map["ADB Authorization"]=ds.Any(x=>x.Authorized)?"✓":"! no authorized device"; map["USB/Wireless"] = ds.Count>0?string.Join(", ",ds.Select(x=>x.ConnectionType)):"!"; }
        catch(Exception ex){map["Android Device"]="✗ "+ex.Message;}
        return map;
    }
}
