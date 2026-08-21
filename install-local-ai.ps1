param(
  [ValidateSet("qwen3:4b", "qwen2.5vl:3b", "deepseek-r1:7b", "gemma3:4b")]
  [string]$Model = "qwen3:4b"
)

$ErrorActionPreference = "Stop"

function Find-Ollama {
  $candidates = @(
    "$env:LOCALAPPDATA\Programs\Ollama\ollama.exe",
    "$env:ProgramFiles\Ollama\ollama.exe"
  )
  return $candidates | Where-Object { $_ -and (Test-Path $_ -PathType Leaf) } | Select-Object -First 1
}

$ollama = Find-Ollama
if (-not $ollama) {
  $winget = "$env:LOCALAPPDATA\Microsoft\WindowsApps\winget.exe"
  if (-not (Test-Path $winget -PathType Leaf)) { throw "Windows Package Manager was not found. Install Ollama manually." }
  Write-Host "Installing Ollama..."
  & $winget install --id Ollama.Ollama -e --accept-package-agreements --accept-source-agreements
  if ($LASTEXITCODE -ne 0) { throw "Ollama installation failed." }
  $ollama = Find-Ollama
  if (-not $ollama) { throw "Ollama installed but its trusted executable was not found. Restart SoplyraAI and retry." }
}

Write-Host "Downloading $Model..."
& $ollama pull $Model
if ($LASTEXITCODE -ne 0) { throw "Model download failed." }
Write-Host "Local AI ready: $Model · http://127.0.0.1:11434/v1" -ForegroundColor Green
