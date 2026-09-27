# RyzoriaUI v1.7
Android Desktop Experience by Riyzz

Native C#/.NET 8 WinForms shell designed around low-end Windows hardware such as Intel Celeron N2040 + 4 GB RAM.

## Final distribution
The GitHub Actions workflow produces exactly one end-user file:

`RyzoriaUI.exe`

The executable is self-contained and bundles Android Platform-Tools ADB plus scrcpy at build time. At runtime, native bundled tools may be extracted to a temporary session folder and cleaned up later; no dependency folder is required by the user.

## Included
- Windows-style desktop shell, taskbar, search and lightweight internal window/page manager
- Android Hub, device auto-detection and ADB controls
- scrcpy presets optimized for low-end PCs
- Android file listing and PC↔Android push/pull
- Gaming Center
- Control Mapper profiles with ADB key/tap test actions
- Performance Manager and low-end presets
- Diagnostics Center
- Developer Console with whitelisted commands
- Settings backup/restore and recovery reset
- Error handling, bounded logs and auto reconnect backoff

## Build
Use GitHub Actions on `windows-latest`.

The build script downloads the current Android Platform-Tools package from Google's stable URL and scrcpy v4.1 Windows x64 from the official Genymobile repository, then embeds them as resources before publishing.

## Important compatibility notes
Some Android capabilities vary by Android version, OEM, USB debugging state, Wireless Debugging support and game restrictions. The mapper, clipboard and notification integrations therefore use best-effort behavior and explicit fallback/error messages instead of pretending to be universal.

## Local Windows build
Run `BUILD-ONE-EXE.ps1` from Windows with .NET 8 SDK. It prepares embedded tools, publishes self-contained win-x64, and fails unless the publish folder contains exactly `RyzoriaUI.exe`.

## Low-end design
Auto mode is intentionally conservative for N2040 + 4 GB: 480p/20 FPS/2 Mbps scrcpy, no decorative blur, slow device polling, bounded logs, no permanent Windows service, and no full-disk scan on search.
