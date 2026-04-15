# Zips the workspace excluding build artifacts, .git, and hidden/system files
$src = $PSScriptRoot
$rootFolderName = (Split-Path $src -Leaf)
$zipName = "$rootFolderName.zip"
$zipPath = Join-Path (Split-Path $src -Parent) $zipName
$tmp = Join-Path $env:TEMP "zip-workspace-temp"
$tmpRoot = Join-Path $tmp $rootFolderName

# Clean previous
Remove-Item $zipPath -ErrorAction SilentlyContinue
if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }

# Create temp root folder
New-Item -ItemType Directory -Path $tmpRoot | Out-Null

# Copy excluding build artifacts + .git + hidden/system files
robocopy $src $tmpRoot /E `
/XD bin obj dist .angular node_modules .vs .vscode docs .git `
/XF "*.zip" `
/XA:SH `
/NFL /NDL /NJH /NJS /NC /NS /NP | Out-Null

# Create zip
Compress-Archive -Path $tmpRoot -DestinationPath $zipPath -Force

# Cleanup temp
Remove-Item $tmp -Recurse -Force

# Report
$size = [math]::Round((Get-Item $zipPath).Length / 1KB, 1)
Write-Host "Created: $zipPath ($size KB)"