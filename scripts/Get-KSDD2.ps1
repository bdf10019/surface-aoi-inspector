[CmdletBinding()]
param(
    [string]$Destination = (Join-Path $PSScriptRoot '..\data\raw')
)

$ErrorActionPreference = 'Stop'
$source = 'https://data.vicos.si/datasets/KSDD/KolektorSDD2.zip'
$expectedBytes = 853126555
$expectedSha256 = 'EDCDB486809B24F1D17B785E30C52FAFC5999554DD5FE18DDF77B61CEB6F36A8'
$destinationRoot = [System.IO.Path]::GetFullPath($Destination)
$zipPath = Join-Path $destinationRoot 'KolektorSDD2.zip'
$extractPath = Join-Path $destinationRoot 'KolektorSDD2'

New-Item -ItemType Directory -Path $destinationRoot -Force | Out-Null

if (-not (Test-Path -LiteralPath $zipPath)) {
    Import-Module BitsTransfer
    Start-BitsTransfer -Source $source -Destination $zipPath -DisplayName 'KSDD2 dataset'
}

$zip = Get-Item -LiteralPath $zipPath
if ($zip.Length -ne $expectedBytes) {
    throw "KSDD2 archive size mismatch. Expected $expectedBytes bytes, found $($zip.Length)."
}

$actualSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath).Hash
if ($actualSha256 -ne $expectedSha256) {
    throw "KSDD2 archive SHA-256 mismatch."
}

if (-not (Test-Path -LiteralPath $extractPath)) {
    Expand-Archive -LiteralPath $zipPath -DestinationPath $extractPath
}

Write-Output "KSDD2 ready: $extractPath"
