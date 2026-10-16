<#
    Builds the offline installer bundle for Noctalia Evolved.

    Publishes the app and the setup window, puts the app where the setup window looks for it,
    and writes a zip plus SHA-256 checksums. Everything happens on this machine: no upload, no
    signing, no network. The zip is the offline installer — unpack it anywhere and run
    NoctaliaEvolved-Setup.exe.

    Usage:
      powershell -NoProfile -ExecutionPolicy Bypass -File tools/make-installer.ps1
      powershell -NoProfile -ExecutionPolicy Bypass -File tools/make-installer.ps1 -Version 1.0.1
#>

param(
    [string]$Version = '',
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$out = Join-Path $repo 'build\installer'
$payload = Join-Path $out 'payload'

if (-not $Version) {
    $props = Get-Content (Join-Path $repo 'Directory.Build.props') -Raw
    $match = [regex]::Match($props, '<Version>([^<]+)</Version>')
    if (-not $match.Success) { throw 'Directory.Build.props has no <Version> to build a bundle from.' }
    $Version = $match.Groups[1].Value
}

Write-Host "Building Noctalia Evolved $Version ($Runtime) into $out"

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force -Path $out | Out-Null

Push-Location $repo
try {
    # Framework-dependent by default: the app is a few hundred files, not a runtime. Pass a
    # self-contained publish here if a machine without the .NET Desktop Runtime needs it.
    dotnet publish src/NoctaliaWin.App -c Release -r $Runtime --self-contained false `
        -p:Version=$Version -o $payload | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish of the app failed.' }

    dotnet publish src/NoctaliaWin.Installer -c Release -r $Runtime --self-contained false `
        -p:Version=$Version -o $out | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish of the installer failed.' }
}
finally {
    Pop-Location
}

$apphost = Join-Path $payload 'NoctaliaWin.exe'
if (-not (Test-Path $apphost)) { throw "The published payload has no NoctaliaWin.exe in $payload." }

$setup = Join-Path $out 'NoctaliaEvolved-Setup.exe'
if (-not (Test-Path $setup)) { throw "No NoctaliaEvolved-Setup.exe was produced in $out." }

$zip = Join-Path $repo "build\NoctaliaEvolved-$Version-Setup-$Runtime.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }

# The payload folder goes in as-is, which is where the setup window looks for the app.
Compress-Archive -Path (Join-Path $out 'payload'), $setup,
    (Join-Path $out 'NoctaliaEvolved-Setup.dll') -DestinationPath $zip

$checksums = Join-Path (Split-Path -Parent $zip) 'SHA256SUMS.txt'
$entries = @($zip) + (Get-ChildItem $payload -File -Recurse | Select-Object -ExpandProperty FullName)
$rows = foreach ($file in $entries) {
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $file).Hash.ToLower()
    $relative = if ($file.StartsWith($payload, [System.StringComparison]::OrdinalIgnoreCase)) {
        'payload\' + $file.Substring($payload.Length).TrimStart('\')
    } else {
        Split-Path -Leaf $file
    }
    "$hash  $relative"
}
Set-Content -LiteralPath $checksums -Value $rows -Encoding utf8

Write-Host ""
Write-Host "Offline installer: $zip"
Write-Host "Checksums:         $checksums ($(($rows | Measure-Object).Count) files)"
Write-Host "Unpack the zip and run NoctaliaEvolved-Setup.exe. Nothing was uploaded, and the"
Write-Host "executables are unsigned: Windows will show SmartScreen until a person chooses Run."
