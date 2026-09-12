# UAT one round on VPS — Bill → Cost/Revenue → Doc/AP-AR → Settlement → Close
# Does not print secrets. Outputs step results + gaps as JSON lines.
$ErrorActionPreference = "Stop"
$Base = "http://194.233.89.26"
$Email = $env:CMS_UAT_EMAIL
$Password = $env:CMS_UAT_PASSWORD
if (-not $Email -or -not $Password) {
  throw "Set CMS_UAT_EMAIL and CMS_UAT_PASSWORD"
}

$gaps = [System.Collections.Generic.List[object]]::new()
$steps = [System.Collections.Generic.List[object]]::new()

function Step([string]$name, [scriptblock]$action) {
  try {
    $result = & $action
    $steps.Add([pscustomobject]@{ step = $name; ok = $true; detail = $result })
    Write-Host "OK  $name :: $result"
    return $result
  } catch {
    $msg = $_.Exception.Message
    $steps.Add([pscustomobject]@{ step = $name; ok = $false; detail = $msg })
    $gaps.Add([pscustomobject]@{ area = $name; severity = "blocker"; note = $msg })
    Write-Host "FAIL $name :: $msg"
    throw
  }
}

function NoteGap([string]$area, [string]$severity, [string]$note) {
  $gaps.Add([pscustomobject]@{ area = $area; severity = $severity; note = $note })
  Write-Host "GAP [$severity] $area :: $note"
}

function Invoke-Api {
  param(
    [string]$Method,
    [string]$Path,
    [object]$Body = $null,
    [string]$Token = $null,
    [hashtable]$Headers = @{}
  )
  $uri = "$Base$Path"
  $hdr = @{}
  foreach ($k in $Headers.Keys) { $hdr[$k] = $Headers[$k] }
  if ($Token) { $hdr["Authorization"] = "Bearer $Token" }
  $hdr["Accept"] = "application/json"
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
    if ($content -and $content.Trim().StartsWith("{") -or ($content -and $content.Trim().StartsWith("["))) {
      $parsed = $content | ConvertFrom-Json
    }
    return [pscustomobject]@{ Status = [int]$resp.StatusCode; Body = $parsed; Raw = $content }
  } catch {
    $ex = $_.Exception
    $status = $null
    $raw = $null
    if ($ex.Response) {
      $status = [int]$ex.Response.StatusCode
      $reader = New-Object System.IO.StreamReader($ex.Response.GetResponseStream())
      $raw = $reader.ReadToEnd()
    }
    throw "HTTP $status $Method $Path :: $raw"
  }
}

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$billNo = "UAT-$stamp"

$token = $null
Step "login" {
  $r = Invoke-Api POST "/api/auth/login" @{ email = $Email; password = $Password }
  if (-not $r.Body.accessToken) { throw "no accessToken" }
  $script:token = $r.Body.accessToken
  "ok (token redacted)"
} | Out-Null

$billId = Step "create_bill" {
  $r = Invoke-Api POST "/api/bills" @{ billNo = $billNo; billType = "freight" } -Token $token
  if (-not $r.Body.id) { throw "no bill id: $($r.Raw)" }
  $r.Body.id
}

$costId = Step "create_cost" {
  $r = Invoke-Api POST "/api/costs" @{
    billId = $billId
    attributionType = "direct"
    amount = 1000000
    currencyCode = "VND"
    costTypeCode = "FREIGHT"
  } -Token $token
  $r.Body.id
}

Step "confirm_cost" {
  $r = Invoke-Api POST "/api/costs/$costId/confirm" @{ confirmedAmount = 1100000 } -Token $token
  "status=$($r.Status)"
}

$revId = Step "create_revenue" {
  $r = Invoke-Api POST "/api/revenues" @{
    billId = $billId
    amount = 2500000
    currencyCode = "VND"
    revenueTypeCode = "FREIGHT"
    serviceDate = "2026-09-10"
  } -Token $token
  if (-not $r.Body.id) { throw "no revenue id: $($r.Raw)" }
  $r.Body.id
}

Step "confirm_revenue" {
  $r = Invoke-Api POST "/api/revenues/$revId/confirm" @{ confirmedAmount = 2500000 } -Token $token
  "status=$($r.Status)"
}

$profile = Step "financial_profile" {
  $r = Invoke-Api GET "/api/bills/$billId/financial-profile" -Token $token
  $vnd = $r.Body.byCurrency | Select-Object -First 1
  "costBA=$($vnd.costBestAvailable) revBA=$($vnd.revenueBestAvailable) profit=$($vnd.profitBestAvailable)"
}

$docId = Step "receive_document" {
  $r = Invoke-Api POST "/api/financial-documents" @{
    documentType = "invoice"
    documentNo = "INV-$stamp"
    direction = "payable"
    totalAmount = 1100000
    currencyCode = "VND"
    billId = $billId
  } -Token $token
  $r.Body.id
}

$lineId = Step "add_document_line" {
  $r = Invoke-Api POST "/api/financial-documents/$docId/lines" @{
    amount = 1100000
    description = "cuoc van chuyen UAT"
  } -Token $token
  $r.Body.id
}

Step "accept_document" {
  $r = Invoke-Api POST "/api/financial-documents/$docId/accept" -Token $token
  "status=$($r.Status)"
}

