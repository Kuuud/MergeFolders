$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $root 'publish'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }

dotnet publish (Join-Path $root 'src\MergeFolders\MergeFolders.csproj') `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -o $out

Write-Host "Published to: $out"
