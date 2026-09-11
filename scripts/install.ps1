param(
    [string]$InstallDirectory = "$env:LOCALAPPDATA\MergeFolders"
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $InstallDirectory 'MergeFolders.exe'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK 未安装。请安装 .NET 8 SDK 后再运行此脚本。'
}

New-Item -ItemType Directory -Force -Path $InstallDirectory | Out-Null

dotnet publish (Join-Path $projectRoot 'src\MergeFolders\MergeFolders.csproj') `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $InstallDirectory

$key = 'HKCU:\Software\Classes\Directory\shell\MergeFolders'
New-Item -Path $key -Force | Out-Null
Set-ItemProperty -Path $key -Name 'MUIVerb' -Value '合并文件夹'
Set-ItemProperty -Path $key -Name 'Icon' -Value $exe
Set-ItemProperty -Path $key -Name 'MultiSelectModel' -Value 'Player'

$commandKey = Join-Path $key 'command'
New-Item -Path $commandKey -Force | Out-Null
Set-ItemProperty -Path $commandKey -Name '(default)' -Value "`"$exe`" `"%1`""

Write-Host "已安装到：$InstallDirectory"
Write-Host '右键多个文件夹后，打开“显示更多选项”即可看到“合并文件夹”。'
Write-Host '若菜单未立即刷新，可重启 explorer.exe。'
