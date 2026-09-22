# P04 UAT - Strict close + period lock stress on VPS (AC-008 / ADR-0010)
# Does not print secrets. Writes result JSON (token redacted).
# Match period-lock errors via VI «khóa chốt» / code period_locked (UX-07).
$ErrorActionPreference = "Stop"
$Base = if ($env:CMS_UAT_BASE) { $env:CMS_UAT_BASE } else { "http://194.233.89.26" }
$Email = $env:CMS_UAT_EMAIL
$Password = $env:CMS_UAT_PASSWORD
if (-not $Email -or -not $Password) {
  throw "Set CMS_UAT_EMAIL and CMS_UAT_PASSWORD (never commit secrets)."
}

$steps = [System.Collections.Generic.List[object]]::new()
$failed = $false

function Record([string]$name, [bool]$ok, [string]$detail) {
  $steps.Add([pscustomobject]@{ step = $name; ok = $ok; detail = $detail })
  if ($ok) { Write-Host "OK  $name :: $detail" }
  else {
    Write-Host "FAIL $name :: $detail"
    $script:failed = $true
  }
}

function Test-PeriodLockMessage([string]$msg) {
  return [bool]($msg -match '(?i)khóa chốt|period[_\s-]?lock|period_locked')
}

function Invoke-Api {
  param(
    [string]$Method,
    [string]$Path,
    [object]$Body = $null,
    [string]$Token = $null,
    [int[]]$AllowStatuses = @()
  )
  $uri = "$Base$Path"
  $hdr = @{ Accept = "application/json" }
  if ($Token) { $hdr["Authorization"] = "Bearer $Token" }
  $json = $null
  if ($null -ne $Body) { $json = ($Body | ConvertTo-Json -Depth 8 -Compress) }
  try {
    if ($json) {
      $resp = Invoke-WebRequest -Uri $uri -Method $Method -Headers $hdr -ContentType "application/json" -Body $json -UseBasicParsing
    } else {
      $resp = Invoke-WebRequest -Uri $uri -Method $Method -Headers $hdr -UseBasicParsing
    }
    $content = $resp.Content
    $parsed = $null
    if ($content -and ($content.Trim().StartsWith("{") -or $content.Trim().StartsWith("["))) {
      $parsed = $content | ConvertFrom-Json
    }
    return [pscustomobject]@{ Status = [int]$resp.StatusCode; Body = $parsed; Raw = $content }
  } catch {
    $ex = $_.Exception
    $status = 0
    $raw = $ex.Message
    if ($ex.Response) {
      $status = [int]$ex.Response.StatusCode
      try {
        $reader = New-Object System.IO.StreamReader($ex.Response.GetResponseStream())
        $raw = $reader.ReadToEnd()
      } catch { }
    }
    if ($AllowStatuses -contains $status) {
      $parsed = $null
      try { if ($raw) { $parsed = $raw | ConvertFrom-Json } } catch { }
      return [pscustomobject]@{ Status = $status; Body = $parsed; Raw = $raw }
    }
    throw "HTTP $status $Method $Path :: $raw"
  }
}

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$billNo = "P04-STRICT-$stamp"
$token = $null

try {
  $login = Invoke-Api POST "/api/auth/login" @{ email = $Email; password = $Password }
  if (-not $login.Body.accessToken) { throw "no accessToken" }
  $token = $login.Body.accessToken
  Record "login" $true "ok (token redacted)"
} catch {
  Record "login" $false $_.Exception.Message
  throw
}

$billId = $null
$costId = $null
$revId = $null
$closeId = $null
$snap1Id = $null
$hash1 = $null

try {
  $r = Invoke-Api POST "/api/bills" @{ billNo = $billNo; billType = "freight" } -Token $token
  $billId = $r.Body.id
  if (-not $billId) { throw "no bill id" }
  Record "create_bill" $true $billNo
} catch {
  Record "create_bill" $false $_.Exception.Message
  throw
}