$matchId = Step "start_match" {
  $r = Invoke-Api POST "/api/document-matches" @{
    primaryDocumentId = $docId
    matchMethod = "line_to_cost"
  } -Token $token
  $r.Body.id
}

Step "match_detail" {
  $r = Invoke-Api POST "/api/document-matches/$matchId/details" @{
    sourceLineId = $lineId
    targetCostId = $costId
    matchedAmount = 1100000
  } -Token $token
  "status=$($r.Status) raw=$($r.Raw)"
}

$apExpId = Step "payable_exposure" {
  $r = Invoke-Api POST "/api/payable-exposures" @{
    amount = 1100000
    currencyCode = "VND"
    billId = $billId
  } -Token $token
  $r.Body.id
}

$apId = Step "recognize_ap" {
  $r = Invoke-Api POST "/api/payable-exposures/$apExpId/recognize" @{ amount = 1100000 } -Token $token
  $r.Body.id
}

$arExpId = Step "receivable_exposure" {
  $r = Invoke-Api POST "/api/receivable-exposures" @{
    amount = 2500000
    currencyCode = "VND"
    billId = $billId
  } -Token $token
  $r.Body.id
}

$arId = Step "recognize_ar" {
  $r = Invoke-Api POST "/api/receivable-exposures/$arExpId/recognize" @{ amount = 2500000 } -Token $token
  $r.Body.id
}

$paymentId = Step "create_payment" {
  $r = Invoke-Api POST "/api/payments" @{
    amount = 1100000
    currencyCode = "VND"
    billId = $billId
  } -Token $token
  $r.Body.id
}

$payAllocId = Step "allocate_payment" {
  $r = Invoke-Api POST "/api/payments/$paymentId/allocations" @{
    accountsPayableId = $apId
    amount = 1100000
  } -Token $token
  $r.Body.id
}

Step "finalize_payment" {
  $r = Invoke-Api POST "/api/payment-allocations/$payAllocId/finalize" -Token $token
  "status=$($r.Status)"
}

$collectionId = Step "create_collection" {
  $r = Invoke-Api POST "/api/collections" @{
    amount = 2500000
    currencyCode = "VND"
    billId = $billId
  } -Token $token
  $r.Body.id
}

$colAllocId = Step "allocate_collection" {
  $r = Invoke-Api POST "/api/collections/$collectionId/allocations" @{
    accountsReceivableId = $arId
    amount = 2500000
  } -Token $token
  $r.Body.id
}

Step "finalize_collection" {
  $r = Invoke-Api POST "/api/collection-allocations/$colAllocId/finalize" -Token $token
  "status=$($r.Status)"
}

$closeId = $null
try {
  $closeId = Step "start_close" {
    $r = Invoke-Api POST "/api/financial-closes" @{
      scopeType = "bill"
      scopeId = $billId
      periodFrom = "2026-09-01"
      periodTo = "2026-09-30"
      policyVersion = "controlled"
    } -Token $token
    if (-not $r.Body.id) { throw "no close id: $($r.Raw)" }
    $r.Body.id
  }
} catch {
  NoteGap "close.start" "blocker" $_.Exception.Message
}

if ($closeId) {
  try {
    $snapId = Step "close_snapshot" {
      $r = Invoke-Api POST "/api/financial-closes/$closeId/snapshot" -Token $token
      if (-not $r.Body.id) { throw "no snapshot: $($r.Raw)" }
      $r.Body.id
    }
    Step "close_pnl" {
      $r = Invoke-Api GET "/api/financial-closes/$closeId/pnl" -Token $token
      "pnl keys=$([string]::Join(',', @($r.Body.PSObject.Properties.Name)))"
    }
  } catch {
    NoteGap "close.snapshot" "blocker" $_.Exception.Message
  }
}

# UI route smoke (auth-less expects redirect/login or 200)
foreach ($path in @("/", "/bills", "/documents", "/ap-ar", "/settlements", "/financial-closes", "/dashboard")) {
  try {
    $resp = Invoke-WebRequest -Uri "$Base$path" -MaximumRedirection 0 -ErrorAction SilentlyContinue -UseBasicParsing
    $code = [int]$resp.StatusCode
  } catch {
    if ($_.Exception.Response) { $code = [int]$_.Exception.Response.StatusCode } else { $code = 0 }
  }
  $ok = $code -in 200, 307, 302, 303
  $steps.Add([pscustomobject]@{ step = "ui$path"; ok = $ok; detail = "http=$code" })
  Write-Host "$(if($ok){'OK'}else{'FAIL'})  ui$path :: http=$code"
  if (-not $ok) { NoteGap "ui$path" "major" "unexpected http $code" }
}

$out = [pscustomobject]@{
  whenUtc = (Get-Date).ToUniversalTime().ToString("o")
  billNo = $billNo
  billId = $billId
  steps = $steps
  gaps = $gaps
}
$outPath = "c:\A1\git\cms\docs\sprint\UAT-VPS-ONE-ROUND-RESULT.json"
$out | ConvertTo-Json -Depth 6 | Set-Content -Path $outPath -Encoding UTF8
Write-Host "Wrote $outPath gaps=$($gaps.Count)"
