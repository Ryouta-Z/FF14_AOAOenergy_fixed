param(
    [Parameter(Mandatory=$true)][string]$DalamudPath,
    [string]$DotnetCommand = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$DalamudPath = (Resolve-Path -LiteralPath $DalamudPath).Path
if (-not (Test-Path -LiteralPath (Join-Path $DalamudPath 'Dalamud.dll'))) {
    throw 'DalamudPath must contain the API 15 Dalamud.dll and its companion libraries.'
}
& $DotnetCommand build (Join-Path $PSScriptRoot 'AoAoEnergy/AoAoEnergy.csproj') -c Release "-p:DalamudLibPath=$DalamudPath" -p:RestoreLockedMode=true
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output (Join-Path $PSScriptRoot 'AoAoEnergy/bin/Release/AoAoEnergy/latest.zip')
