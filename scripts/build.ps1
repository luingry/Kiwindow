<#
    Release build → tests → SwissTechKnife module package (→ installer).
      powershell -File scripts/build.ps1               build + tests + artifacts/kiwindow-<ver>.zip + catalog entry
      powershell -File scripts/build.ps1 -Installer    + artifacts/installer/Kiwindow-<ver>-setup.exe (Inno Setup 6)
      powershell -File scripts/build.ps1 -SkipTests
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [switch]$SkipTests,
    [switch]$Installer
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

dotnet build (Join-Path $root 'Kiwindow.sln') -c $Configuration -nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

if (-not $SkipTests) {
    & (Join-Path $root "tests/Kiwindow.Tests/bin/$Configuration/net48/Kiwindow.Tests.exe")
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}

& (Join-Path $PSScriptRoot 'pack-module.ps1') -Configuration $Configuration -NoBuild

if ($Installer) {
    [xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
    $version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
    $iscc = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) { throw 'Inno Setup 6 not found (https://jrsoftware.org/isdl.php or: winget install JRSoftware.InnoSetup).' }
    & $iscc /Q "/DAppVersion=$version" (Join-Path $root 'installer/Kiwindow.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
    Write-Host "Installer: artifacts/installer/Kiwindow-$version-setup.exe" -ForegroundColor Green
}
