# Soak smoke — money-path timed loop (P25)
# Usage (PowerShell): .\scripts\soak\money-path-soak.ps1 [-BaseUrl http://127.0.0.1:8080] [-Iterations 50]

param(
  [string]$BaseUrl = "http://127.0.0.1:8080",
  [int]$Iterations = 40
)

$ErrorActionPreference = "Stop"
$health = Invoke-RestMethod -Uri "$BaseUrl/health" -Method GET
if ($health.status -ne "ok") { throw "health not ok" }

$sw = [System.Diagnostics.Stopwatch]::StartNew()
$fail = 0
for ($i = 1; $i -le $Iterations; $i++) {
  try {
    $null = Invoke-RestMethod -Uri "$BaseUrl/api/terminology" -Method GET
    $null = Invoke-RestMethod -Uri "$BaseUrl/ready" -Method GET
  } catch {
    $fail++
  }
}
$sw.Stop()

Write-Host ("iterations={0} fail={1} elapsed_ms={2} avg_ms={3:N1}" -f `
  $Iterations, $fail, $sw.ElapsedMilliseconds, ($sw.ElapsedMilliseconds / [math]::Max(1, $Iterations)))

if ($fail -gt 0) { exit 1 }
