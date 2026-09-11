[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Ymm4Dir,
    [switch]$SkipWindowsTests
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$Ymm4Dir = (Resolve-Path $Ymm4Dir).Path
if (!(Test-Path "$Ymm4Dir/YukkuriMovieMaker.Plugin.dll")) { throw 'Ymm4Dir must point to YMM4 v4.47+ (.NET 10).' }
if ($env:OS -ne 'Windows_NT' -and !$SkipWindowsTests) { throw 'Windows GPU tests require Windows. Use -SkipWindowsTests only for an unverified alpha package.' }
Push-Location $root
try {
    $env:YMM4_DIR = $Ymm4Dir
    dotnet build tests/Windows -c Release --warnaserror
    if ($LASTEXITCODE) { throw 'Build failed' }
    if (!$SkipWindowsTests) {
        $testOut = Join-Path $root 'tests/Windows/bin/Release/net10.0-windows10.0.19041.0'
        Copy-Item "$Ymm4Dir/*.dll" $testOut -Force
        & "$testOut/YMM4.ExtendedEffectsPack.Tests.exe"
        if ($LASTEXITCODE) { throw 'Windows rendering tests failed: no package produced.' }
    }
    $out = Join-Path $root 'artifacts'
    New-Item $out -ItemType Directory -Force | Out-Null
    $dll = Join-Path $root 'src/YMM4.ExtendedEffectsPack/bin/Release/net10.0-windows10.0.19041.0/YMM4.ExtendedEffectsPack.dll'
    $suffix = if ($SkipWindowsTests) { '.alpha-unverified' } else { '.alpha-warp-tested' }
    $zip = Join-Path $out "YMM4.ExtendedEffectsPack$suffix.zip"
    $package = Join-Path $out "YMM4.ExtendedEffectsPack$suffix.ymme"
    Compress-Archive -Path $dll -DestinationPath $zip -Force
    Move-Item $zip $package -Force
    Copy-Item $dll $out -Force
    Get-FileHash $package -Algorithm SHA256
    Write-Host "Package: $package"
    Write-Host 'YMM4 editor integration and visual QA are still required, even after WARP tests pass.'
}
finally { Pop-Location }
