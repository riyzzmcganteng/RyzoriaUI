# RyzoriaUI v1.0

Windows-only Android desktop experience by Riyzz. RyzoriaUI uses **ADB + scrcpy**; no APK is required on the Android phone.

## Requirements
- Windows 10/11 x64
- .NET 8 SDK
- Android Platform Tools (ADB)
- scrcpy
- Android 10+ recommended
- USB debugging enabled

## Build
```powershell
dotnet restore
dotnet build -c Release
dotnet run
```

## Use
1. On Android: Settings → About phone → Developer options → USB debugging.
2. Connect the phone by USB.
3. Accept the RSA debugging prompt on the phone.
4. Run RyzoriaUI.
5. Press **Scan Device**.
6. Choose 720p / 30 FPS / 4 Mbps for low-end PCs.
7. Press **Start Mirror**.

## Notes
The Lock Screen button sends the Android screen-lock key event; it does not power the phone off. Exact behavior can vary by device/vendor.

## Roadmap
v1.0: ADB + scrcpy + device info + performance controls + Android buttons.
v1.1: compatibility checks and reconnect.
v1.2: gaming mode and keyboard mapping.
v1.3: wireless ADB and multi-device.
v1.4: polish and stability.
v1.5: Linux port.
