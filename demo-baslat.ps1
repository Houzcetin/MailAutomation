# ============================================================================
#  EmailAnalyzer - Demo Baslatma Scripti
# ----------------------------------------------------------------------------
#  Ne yapar:
#    1) cloudflared kurulu mu bakar; degilse winget ile kurar.
#    2) Uygulamayi http://localhost:5247 uzerinde baslatir.
#    3) Uygulama hazir olana kadar bekler.
#    4) Gecici bir genel link (https://*.trycloudflare.com) uretir ve
#       ekranda gosterir. Bu linki karsi tarafa gonder.
#
#  Kullanim: Bu dosyaya (ya da demo-baslat.cmd'ye) cift tikla.
#            Kapatmak icin bu pencerede Ctrl+C yap ya da pencereyi kapat.
#
#  Not: Karsi taraf HICBIR sey kurmaz, sadece gonderdigin linke tiklar.
# ============================================================================

$ErrorActionPreference = 'Stop'
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$Port      = 5247
$LocalUrl  = "http://localhost:$Port"
$WebProj   = Join-Path $ScriptDir 'src\EmailAnalyzer.Web'

function Write-Header($text) {
    Write-Host ""
    Write-Host "==================================================================" -ForegroundColor Cyan
    Write-Host "  $text" -ForegroundColor Cyan
    Write-Host "==================================================================" -ForegroundColor Cyan
}

# --- Temizlik: script kapaninca alt islemleri de durdur ---------------------
$script:AppProc    = $null
$script:TunnelProc = $null

function Stop-All {
    Write-Host ""
    Write-Host "Kapatiliyor..." -ForegroundColor Yellow
    foreach ($p in @($script:TunnelProc, $script:AppProc)) {
        if ($p -and -not $p.HasExited) {
            try { $p.Kill($true) } catch {}
        }
    }
}

