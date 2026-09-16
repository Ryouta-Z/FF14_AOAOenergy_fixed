param([Parameter(Mandatory=$true)][string]$Package)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Package))
try {
    $required = @('AoAoEnergy.dll','AoAoEnergy.json','AoAoEnergy.deps.json','Penumbra.String.dll','Penumbra.String.xml','ev_energydrink_01x_30s.avfx')
    foreach ($name in $required) {
        $entry = $zip.GetEntry($name)
        if ($null -eq $entry -or $entry.Length -eq 0) { throw "Missing or empty: $name" }
        "PASS package entry: $name ($($entry.Length) bytes)"
    }
    $reader = [IO.StreamReader]::new($zip.GetEntry('AoAoEnergy.json').Open())
    try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    $repo = @(Get-Content (Join-Path $PSScriptRoot 'repo.json') -Raw | ConvertFrom-Json)[0]
    foreach ($field in @('InternalName','AssemblyVersion','DalamudApiLevel','ApplicableVersion')) {
        if ($manifest.$field -ne $repo.$field) { throw "Manifest/repo mismatch: $field" }
    }
    if ($manifest.DalamudApiLevel -ne 15 -or $manifest.AssemblyVersion -ne '1.0.3.3') { throw 'Unexpected version' }
    if ($manifest.ApplicableVersion -ne '2026.09.01.0000.0000') { throw 'Unexpected applicable game version' }
    'PASS manifest/repo API 15, version 1.0.3.3, game 2026.09.01.0000.0000'
    $stream = $zip.GetEntry('ev_energydrink_01x_30s.avfx').Open()
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $assetHash = [Convert]::ToHexString($sha.ComputeHash($stream)) } finally { $stream.Dispose(); $sha.Dispose() }
    $sourceHash = (Get-FileHash (Join-Path $PSScriptRoot 'AoAoEnergy/ev_energydrink_01x_30s.avfx')).Hash
    if ($assetHash -ne $sourceHash) { throw 'VFX asset mismatch' }
    "PASS AVFX unchanged: $assetHash"
    if (@($zip.Entries | Where-Object { $_.FullName -match '(^|/)(Dalamud|FFXIVClientStructs)\.dll$' }).Count) {
        throw 'Host libraries must not be distributed in the plugin package'
    }
    'PASS no bundled host libraries'
    'NOT TESTED: live hook installation, runtime ABI, VFX rendering and unload behavior'
} finally { $zip.Dispose() }
Get-FileHash -LiteralPath $Package | Format-List Algorithm,Hash,Path
