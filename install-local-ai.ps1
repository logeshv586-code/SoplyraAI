$ErrorActionPreference = "Stop"

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
  throw "For safety, run this setup script from a normal non-Administrator PowerShell window."
}

$ollamaCandidates = @(
  (Join-Path $env:LOCALAPPDATA "Programs\Ollama\ollama.exe"),
  (Join-Path $env:ProgramFiles "Ollama\ollama.exe")
)

$ollama = $ollamaCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1

if (-not $ollama) {
  $winget = Join-Path $env:LOCALAPPDATA "Microsoft\WindowsApps\winget.exe"
  if (-not (Test-Path -LiteralPath $winget -PathType Leaf)) {
    throw "Windows Package Manager was not found. Install Ollama manually from its official source, then rerun this script."
  }

  Write-Host "Installing Ollama with Windows Package Manager..."
  & $winget install --id Ollama.Ollama -e --accept-package-agreements --accept-source-agreements
  if ($LASTEXITCODE -ne 0) {
    throw "Ollama installation failed with exit code $LASTEXITCODE."
  }

  $ollama = $ollamaCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
  if (-not $ollama) {
    throw "Ollama was installed but its trusted executable path was not found. Restart PowerShell and retry."
  }
}

Write-Host "Downloading qwen2.5:0.5b..."
& $ollama pull qwen2.5:0.5b
if ($LASTEXITCODE -ne 0) {
  throw "Model download failed with exit code $LASTEXITCODE."
}

Write-Host "Local AI ready. Endpoint: http://127.0.0.1:11434/v1" -ForegroundColor Green
