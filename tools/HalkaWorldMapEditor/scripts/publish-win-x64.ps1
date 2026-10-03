$ErrorActionPreference = 'Stop'
$toolRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$projectFile = Join-Path $toolRoot 'HalkaWorldMapEditor/HalkaWorldMapEditor.csproj'
$destination = Join-Path $toolRoot 'dist/win-x64'
dotnet publish $projectFile -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $destination --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$exe = Join-Path $destination 'HALKA WORLD MAP EDITOR.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw "Published EXE missing: $exe" }
Write-Output "Published: $exe"
