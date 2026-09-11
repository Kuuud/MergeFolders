$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $root 'publish'
$project = Join-Path $root 'src\MergeFolders\MergeFolders.csproj'

if (Test-Path $out) { Remove-Item $out -Recurse -Force }

$publishArgs = @(
    'publish', $project,
    '-c', 'Release',
    '-r', 'win-x64',
    '--self-contained', 'true',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-o', $out
)

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Write-Host "Published to: $out"
