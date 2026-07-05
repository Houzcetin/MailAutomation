<#
.SYNOPSIS
    Reads test emails from a text file and posts each to the /api/emails/analyze?save=true
    endpoint, so they are AI-analysed and saved to the database WITHOUT sending real mail.

.USAGE
    1. Uygulamayı çalıştır (Visual Studio F5 veya `dotnet run`).
    2. ornek-mailler.txt dosyasına test maillerini yapıştır (--- ile ayrılmış).
    3. Bu script'i çalıştır:  pwsh ./Seed-Emails.ps1
       Farklı dosya/port için:  pwsh ./Seed-Emails.ps1 -File "yol\dosya.txt" -BaseUrl "http://localhost:5247"
#>
param(
    [string]$File = "$PSScriptRoot\ornek-mailler.txt",
    [string]$BaseUrl = "http://localhost:5247"
)

if (-not (Test-Path $File)) {
    Write-Host "Dosya bulunamadı: $File" -ForegroundColor Red
    exit 1
}

$url = "$BaseUrl/api/emails/analyze?save=true"
Write-Host "Kaynak dosya : $File"
Write-Host "Hedef        : $url`n"

# Split the file into blocks on lines that are exactly "---".
$raw = Get-Content -Path $File -Raw -Encoding UTF8
$blocks = $raw -split "(?m)^\s*---\s*$"

$sent = 0
$failed = 0

foreach ($block in $blocks) {
    $text = $block.Trim()
    # Skip empty blocks and the "###" comment header.
    if ([string]::IsNullOrWhiteSpace($text) -or $text.StartsWith("###")) { continue }

    $senderName = $null
    $senderEmail = $null
    $subject = $null
    $bodyLines = @()
    $inBody = $false

    foreach ($line in ($text -split "`r?`n")) {
        if ($inBody) { $bodyLines += $line; continue }

        if ($line -match '^\s*GÖNDEREN ADI\s*:\s*(.+)$')      { $senderName  = $Matches[1].Trim(); continue }
        if ($line -match '^\s*GÖNDEREN E-POSTA\s*:\s*(.+)$')  { $senderEmail = $Matches[1].Trim(); continue }
        if ($line -match '^\s*KONU\s*:\s*(.+)$')              { $subject     = $Matches[1].Trim(); continue }
        if ($line -match '^\s*İÇERİK\s*:\s*(.*)$') {
            $inBody = $true
            if ($Matches[1].Trim()) { $bodyLines += $Matches[1] }
            continue
        }
    }

    $body = ($bodyLines -join "`n").Trim()

    if (-not $subject -or -not $body) {
        Write-Host "ATLANDI (konu/içerik eksik): $($text.Substring(0, [Math]::Min(40, $text.Length)))..." -ForegroundColor Yellow
        continue
    }

    $payload = @{
        subject     = $subject
        body        = $body
        senderEmail = $senderEmail
        senderName  = $senderName
    } | ConvertTo-Json

    try {
        $resp = Invoke-RestMethod -Uri $url -Method Post -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($payload))
        $a = $resp.analysis
        Write-Host ("OK  [{0}] {1} -> {2}/{3} (güven {4})" -f $resp.savedId, $subject, $a.mainCategory, $a.priority, $a.confidence) -ForegroundColor Green
        $sent++
    }
    catch {
        Write-Host "HATA: $subject -> $($_.Exception.Message)" -ForegroundColor Red
        $failed++
    }

    Start-Sleep -Milliseconds 400  # OpenAI'yi yormamak için küçük ara
}

Write-Host "`nTamamlandı. Gönderilen: $sent, Hata: $failed" -ForegroundColor Cyan
