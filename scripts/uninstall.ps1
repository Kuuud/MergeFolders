$ErrorActionPreference = 'Stop'
$key = 'HKCU:\Software\Classes\Directory\shell\MergeFolders'
if (Test-Path $key) {
    Remove-Item $key -Recurse -Force
    Write-Host 'Merge Folders context menu removed.'
} else {
    Write-Host 'Merge Folders context menu is not installed.'
}
