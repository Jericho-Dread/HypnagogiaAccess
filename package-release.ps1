param(
    [string]$Configuration = "Debug",
    [string]$OutputDir = ".\\dist\\HypnagogiaAccessMod"
)

$ErrorActionPreference = "Stop"

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$dllPath = Join-Path $projectDir "bin\\$Configuration\\net472\\HypnagogiaAccess.dll"
$nvdaPath = Join-Path $projectDir "externals\\nvdaControllerClient64.dll"
$pluginsDir = Join-Path $OutputDir "BepInEx\\plugins"
$rootNvdaPath = Join-Path $OutputDir "nvdaControllerClient64.dll"
$zipPath = Join-Path $projectDir "dist\\HypnagogiaAccessMod.zip"

if (!(Test-Path $dllPath)) {
    throw "Built DLL not found: $dllPath"
}

if (!(Test-Path $nvdaPath)) {
    throw "NVDA client DLL not found: $nvdaPath"
}

if (Test-Path $OutputDir) {
    Remove-Item -LiteralPath $OutputDir -Recurse -Force
}

New-Item -ItemType Directory -Path $pluginsDir | Out-Null

Copy-Item -LiteralPath $dllPath -Destination (Join-Path $pluginsDir "HypnagogiaAccess.dll") -Force
Copy-Item -LiteralPath $nvdaPath -Destination $rootNvdaPath -Force

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $OutputDir "*") -DestinationPath $zipPath -Force

Get-Item $zipPath | Select-Object FullName,Length,LastWriteTime
