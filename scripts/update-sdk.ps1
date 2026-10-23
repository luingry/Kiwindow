<#
    Refreshes lib/SwissTechKnife.Sdk.dll (compile-only reference) from a SwissTechKnife checkout.
      powershell -File scripts/update-sdk.ps1 [-HostRepo ..\SwissTechKnife]
#>
[CmdletBinding()]
param([string]$HostRepo = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'SwissTechKnife'))
$ErrorActionPreference = 'Stop'
$project = Join-Path $HostRepo 'src/SwissTechKnife.Sdk/SwissTechKnife.Sdk.csproj'
if (-not (Test-Path $project)) { throw "SDK project not found: $project" }
dotnet build $project -c Release -nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'SDK build failed.' }
$lib = Join-Path (Split-Path -Parent $PSScriptRoot) 'lib'
Copy-Item (Join-Path $HostRepo 'src/SwissTechKnife.Sdk/bin/Release/net48/SwissTechKnife.Sdk.*') $lib -Force
Write-Host "SDK copied to $lib" -ForegroundColor Green
