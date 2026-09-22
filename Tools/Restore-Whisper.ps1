$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$files = Get-Content (Join-Path $PSScriptRoot 'whisper-files.json') -Raw | ConvertFrom-Json
foreach ($file in $files) {
    $target = Join-Path $projectRoot $file.target
    if (Test-Path -LiteralPath $target) {
        $hash = & git hash-object -- $target
        if ($LASTEXITCODE -eq 0 -and $hash -eq $file.sha) { continue }
    }
    New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
    Write-Host ('Downloading ' + $file.target)
    Invoke-WebRequest -Uri $file.url -OutFile ($target + '.download')
    $hash = & git hash-object -- ($target + '.download')
    if ($LASTEXITCODE -ne 0 -or $hash -ne $file.sha) { throw ('Whisper download checksum mismatch: ' + $file.target) }
    Move-Item -LiteralPath ($target + '.download') -Destination $target -Force
}
Write-Host 'Whisper package and model are ready.'