try {
    Write-Header "EmailAnalyzer Demo Baslatiliyor"

    # --- 1) cloudflared var mi? -------------------------------------------
    # PATH'te ya da bilinen kurulum yollarinda cloudflared arar.
    function Find-Cloudflared {
        $cmd = Get-Command cloudflared -ErrorAction SilentlyContinue
        if ($cmd) { return $cmd.Source }
        $candidates = @(
            "${env:ProgramFiles(x86)}\cloudflared\cloudflared.exe",
            "$env:ProgramFiles\cloudflared\cloudflared.exe",
            "$env:LOCALAPPDATA\Microsoft\WinGet\Links\cloudflared.exe"
        )
        foreach ($c in $candidates) {
            if ($c -and (Test-Path $c)) { return $c }
        }
        # WinGet paket klasoru (surum numarasi degisebilir)
        $pkg = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Cloudflare.cloudflared_*\cloudflared.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($pkg) { return $pkg.FullName }
        return $null
    }

    $CloudflaredExe = Find-Cloudflared
    if (-not $CloudflaredExe) {
        Write-Host "cloudflared bulunamadi. winget ile kuruluyor (bir kerelik)..." -ForegroundColor Yellow
        winget install --id Cloudflare.cloudflared --accept-source-agreements --accept-package-agreements -e
        $CloudflaredExe = Find-Cloudflared
        if (-not $CloudflaredExe) {
            throw "cloudflared kurulamadi. Lutfen https://github.com/cloudflare/cloudflared adresinden manuel kurun."
        }
    }
    Write-Host "cloudflared hazir: $CloudflaredExe" -ForegroundColor Green

    # --- 2) Uygulamayi baslat ---------------------------------------------
    Write-Host "Uygulama baslatiliyor ($LocalUrl) ..." -ForegroundColor Yellow
    $script:AppProc = Start-Process -FilePath 'dotnet' `
        -ArgumentList @('run', '--project', $WebProj, '--launch-profile', 'http') `
        -PassThru -NoNewWindow

    # --- 3) Uygulama hazir olana kadar bekle ------------------------------
    Write-Host "Uygulamanin hazir olmasi bekleniyor..." -NoNewline
    $ready = $false
    for ($i = 0; $i -lt 60; $i++) {
        if ($script:AppProc.HasExited) {
            throw "Uygulama beklenmedik sekilde kapandi. (dotnet run hatasi - user-secrets / SQL Server / EF migration kontrol edin.)"
        }
        try {
            $resp = Invoke-WebRequest -Uri "$LocalUrl/admin" -UseBasicParsing -TimeoutSec 3
            if ($resp.StatusCode -ge 200) { $ready = $true; break }
        } catch {
            Start-Sleep -Seconds 2
            Write-Host "." -NoNewline
        }
    }
    Write-Host ""
    if (-not $ready) {
        throw "Uygulama 2 dakikada hazir olmadi. Loglari kontrol edin."
    }
    Write-Host "Uygulama hazir." -ForegroundColor Green

    # --- 4) Tuneli ac ve linki yakala -------------------------------------
    Write-Host "Genel link olusturuluyor..." -ForegroundColor Yellow
    $logFile = Join-Path $env:TEMP "cloudflared-demo-$PID.log"
    if (Test-Path $logFile) { Remove-Item $logFile -Force }

    $script:TunnelProc = Start-Process -FilePath $CloudflaredExe `
        -ArgumentList @('tunnel', '--url', $LocalUrl) `
        -PassThru -NoNewWindow `
        -RedirectStandardError $logFile -RedirectStandardOutput "$logFile.out"

    # cloudflared linki stderr'e yazar; log dosyasindan yakala.
    $publicUrl = $null
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Seconds 1
        if (Test-Path $logFile) {
            $match = Select-String -Path $logFile -Pattern 'https://[a-zA-Z0-9-]+\.trycloudflare\.com' -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($match) {
                $publicUrl = ($match.Matches[0].Value)
                break
            }
        }
        if ($script:TunnelProc.HasExited) {
            throw "cloudflared beklenmedik sekilde kapandi. Log: $logFile"
        }
    }

    if (-not $publicUrl) {
        throw "Genel link yakalanamadi. Log dosyasi: $logFile"
    }

    # --- Linki goster ------------------------------------------------------
    Write-Host ""
    Write-Host "##################################################################" -ForegroundColor Green
    Write-Host "#" -ForegroundColor Green
    Write-Host "#   DEMO LINKI HAZIR - bu linki karsi tarafa gonder:" -ForegroundColor Green
    Write-Host "#" -ForegroundColor Green
    Write-Host "#   $publicUrl" -ForegroundColor White -BackgroundColor DarkGreen
    Write-Host "#" -ForegroundColor Green
    Write-Host "##################################################################" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Karsi taraf sadece bu linke tiklar - baska bir sey yapmasi gerekmez." -ForegroundColor Gray
    Write-Host "  Bilgisayarin acik ve bu pencere acik kaldigi surece link calisir." -ForegroundColor Gray
    Write-Host "  Bitirmek icin: bu pencerede Ctrl+C yap ya da pencereyi kapat." -ForegroundColor Gray
    Write-Host ""

    # Linki panoya da kopyala (kolay paylasim icin).
    try { $publicUrl | Set-Clipboard; Write-Host "  (Link panoya kopyalandi.)" -ForegroundColor Gray } catch {}

    # --- Bekle: kullanici kapatana kadar calis ----------------------------
    Write-Host "Demo calisiyor. Bu pencereyi kapatma." -ForegroundColor Cyan
    while (-not $script:TunnelProc.HasExited -and -not $script:AppProc.HasExited) {
        Start-Sleep -Seconds 2
    }
    Write-Host "Alt islemlerden biri kapandi, demo sonlaniyor." -ForegroundColor Yellow
}
catch {
    Write-Host ""
    Write-Host "HATA: $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    Stop-All
    Write-Host ""
    Write-Host "Cikmak icin Enter'a bas..." -ForegroundColor Gray
    try { Read-Host | Out-Null } catch {}
}
