$ErrorActionPreference = 'Stop'
$key = 'HKCU:\Software\Classes\Directory\shell\MergeFolders'
if (Test-Path $key) {
    Remove-Item $key -Recurse -Force
    Write-Host '已移除“合并文件夹”右键菜单。'
} else {
    Write-Host '没有找到已安装的右键菜单。'
}