try {
  $r = Invoke-Api POST "/api/costs" @{
    billId = $billId
    attributionType = "direct"
    amount = 1000000
    currencyCode = "VND"
    costTypeCode = "P04-FREIGHT"
  } -Token $token
  $costId = $r.Body.id
  Record "create_cost_expected" $true $costId
} catch {
  Record "create_cost_expected" $false $_.Exception.Message
  throw
}

try {
  $r = Invoke-Api POST "/api/revenues" @{
    billId = $billId
    amount = 1500000
    currencyCode = "VND"
    revenueTypeCode = "P04-FREIGHT"
  } -Token $token
  $revId = $r.Body.id
  Record "create_revenue_expected" $true $revId
} catch {
  Record "create_revenue_expected" $false $_.Exception.Message
  throw
}

try {
  $r = Invoke-Api POST "/api/financial-closes" @{
    scopeType = "bill"
    scopeId = $billId
    periodFrom = $null
    periodTo = $null
    policyVersion = "strict"
    baseCurrency = "VND"
    notes = "P04 Strict period-lock UAT $stamp"
    supersedesCloseId = $null
  } -Token $token
  $closeId = $r.Body.id
  if (-not $closeId) { throw "no close id" }
  Record "start_close_strict" $true "closeId=$closeId policy=strict"
} catch {
  Record "start_close_strict" $false $_.Exception.Message
  throw
}

try {
  $r = Invoke-Api POST "/api/financial-closes/$closeId/snapshot" -Token $token
  $snap1Id = $r.Body.id
  if (-not $snap1Id) { throw "no snapshot id: $($r.Raw)" }
  $close = (Invoke-Api GET "/api/financial-closes/$closeId" -Token $token).Body
  $snap = (Invoke-Api GET "/api/financial-close-snapshots/$snap1Id" -Token $token).Body
  $hash1 = $snap.immutableHash
  $ok = ($close.status -eq "locked") -and ($close.policyVersion -eq "strict") -and ($snap.policyVersion -eq "strict") -and $hash1
  Record "snapshot_lock_strict" $ok "status=$($close.status) policy=$($close.policyVersion) snapPolicy=$($snap.policyVersion) hashLen=$($hash1.Length)"
  if (-not $ok) { throw "strict lock invariants failed" }
} catch {
  Record "snapshot_lock_strict" $false $_.Exception.Message
  throw
}

try {
  $r = Invoke-Api POST "/api/costs/$costId/confirm" @{ confirmedAmount = 1000000 } -Token $token -AllowStatuses @(409)
  $msg = if ($r.Body.message) { [string]$r.Body.message } else { [string]$r.Raw }
  $ok = ($r.Status -eq 409) -and (Test-PeriodLockMessage $msg)
  Record "period_lock_blocks_confirm_cost" $ok "HTTP $($r.Status) periodLockMarker=$(Test-PeriodLockMessage $msg)"
  if (-not $ok) { throw "expected 409 period lock on cost confirm :: $msg" }
} catch {
  Record "period_lock_blocks_confirm_cost" $false $_.Exception.Message
  throw
}

try {
  $r = Invoke-Api POST "/api/revenues/$revId/confirm" @{ confirmedAmount = 1500000 } -Token $token -AllowStatuses @(409)
  $msg = if ($r.Body.message) { [string]$r.Body.message } else { [string]$r.Raw }
  $ok = ($r.Status -eq 409) -and (Test-PeriodLockMessage $msg)
  Record "period_lock_blocks_confirm_revenue" $ok "HTTP $($r.Status) periodLockMarker=$(Test-PeriodLockMessage $msg)"
  if (-not $ok) { throw "expected 409 period lock on revenue confirm :: $msg" }
} catch {
  Record "period_lock_blocks_confirm_revenue" $false $_.Exception.Message
  throw
}

