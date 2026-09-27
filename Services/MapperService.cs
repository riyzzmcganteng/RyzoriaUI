using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RyzoriaUI.Services;

namespace RyzoriaUI.Services;

public sealed class MapperService
{
    public IReadOnlyDictionary<string, string> KeyEventMap { get; } =
        new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["UP"] = "19",
            ["DOWN"] = "20",
            ["LEFT"] = "21",
            ["RIGHT"] = "22",
            ["SPACE"] = "62",
            ["ENTER"] = "66",
            ["ESC"] = "111"
        };

    public async Task<bool> SendBindingAsync(
        AdbService adb,
        string serial,
        string binding)
    {
        if (string.IsNullOrWhiteSpace(binding))
            return false;

        if (KeyEventMap.TryGetValue(
                binding.Trim(),
                out var code))
        {
            await adb.ShellAsync(
                serial,
                "input",
                "keyevent",
                code);

            return true;
        }

        if (binding.StartsWith(
                "TAP:",
                StringComparison.OrdinalIgnoreCase))
        {
            var parts = binding.Split(
                ':',
                StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 3 &&
                int.TryParse(parts[1], out var x) &&
                int.TryParse(parts[2], out var y))
            {
                await adb.ShellAsync(
                    serial,
                    "input",
                    "tap",
                    x.ToString(),
                    y.ToString());

                return true;
            }
        }

        return false;
    }
}