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
    foreach ($readme in @('README.md','thunderstore/README.md')) {
        $readmeText=[IO.File]::ReadAllText((Join-Path $projectRoot $readme),[Text.Encoding]::UTF8)
        $latest=[regex]::Match($readmeText,'(?ms)^## Update history\s*\r?\n+### ([0-9.]+)')
        if (!$latest.Success -or $latest.Groups[1].Value -ne $manifest.version_number) { throw "Prepend short release notes for $($manifest.version_number) to $readme using scripts/add-release-notes.ps1" }
        $historyStart=[regex]::Match($readmeText,'(?m)^## Update history[^\r\n]*\r?\n')
        if ([regex]::IsMatch($readmeText.Substring($historyStart.Index+$historyStart.Length),'(?m)^## ')) { throw "Update history must be the last section in $readme" }
    }
    $englishPath=Join-Path $projectRoot 'language/en.json'
    $english=[IO.File]::ReadAllText($englishPath,[Text.Encoding]::UTF8)|ConvertFrom-Json
    if (!$english.entries -or $english.entries.Count -lt 1) { throw 'English language template is missing/empty' }
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
        # Keep the technical reference and supplied screenshots with the package.
        # The listing README uses GitHub image URLs; repo README uses local paths.
        $docsPath = Join-Path $projectRoot 'docs'
        if (Test-Path -LiteralPath $docsPath -PathType Container) {
            foreach ($doc in Get-ChildItem -LiteralPath $docsPath -File -Recurse) {
                $relativePath = [IO.Path]::GetRelativePath($projectRoot, $doc.FullName).Replace('\','/')
                $files[$relativePath] = $doc.FullName
            }
        }
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $projectRoot 'language') -File -Recurse) {
            $relative=[IO.Path]::GetRelativePath((Join-Path $projectRoot 'language'),$file.FullName).Replace('\','/')
            $files['plugins/BazaarCasino/language/'+$relative]=$file.FullName
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
