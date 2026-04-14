# Zips the workspace excluding build artifacts (bin, obj, dist, .angular, node_modules)
$src = $PSScriptRoot
$zipName = (Split-Path $src -Leaf) + ".zip"
$zipPath = Join-Path (Split-Path $src -Parent) $zipName
$tmp = Join-Path $env:TEMP "zip-workspace-temp"

# Clean previous
Remove-Item $zipPath -ErrorAction SilentlyContinue
if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }

# Copy excluding build artifacts
robocopy $src $tmp /E /XD bin obj dist .angular node_modules .vs .vscode /XF "*.zip" /NFL /NDL /NJH /NJS /NC /NS /NP | Out-Null

# Create zip
Compress-Archive -Path "$tmp\*" -DestinationPath $zipPath -Force

# Cleanup temp
Remove-Item $tmp -Recurse -Force

# Report
$size = [math]::Round((Get-Item $zipPath).Length / 1KB, 1)
Write-Host "Created: $zipPath ($size KB)"
