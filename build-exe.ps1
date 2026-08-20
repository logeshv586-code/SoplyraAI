$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $root "dist\win-x64"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }

dotnet restore "$root\SoplyraAI.sln"
dotnet publish "$root\src\SoplyraAI.App\SoplyraAI.App.csproj" `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None -p:DebugSymbols=false `
  -o $out

Write-Host "Built: $out\SoplyraAI.exe" -ForegroundColor Green
