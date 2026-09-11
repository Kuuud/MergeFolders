param(
    [string]$InstallDirectory = "$env:LOCALAPPDATA\MergeFolders"
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $projectRoot 'src\MergeFolders\MergeFolders.csproj'
$exe = Join-Path $InstallDirectory 'MergeFolders.exe'

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -eq $dotnet) {
    throw 'dotnet SDK was not found. Install .NET SDK 8, 9, or 10.'
}

$version = (& dotnet --version).Trim()
$major = 0
[void][int]::TryParse($version.Split('.')[0], [ref]$major)
if ($major -lt 8) {
    throw "dotnet SDK $version is too old. Install .NET SDK 8 or newer."
}

Write-Host "Using .NET SDK $version"
Write-Host "Publishing MergeFolders..."

New-Item -ItemType Directory -Path $InstallDirectory -Force | Out-Null

& dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $InstallDirectory
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Write-Host 'Registering Explorer context menu...'

$key = 'HKCU:\Software\Classes\Directory\shell\MergeFolders'
New-Item -Path $key -Force | Out-Null

# Unicode code points for the Chinese text "合并文件夹".
$menuText = [char]0x5408 + [char]0x5E76 + [char]0x6587 + [char]0x4EF6 + [char]0x5939
Set-ItemProperty -Path $key -Name 'MUIVerb' -Value $menuText
Set-ItemProperty -Path $key -Name 'Icon' -Value $exe
Set-ItemProperty -Path $key -Name 'MultiSelectModel' -Value 'Player'

$commandKey = Join-Path $key 'command'
New-Item -Path $commandKey -Force | Out-Null

# Player selection model: pass up to 100 selected directory paths as %1 ... %100.
$command = '"{0}" %1 %2 %3 %4 %5 %6 %7 %8 %9 %10 %11 %12 %13 %14 %15 %16 %17 %18 %19 %20 %21 %22 %23 %24 %25 %26 %27 %28 %29 %30 %31 %32 %33 %34 %35 %36 %37 %38 %39 %40 %41 %42 %43 %44 %45 %46 %47 %48 %49 %50 %51 %52 %53 %54 %55 %56 %57 %58 %59 %60 %61 %62 %63 %64 %65 %66 %67 %68 %69 %70 %71 %72 %73 %74 %75 %76 %77 %78 %79 %80 %81 %82 %83 %84 %85 %86 %87 %88 %89 %90 %91 %92 %93 %94 %95 %96 %97 %98 %99 %100' -f $exe
Set-ItemProperty -Path $commandKey -Name '(default)' -Value $command

Write-Host ''
Write-Host 'Installation completed.'
Write-Host "Installed to: $InstallDirectory"
Write-Host 'Restart Windows Explorer if the menu does not appear immediately.'
