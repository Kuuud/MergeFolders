param(
    [string]$InstallDirectory = "$env:LOCALAPPDATA\MergeFolders"
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $projectRoot 'src\MergeFolders\MergeFolders.csproj'
$exe = Join-Path $InstallDirectory 'MergeFolders.exe'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '未检测到 dotnet。请安装 .NET SDK 8、9 或 10 后重新运行。'
}

$dotnetVersionText = (& dotnet --version).Trim()
try {
    $dotnetMajor = [int]($dotnetVersionText.Split('.')[0])
} catch {
    throw "无法识别 dotnet SDK 版本：$dotnetVersionText"
}
if ($dotnetMajor -lt 8) {
    throw "当前 dotnet SDK 为 $dotnetVersionText，需要 .NET 8 或更高版本。"
}
Write-Host "检测到 .NET SDK $dotnetVersionText"

New-Item -ItemType Directory -Force -Path $InstallDirectory | Out-Null

$publishArgs = @(
    'publish', $project,
    '-c', 'Release',
    '-r', 'win-x64',
    '--self-contained', 'true',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-o', $InstallDirectory
)

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

# Register a multi-selection legacy Shell verb for up to 100 selected folders.
# Microsoft documents Player as the selection model for verbs that support any number of items;
# legacy Player verbs have a 100-item default limit.
$key = 'HKCU:\Software\Classes\Directory\shell\MergeFolders'
New-Item -Path $key -Force | Out-Null

# Build the Chinese menu label without embedding non-ASCII text in the script.
$menuText = ([char]0x5408) + ([char]0x5e76) + ([char]0x6587) + ([char]0x4ef6) + ([char]0x5939)
Set-ItemProperty -Path $key -Name 'MUIVerb' -Value $menuText
Set-ItemProperty -Path $key -Name 'Icon' -Value $exe
Set-ItemProperty -Path $key -Name 'MultiSelectModel' -Value 'Player'

$commandKey = Join-Path $key 'command'
New-Item -Path $commandKey -Force | Out-Null

$arguments = 1..100 | ForEach-Object { '"%{0}"' -f $_ }
$command = '"{0}" {1}' -f $exe, ($arguments -join ' ')
Set-ItemProperty -Path $commandKey -Name '(default)' -Value $command

Write-Host "Installed to: $InstallDirectory"
Write-Host 'Right-click one or more folders and choose the Merge Folders command.'
Write-Host 'On Windows 11, it may be under Show more options.'
Write-Host 'If the menu does not refresh immediately, restart explorer.exe.'
