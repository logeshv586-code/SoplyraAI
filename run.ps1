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
    throw "dotnet was not found. Please install the .NET 8 SDK or run install-local-ai.ps1."
}

# Close any running instance to avoid file lock
$running = Get-Process SoplyraAI -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Closing previous SoplyraAI instance..." -ForegroundColor Yellow
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 400
}

$dotnet = Get-DotNetPath
$root = $PSScriptRoot

Write-Host "Using .NET SDK at: $dotnet" -ForegroundColor Cyan
Write-Host "Starting SoplyraAI..." -ForegroundColor Green

& $dotnet run --project "$root\src\SoplyraAI.App\SoplyraAI.App.csproj"