$paymentId = $null
$apId = $null
try {
  $exp = Invoke-Api POST "/api/payable-exposures" @{
    amount = 400000
    currencyCode = "VND"
    billId = $billId
  } -Token $token
  $ap = Invoke-Api POST "/api/payable-exposures/$($exp.Body.id)/recognize" @{ amount = 400000 } -Token $token
  $apId = $ap.Body.id
  $pay = Invoke-Api POST "/api/payments" @{
    amount = 400000
    currencyCode = "VND"
    billId = $billId
  } -Token $token
  $paymentId = $pay.Body.id
  $r = Invoke-Api POST "/api/payments/$paymentId/allocations" @{
    accountsPayableId = $apId
    amount = 400000
  } -Token $token -AllowStatuses @(409)
  $msg = if ($r.Body.message) { [string]$r.Body.message } else { [string]$r.Raw }
  $ok = ($r.Status -eq 409) -and (Test-PeriodLockMessage $msg)
  Record "period_lock_blocks_allocate" $ok "HTTP $($r.Status) periodLockMarker=$(Test-PeriodLockMessage $msg)"
  if (-not $ok) { throw "expected 409 period lock on allocate :: $msg" }
} catch {
  Record "period_lock_blocks_allocate" $false $_.Exception.Message
  throw
}

try {
  Invoke-Api POST "/api/financial-closes/$closeId/reopen" @{ reason = "P04 reopen after lock stress" } -Token $token | Out-Null
  $close = (Invoke-Api GET "/api/financial-closes/$closeId" -Token $token).Body
  $snap = (Invoke-Api GET "/api/financial-close-snapshots/$snap1Id" -Token $token).Body
  $ok = ($close.status -eq "reopened") -and ($snap.immutableHash -eq $hash1)
  Record "reopen_snapshot_immutable" $ok "status=$($close.status) hashUnchanged=$($snap.immutableHash -eq $hash1)"
  if (-not $ok) { throw "reopen/immutable failed" }
} catch {
  Record "reopen_snapshot_immutable" $false $_.Exception.Message
  throw
}

try {
  $r = Invoke-Api POST "/api/costs/$costId/confirm" @{ confirmedAmount = 1000000 } -Token $token
  Record "confirm_cost_after_reopen" ($r.Status -eq 204 -or $r.Status -eq 200) "HTTP $($r.Status)"
} catch {
  Record "confirm_cost_after_reopen" $false $_.Exception.Message
  throw
}

try {
  $alloc = Invoke-Api POST "/api/payments/$paymentId/allocations" @{
    accountsPayableId = $apId
    amount = 400000
  } -Token $token
  $allocId = $alloc.Body.id
  Invoke-Api POST "/api/payment-allocations/$allocId/finalize" -Token $token | Out-Null
  Record "allocate_finalize_after_reopen" $true "allocId=$allocId"
} catch {
  Record "allocate_finalize_after_reopen" $false $_.Exception.Message
  throw
}

try {
  $r = Invoke-Api POST "/api/financial-closes/$closeId/snapshot" -Token $token
  $snap2Id = $r.Body.id
  $snap1 = (Invoke-Api GET "/api/financial-close-snapshots/$snap1Id" -Token $token).Body
  $snap2 = (Invoke-Api GET "/api/financial-close-snapshots/$snap2Id" -Token $token).Body
  $ok = ($snap2Id -ne $snap1Id) -and ($snap1.immutableHash -eq $hash1) -and ($snap1.snapshotVersion -eq 1) -and ($snap2.snapshotVersion -eq 2)
  Record "reclose_appends_v2" $ok "snap1v=$($snap1.snapshotVersion) snap2v=$($snap2.snapshotVersion) hash1stable=$($snap1.immutableHash -eq $hash1)"
  if (-not $ok) { throw "reclose append failed" }
} catch {
  Record "reclose_appends_v2" $false $_.Exception.Message
  throw
}

$outPath = Join-Path $PSScriptRoot "..\docs\sprint\P04-UAT-VPS-RESULT.json"
$result = [pscustomobject]@{
  runAt = (Get-Date).ToString("o")
  base = $Base
  billNo = $billNo
  billId = $billId
  closeId = $closeId
  snapshotV1Id = $snap1Id
  verdict = $(if ($failed) { "FAIL" } else { "PASS" })
  steps = $steps
}
$result | ConvertTo-Json -Depth 6 | Set-Content -Path $outPath -Encoding UTF8
Write-Host ""
Write-Host "VERDICT: $($result.verdict)"
Write-Host "RESULT: $outPath"
if ($failed) { exit 1 }
exit 0
