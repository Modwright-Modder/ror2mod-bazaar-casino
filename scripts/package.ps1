param([switch]$SkipBuild)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    $manifestPath = Join-Path $projectRoot 'thunderstore/manifest.json'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.name -notmatch '^[A-Za-z0-9_]{1,128}$' -or $manifest.version_number -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid Thunderstore name/version' }
    if ($manifest.description.Length -gt 250) { throw 'Thunderstore description exceeds 250 characters' }
    foreach ($dependency in $manifest.dependencies) { if ($dependency -notmatch '^.+-.+-\d+\.\d+\.\d+$') { throw "Invalid dependency: $dependency" } }
    $pluginText = Get-Content -LiteralPath 'src/BazaarCasino/CasinoPlugin.cs' -Raw
    $pluginVersion = [regex]::Match($pluginText, '\[BepInPlugin\("[^"]+", "[^"]+", "([^"]+)"\)\]').Groups[1].Value
    if ($pluginVersion -ne $manifest.version_number) { throw 'Manifest and BepInPlugin versions differ' }
    $iconPath = Join-Path $projectRoot 'thunderstore/icon.png'
    $icon = [IO.File]::ReadAllBytes($iconPath)
    if ($icon.Length -lt 24 -or [BitConverter]::ToString($icon[0..7]) -ne '89-50-4E-47-0D-0A-1A-0A') { throw 'icon.png must be PNG' }
    $iconWidth = $icon[16]*16777216+$icon[17]*65536+$icon[18]*256+$icon[19]
    $iconHeight = $icon[20]*16777216+$icon[21]*65536+$icon[22]*256+$icon[23]
    if ($iconWidth -ne 256 -or $iconHeight -ne 256) { throw 'icon.png must be 256x256' }
    if (!$SkipBuild) {
        & dotnet build src/BazaarCasino/BazaarCasino.csproj -c Release
        if ($LASTEXITCODE -ne 0) { throw 'Release build failed' }
        & dotnet run --project tests/BazaarCasino.Tests -c Release
        if ($LASTEXITCODE -ne 0) { throw 'Rules tests failed' }
    }
    $dllPath = Join-Path $projectRoot 'src/BazaarCasino/bin/Release/netstandard2.1/BazaarCasino.dll'
    if (!(Test-Path -LiteralPath $dllPath -PathType Leaf)) { throw 'Build DLL missing' }
    $dist = Join-Path $projectRoot 'dist'
    New-Item -ItemType Directory -Path $dist -Force | Out-Null
    $zipPath = Join-Path $dist ("BazaarCasino-{0}.zip" -f $manifest.version_number)
    Add-Type -AssemblyName System.IO.Compression
    $stream = [IO.File]::Open($zipPath,[IO.FileMode]::Create)
    $archive = [IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create)
    try {
        $files = @{
            'manifest.json'=$manifestPath
            'README.md'=(Join-Path $projectRoot 'thunderstore/README.md')
            'icon.png'=$iconPath
            'CHANGELOG.md'=(Join-Path $projectRoot 'CHANGELOG.md')
            'LICENSE'=(Join-Path $projectRoot 'LICENSE')
            'Cards-LICENSE.txt'=(Join-Path $projectRoot 'Cards-LICENSE.txt')
            'THIRD_PARTY_NOTICES.md'=(Join-Path $projectRoot 'THIRD_PARTY_NOTICES.md')
            'plugins/BazaarCasino/BazaarCasino.dll'=$dllPath
        }
        foreach ($name in $files.Keys) {
            $entry = $archive.CreateEntry($name,[IO.Compression.CompressionLevel]::Optimal)
            $destination = $entry.Open()
            $source = [IO.File]::OpenRead($files[$name])
            try { $source.CopyTo($destination) } finally { $source.Dispose(); $destination.Dispose() }
        }
    } finally { $archive.Dispose(); $stream.Dispose() }
    Write-Output "Validated Thunderstore package: $zipPath"
} finally { Pop-Location }
