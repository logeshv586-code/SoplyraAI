$ErrorActionPreference = "Stop"

function Get-DotNetPath {
    $candidates = @(
        (Get-Command dotnet -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
        "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe",
        "$env:ProgramFiles\dotnet\dotnet.exe",
        "$env:ProgramFiles(x86)\dotnet\dotnet.exe"
    )
    foreach ($c in $candidates) {
        if ($c -and (Test-Path $c)) { return $c }
    }
    throw "dotnet was not found. Please install the .NET 8 SDK."
}

# Automatically close any running instance so files are not locked
Get-Process | Where-Object { $_.ProcessName -eq "SoplyraAI" -or ($_.ProcessName -eq "dotnet" -and $_.MainWindowTitle -like "*SoplyraAI*") } | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 400

$dotnet = Get-DotNetPath
$root = $PSScriptRoot

Write-Host "Building SoplyraAI using: $dotnet" -ForegroundColor Cyan
& $dotnet build "$root\SoplyraAI.sln"
Write-Host "Build complete!" -ForegroundColor Green
