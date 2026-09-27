$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\scripts\PrepareTools.ps1"
dotnet restore "$PSScriptRoot\RyzoriaUI.csproj"
dotnet publish "$PSScriptRoot\RyzoriaUI.csproj" --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true --output "$PSScriptRoot\publish"
$files = Get-ChildItem "$PSScriptRoot\publish" -File
if ($files.Count -ne 1 -or $files[0].Name -ne 'RyzoriaUI.exe') { throw 'Publish output is not a single RyzoriaUI.exe.' }
Write-Host "Built: $($files[0].FullName)"
Write-Host "Size : $([math]::Round($files[0].Length / 1MB, 2)) MB"
