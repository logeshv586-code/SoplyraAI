$ErrorActionPreference = "Stop"

if (-not (Get-Command ollama -ErrorAction SilentlyContinue)) {
  Write-Host "Installing Ollama..."
  winget install --id Ollama.Ollama -e --accept-package-agreements --accept-source-agreements
  $possible = @(
    "$env:LOCALAPPDATA\Programs\Ollama\ollama.exe",
    "$env:ProgramFiles\Ollama\ollama.exe"
  ) | Where-Object { Test-Path $_ } | Select-Object -First 1
  if ($possible) { $ollama = $possible } else { $ollama = "ollama" }
} else {
  $ollama = (Get-Command ollama).Source
}

Write-Host "Downloading qwen2.5:0.5b..."
& $ollama pull qwen2.5:0.5b
Write-Host "Local AI ready. Endpoint: http://127.0.0.1:11434/v1" -ForegroundColor Green
