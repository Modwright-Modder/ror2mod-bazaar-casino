param(
    [Parameter(Mandatory=$true)][string]$Version,
    [Parameter(Mandatory=$true)][string]$NotesPath,
    [string]$ProjectRoot=(Split-Path $PSScriptRoot -Parent)
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if($Version -notmatch '^\d+\.\d+\.\d+$'){throw 'Use a semantic version such as 1.11.0'}
$notes=[IO.File]::ReadAllText((Resolve-Path -LiteralPath $NotesPath),[Text.Encoding]::UTF8).Trim()
$lines=@($notes -split '\r?\n' | Where-Object { $_.Trim() })
if($lines.Count -lt 1 -or $lines.Count -gt 6 -or @($lines | Where-Object {$_ -notmatch '^- .+'}).Count){throw 'Release notes must contain one to six short Markdown bullets'}
$updates=@()
foreach($relative in @('README.md','thunderstore/README.md')){
    $path=Join-Path $ProjectRoot $relative
    $text=[IO.File]::ReadAllText($path,[Text.Encoding]::UTF8)
    if($text -match ('(?m)^### '+[regex]::Escape($Version)+'\s*$')){throw "Release $Version already exists in $relative; previous notes were preserved"}
    $newLine=if($text.Contains("`r`n")){"`r`n"}else{"`n"}
    $entry='### '+$Version+$newLine+$newLine+($lines -join $newLine)+$newLine+$newLine
    $header=[regex]::Match($text,'(?m)^## Update history[^\S\r\n]*\r?\n')
    if($header.Success){
        $index=$header.Index+$header.Length
        if([regex]::IsMatch($text.Substring($index),'(?m)^## ')){throw "Update history must be the last section in $relative"}
        $text=$text.Substring(0,$index)+$newLine+$entry+$text.Substring($index).TrimStart("`r","`n")
    }else{
        $text=$text.TrimEnd()+$newLine+$newLine+'## Update history'+$newLine+$newLine+$entry
    }

    $updates+=@{Path=$path;Text=$text}
}
# Validate both before changing either, preserving all old history verbatim.
foreach($update in $updates){[IO.File]::WriteAllText($update.Path,$update.Text,[Text.UTF8Encoding]::new($false))}
Write-Output "Prepended $Version to both README histories."
