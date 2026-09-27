using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RyzoriaUI.Core;

namespace RyzoriaUI.Services;

public sealed class FileTransferService
{
    private readonly EmbeddedToolManager _tools;

    public FileTransferService(EmbeddedToolManager tools)
    {
        _tools = tools;
    }

    private Dictionary<string, string> GetEnvironment()
    {
        var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        return new Dictionary<string, string>
        {
            ["PATH"] = _tools.AdbDirectory + ";" + currentPath
        };
    }

    public async Task<string> ListAsync(
        string serial,
        string remote = "/sdcard")
    {
        var result = await ProcessRunner.RunAsync(
            _tools.AdbExe,
            new[]
            {
                "-s",
                serial,
                "shell",
                "ls",
                "-la",
                remote
            },
            _tools.AdbDirectory,
            GetEnvironment(),
            10000
        );

        return string.IsNullOrWhiteSpace(result.StandardOutput)
            ? result.StandardError
            : result.StandardOutput;
    }

    public async Task<bool> PullAsync(
        string serial,
        string remote,
        string local,
        CancellationToken ct = default)
    {
        var result = await ProcessRunner.RunAsync(
            _tools.AdbExe,
            new[]
            {
                "-s",
                serial,
                "pull",
                remote,
                local
            },
            _tools.AdbDirectory,
            GetEnvironment(),
            60000,
            ct
        );

        return result.ExitCode == 0;
    }

    public async Task<bool> PushAsync(
        string serial,
        string local,
        string remote,
        CancellationToken ct = default)
    {
        var result = await ProcessRunner.RunAsync(
            _tools.AdbExe,
            new[]
            {
                "-s",
                serial,
                "push",
                local,
                remote
            },
            _tools.AdbDirectory,
            GetEnvironment(),
            60000,
            ct
        );

        return result.ExitCode == 0;
    }

    public async Task<bool> ShellAsync(
        string serial,
        params string[] args)
    {
        var command = new[]
        {
            "-s",
            serial,
            "shell"
        }.Concat(args);

        var result = await ProcessRunner.RunAsync(
            _tools.AdbExe,
            command,
            _tools.AdbDirectory,
            GetEnvironment(),
            15000
        );

        return result.ExitCode == 0;
    }
}