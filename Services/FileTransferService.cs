using RyzoriaUI.Core;

namespace RyzoriaUI.Services;

public sealed class FileTransferService
{
    private readonly EmbeddedToolManager _tools;
    public FileTransferService(EmbeddedToolManager tools)=>_tools=tools;
    public async Task<string> ListAsync(string serial,string remote="/sdcard")
    {
        var r=await ProcessRunner.RunAsync(_tools.AdbExe,["-s",serial,"shell","ls","-la",remote],_tools.AdbDirectory,new(){["PATH"]=_tools.AdbDirectory+";"+Environment.GetEnvironmentVariable("PATH")},10000);
        return string.IsNullOrWhiteSpace(r.StandardOutput)?r.StandardError:r.StandardOutput;
    }
    public async Task<bool> PullAsync(string serial,string remote,string local,CancellationToken ct=default)
    {
        var r=await ProcessRunner.RunAsync(_tools.AdbExe,["-s",serial,"pull",remote,local],_tools.AdbDirectory,new(){["PATH"]=_tools.AdbDirectory+";"+Environment.GetEnvironmentVariable("PATH")},60000,ct); return r.ExitCode==0;
    }
    public async Task<bool> PushAsync(string serial,string local,string remote,CancellationToken ct=default)
    {
        var r=await ProcessRunner.RunAsync(_tools.AdbExe,["-s",serial,"push",local,remote],_tools.AdbDirectory,new(){["PATH"]=_tools.AdbDirectory+";"+Environment.GetEnvironmentVariable("PATH")},60000,ct); return r.ExitCode==0;
    }
    public async Task<bool> ShellAsync(string serial,params string[] args){var r=await ProcessRunner.RunAsync(_tools.AdbExe,new[]{"-s",serial,"shell"}.Concat(args),_tools.AdbDirectory,new(){["PATH"]=_tools.AdbDirectory+";"+Environment.GetEnvironmentVariable("PATH")},15000);return r.ExitCode==0;}
}
