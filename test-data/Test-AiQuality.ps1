<#
.SYNOPSIS
    AI kalite test matrisi çalıştırıcı. Her test mailini /api/emails/analyze (KAYDETMEDEN)
    endpoint'ine gönderir, dönen AI sonucunu dosyadaki BEKLENEN değerlerle karşılaştırır ve
    kategori/öncelik başarı oranını raporlar.

.USAGE
    Uygulama çalışırken:  pwsh ./Test-AiQuality.ps1
    Farklı dosya/port:    pwsh ./Test-AiQuality.ps1 -File "yol.txt" -BaseUrl "http://localhost:5247"
#>
param(
    [string]$File = "$PSScriptRoot\ai-test-matrisi.txt",
    [string]$BaseUrl = "http://localhost:5247"
)

if (-not (Test-Path $File)) { Write-Host "Dosya yok: $File" -ForegroundColor Red; exit 1 }

# analyze endpoint — save=true YOK, yani DB'yi kirletmez.
$url = "$BaseUrl/api/emails/analyze"
$raw = Get-Content -Path $File -Raw -Encoding UTF8
$blocks = $raw -split "(?m)^\s*---\s*$"

$total = 0
$catOk = 0
$priOk = 0
$reviewChecked = 0
$reviewOk = 0
$rows = @()

foreach ($block in $blocks) {
    $text = $block.Trim()
    if ([string]::IsNullOrWhiteSpace($text) -or $text.StartsWith("###")) { continue }

    $subject = $null; $bodyLines = @(); $inBody = $false
    $expCat = $null; $expPri = $null; $expReview = $null

    foreach ($line in ($text -split "`r?`n")) {
        if ($line -match '^\s*BEKLENEN_KATEGORI\s*:\s*(.+)$') { $expCat = $Matches[1].Trim(); $inBody = $false; continue }
        if ($line -match '^\s*BEKLENEN_ONCELIK\s*:\s*(.+)$')  { $expPri = $Matches[1].Trim(); $inBody = $false; continue }
        if ($line -match '^\s*BEKLENEN_REVIEW\s*:\s*(.+)$')   { $expReview = $Matches[1].Trim(); $inBody = $false; continue }
        if ($line -match '^\s*KONU\s*:\s*(.+)$')              { $subject = $Matches[1].Trim(); $inBody = $false; continue }
        if ($line -match '^\s*İÇERİK\s*:\s*(.*)$') {
            $inBody = $true
            if ($Matches[1].Trim()) { $bodyLines += $Matches[1] }
            continue
        }
        if ($inBody) { $bodyLines += $line }
    }

    $body = ($bodyLines -join "`n").Trim()
    if (-not $subject -or -not $body) { continue }

    $payload = @{ subject = $subject; body = $body } | ConvertTo-Json

    try {
        $resp = Invoke-RestMethod -Uri $url -Method Post -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($payload))
        $a = $resp.analysis
    }
    catch {
        Write-Host "HATA: $subject -> $($_.Exception.Message)" -ForegroundColor Red
        continue
    }

    $total++
    $catExpected = $expCat -split '\|' | ForEach-Object { $_.Trim() }
    $priExpected = $expPri -split '\|' | ForEach-Object { $_.Trim() }

    $cOk = $catExpected -contains "$($a.mainCategory)"
    $pOk = (-not $expPri) -or ($priExpected -contains "$($a.priority)")
    if ($cOk) { $catOk++ }
    if ($pOk) { $priOk++ }

    $rOk = $true
    if ($expReview) {
        $reviewChecked++
        $rOk = ("$($a.requiresHumanReview)".ToLower() -eq $expReview.ToLower())
        if ($rOk) { $reviewOk++ }
    }

    $mark = if ($cOk -and $pOk -and $rOk) { "OK " } else { "FAIL" }
    $color = if ($mark -eq "OK ") { "Green" } else { "Yellow" }
    $catMark = if ($cOk) { "✓" } else { "✗(bek: $expCat)" }
    $priMark = if ($pOk) { "✓" } else { "✗(bek: $expPri)" }

    Write-Host ("[{0}] {1}" -f $mark, $subject.Substring(0, [Math]::Min(48, $subject.Length))) -ForegroundColor $color
    Write-Host ("       kategori: {0} {1} | öncelik: {2} {3} | güven: {4}" -f $a.mainCategory, $catMark, $a.priority, $priMark, $a.confidence) -ForegroundColor Gray

    Start-Sleep -Milliseconds 300
}

Write-Host ""
Write-Host "==================== SONUÇ ====================" -ForegroundColor Cyan
Write-Host ("Toplam mail        : {0}" -f $total)
Write-Host ("Kategori doğru     : {0}/{1}  (%{2})" -f $catOk, $total, [Math]::Round(100*$catOk/[Math]::Max(1,$total)))
Write-Host ("Öncelik doğru      : {0}/{1}  (%{2})" -f $priOk, $total, [Math]::Round(100*$priOk/[Math]::Max(1,$total)))
if ($reviewChecked -gt 0) {
    Write-Host ("Review doğru       : {0}/{1}  (%{2})" -f $reviewOk, $reviewChecked, [Math]::Round(100*$reviewOk/$reviewChecked))
}
Write-Host "==============================================" -ForegroundColor Cyan
