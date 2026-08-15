param(
    [string]$SourceDirectory = (Join-Path $PSScriptRoot "..\ai\runs\ksdd2-v1"),
    [string]$DestinationDirectory = (Join-Path $PSScriptRoot "..\models\local\ksdd2-v1")
)

$ErrorActionPreference = "Stop"
$modelName = "surface_defect_segmenter.onnx"
$dataName = "surface_defect_segmenter.onnx.data"
$expected = @{
    $modelName = "3B8AEE0FCEDE6BE7C470783582D34FA2F24C2ABB280D01DADF40466DBE6CABAA"
    $dataName = "935E5C97DE460554F16780FCEE504789FC7770ECCCC6F7DB5CD5BFAEA6CB288B"
}

New-Item -ItemType Directory -Force -Path $DestinationDirectory | Out-Null

foreach ($fileName in $expected.Keys) {
    $sourcePath = Join-Path $SourceDirectory $fileName
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        throw "Missing trained model artifact: $sourcePath"
    }

    $actualHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
    if ($actualHash -ne $expected[$fileName]) {
        throw "Unexpected SHA-256 for $fileName. Expected $($expected[$fileName]), got $actualHash"
    }

    Copy-Item -LiteralPath $sourcePath -Destination (Join-Path $DestinationDirectory $fileName) -Force
}

Write-Host "AI model prepared at $DestinationDirectory"
