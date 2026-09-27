$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$embedded = Join-Path $root 'EmbeddedTools'
$tmp = Join-Path $env:RUNNER_TEMP 'ryzoria-tools'
Remove-Item $embedded -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path (Join-Path $embedded 'adb'), (Join-Path $embedded 'scrcpy'), $tmp | Out-Null

# Google publishes a stable URL that resolves to the newest Windows Platform-Tools package.
$platformUrl = 'https://dl.google.com/android/repository/platform-tools-latest-windows.zip'
# Official scrcpy release verified from the Genymobile repository during project preparation.
$scrcpyVersion = '4.1'
$scrcpyUrl = "https://github.com/Genymobile/scrcpy/releases/download/v$scrcpyVersion/scrcpy-win64-v$scrcpyVersion.zip"
$platformZip = Join-Path $tmp 'platform-tools.zip'
$scrcpyZip = Join-Path $tmp 'scrcpy.zip'

Invoke-WebRequest -Uri $platformUrl -OutFile $platformZip
Invoke-WebRequest -Uri $scrcpyUrl -OutFile $scrcpyZip
Write-Host "Platform-Tools SHA256:" (Get-FileHash $platformZip -Algorithm SHA256).Hash
Write-Host "scrcpy SHA256:" (Get-FileHash $scrcpyZip -Algorithm SHA256).Hash

Expand-Archive -Path $platformZip -DestinationPath (Join-Path $tmp 'platform') -Force
Expand-Archive -Path $scrcpyZip -DestinationPath (Join-Path $tmp 'scrcpy') -Force
$platformDir = Get-ChildItem (Join-Path $tmp 'platform') -Directory | Select-Object -First 1
$scrcpyDir = Get-ChildItem (Join-Path $tmp 'scrcpy') -Directory | Select-Object -First 1
if ($null -eq $platformDir -or $null -eq $scrcpyDir) { throw 'Could not locate extracted tool directories.' }

Copy-Item (Join-Path $platformDir.FullName 'adb.exe') (Join-Path $embedded 'adb') -Force
Copy-Item (Join-Path $platformDir.FullName 'AdbWinApi.dll') (Join-Path $embedded 'adb') -Force
Copy-Item (Join-Path $platformDir.FullName 'AdbWinUsbApi.dll') (Join-Path $embedded 'adb') -Force

# Keep the entire official scrcpy Windows package, including its native DLLs and Android server artifact.
Get-ChildItem $scrcpyDir.FullName -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($scrcpyDir.FullName.Length).TrimStart([char[]]('\','/'))
    $dest = Join-Path (Join-Path $embedded 'scrcpy') $relative
    New-Item -ItemType Directory -Force -Path (Split-Path $dest -Parent) | Out-Null
    Copy-Item $_.FullName $dest -Force
}

@"
RyzoriaUI v1.7 bundled tools
Android SDK Platform-Tools: $platformUrl (latest stable at build time)
scrcpy: v$scrcpyVersion Windows x64
Official upstream sources only.
"@ | Set-Content (Join-Path $embedded 'BUNDLED_TOOLS.txt')
