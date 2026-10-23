<#
    Packages Kiwindow as a SwissTechKnife module (docs/MODULES.md in the host repository):
      artifacts/kiwindow-<ver>.zip       module.json at the root, no SwissTechKnife.Sdk.dll
      artifacts/catalog-entry.json       the entry for the host's catalog/catalog.json
    The version comes from Directory.Build.props and is written into module.json.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src/Kiwindow.Core'
[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
$version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version

# module.json follows the build version (only the first "version": the ones in releaseNotes name past versions).
$manifestPath = Join-Path $project 'module.json'
$utf8 = New-Object Text.UTF8Encoding $false
$manifest = [IO.File]::ReadAllText($manifestPath, $utf8)
$manifest = ([regex]'"version":\s*"[^"]*"').Replace($manifest, "`"version`": `"$version`"", 1)
[IO.File]::WriteAllText($manifestPath, $manifest, $utf8)

if (-not $NoBuild) {
    dotnet build $project -c $Configuration -nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}

$output = Join-Path $project "bin/$Configuration/net48"
Copy-Item $manifestPath $output -Force
$artifacts = Join-Path $root 'artifacts'
New-Item -ItemType Directory -Force $artifacts | Out-Null
$zipPath = Join-Path $artifacts "kiwindow-$version.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath }

$zip = [IO.Compression.ZipFile]::Open($zipPath, 'Create')
try {
    Get-ChildItem $output -Recurse -File |
        Where-Object { $_.Name -notlike 'SwissTechKnife.Sdk.*' -and $_.Extension -notin '.pdb', '.xml' } |
        ForEach-Object {
            $relative = $_.FullName.Substring($output.Length).TrimStart('\', '/').Replace('\', '/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $relative, 'Optimal') | Out-Null
        }
} finally { $zip.Dispose() }

$hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$size = (Get-Item $zipPath).Length
$entry = [ordered]@{
    id             = 'kiwindow'
    name           = 'Kiwindow'
    tagline        = 'Save where every window sits and put them all back with one click.'
    description    = "Arrange your windows across your monitors, capture the layout, and Kiwindow puts every window back in place whenever you want: it reopens what is closed (apps, files, Explorer folders), restores sizes, maximized windows and stacking order.`n`nEdit a layout on a map of your monitors, keep as many profiles as you like and apply them from the notification-area menu. Profiles are shared with the standalone Kiwindow app."
    version        = $version
    icon           = 'M4,5 L10.5,5 L10.5,19 L4,19 Z M13.5,5 L20,5 L20,10.5 L13.5,10.5 Z M13.5,13.5 L20,13.5 L20,19 L13.5,19 Z'
    accent         = '#8CC63F'
    iconImage      = 'icons/kiwindow.png'
    iconBackground = '#211F1D'
    homepage       = 'https://github.com/luingry/Kiwindow'
    package        = "https://github.com/luingry/Kiwindow/releases/download/v$version/kiwindow-$version.zip"
    sha256         = $hash
    size           = $size
}
[IO.File]::WriteAllText((Join-Path $artifacts 'catalog-entry.json'), ($entry | ConvertTo-Json -Depth 4), $utf8)
Write-Host "Packed $zipPath ($size bytes, sha256 $hash)" -ForegroundColor Green
