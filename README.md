# EmailAnalyzer — AI Destekli E-Posta Analiz ve Sınıflandırma Sistemi

Kurumsal yazılım (ERP, muhasebe/finans, e-dönüşüm, CRM, İK, raporlama/analitik, eğitim/danışmanlık) hizmetleri sunan bir şirketin ortak mail adresine gelen e-postaları otomatik okuyan, **OpenAI** ile analiz edip sınıflandıran, sonuçları MSSQL'e kaydeden ve Türkçe admin panelinde raporlayan bir sistem.

## Teknoloji

- .NET 8, C# — ASP.NET Core Web API + Razor Pages admin panel (tek proje)
- E-posta: **MailKit** (IMAP, Gmail App Password)
- AI: **OpenAI Chat Completions API** (`HttpClient`, SDK yok)
- ORM: **EF Core 8 + SQL Server** (code-first, migrations)
- Loglama: **Serilog** (console + günlük rolling file, `logs/`)
- Swagger/OpenAPI aktif

## Çözüm Yapısı

```
EmailAnalyzer.sln
├── src/
│   ├── EmailAnalyzer.Domain          # Entity, enum, DTO, servis arayüzleri
│   ├── EmailAnalyzer.Infrastructure  # DbContext, MailKit servisi, OpenAI client
│   └── EmailAnalyzer.Web             # Web API + Razor panel + BackgroundService
└── tests/
    └── EmailAnalyzer.Tests           # xUnit (AiResponseParser testleri)
```

Tek çalıştırılabilir proje: **EmailAnalyzer.Web**. Mail dinleyen worker bu projede hosted service olarak çalışır — `dotnet run` ile her şey ayağa kalkar.

## Kurulum

### 1. Gereksinimler
- .NET 8 SDK
- SQL Server (LocalDB / SQLEXPRESS / tam sürüm)
- Bir Gmail hesabı (IMAP açık) ve bir OpenAI API anahtarı

### 2. Gmail App Password alma
1. Gmail hesabında **2 Adımlı Doğrulama (2FA) açık olmalı**.
2. Google Hesabı → **Güvenlik** → **Uygulama şifreleri**.
3. 16 haneli şifreyi oluştur ve kopyala (boşluklu görünür, boşluklu da kullanılabilir).
4. Gmail'de **IMAP erişiminin açık** olduğundan emin ol (Ayarlar → Yönlendirme ve POP/IMAP).

### 3. OpenAI API anahtarı alma
- https://platform.openai.com/api-keys → **Create new secret key** (`sk-proj-...`).

### 4. Gizli anahtarları user-secrets'a koy
Anahtarlar **asla** koda/repoya yazılmaz; `dotnet user-secrets` ile tutulur:

```bash
cd src/EmailAnalyzer.Web
dotnet user-secrets set "OpenAi:ApiKey"      "sk-proj-..."
dotnet user-secrets set "Gmail:Email"        "adresiniz@gmail.com"
dotnet user-secrets set "Gmail:AppPassword"  "xxxx xxxx xxxx xxxx"
```

Kontrol: `dotnet user-secrets list`

### 5. Bağlantı dizesi
`src/EmailAnalyzer.Web/appsettings.json` içindeki `ConnectionStrings:Default` kendi SQL Server'ına göre ayarlanır (varsayılan: `.\SQLEXPRESS`).

### 6. Veritabanını oluştur

```bash
cd src/EmailAnalyzer.Web
dotnet ef database update
```

> `dotnet ef` yoksa: `dotnet tool install --global dotnet-ef`

### 7. Çalıştır

```bash
cd src/EmailAnalyzer.Web
dotnet run
```

- Admin panel: **http://localhost:5247/admin**
- Swagger: **http://localhost:5247/swagger**

Worker otomatik başlar, INBOX'taki okunmamış mailleri (her döngüde en fazla `MaxEmailsPerCycle` kadar) çeker, analiz eder, kaydeder ve `\Seen` işaretler.

## Yapılandırma (`appsettings.json`)

| Bölüm | Anahtar | Açıklama |
|---|---|---|
| Gmail | `PollingIntervalSeconds` | Worker'ın kontrol aralığı (varsayılan 60) |
| Gmail | `MaxEmailsPerCycle` | Bir döngüde işlenecek en fazla mail (varsayılan 20) |
| Gmail | `Folder` | Okunacak IMAP klasörü (varsayılan INBOX) |
| OpenAi | `Model` | Model adı (varsayılan `gpt-4o-mini`) |
| Analysis | `ConfidenceThreshold` | Altında insan incelemesi işaretlenir (varsayılan 0.70) |
| Analysis | `MaxBodyChars` | Analiz öncesi gövde kırpma sınırı (varsayılan 8000) |

## Test verisi üretme (mail göndermeden)

Gerçek mail göndermeden panele test verisi eklemek için:

```powershell
cd test-data
# ornek-mailler.txt dosyasına mailleri --- ile ayırarak yaz, sonra:
pwsh -File .\Seed-Emails.ps1
```

Script her maili `POST /api/emails/analyze?save=true` endpoint'ine gönderir, sonuçlar panele düşer.

## Testler

```bash
dotnet test
```

## API Endpoint'leri

| Metot | Yol | Açıklama |
|---|---|---|
| POST | `/api/emails/analyze?save=true` | Manuel analiz (+ opsiyonel kayıt) |
| GET | `/api/emails` | Sayfalı + filtreli liste |
| GET | `/api/emails/{id}` | Tek kayıt detayı |
| PUT | `/api/emails/{id}/review` | İnsan düzeltmesi |
| GET | `/api/dashboard/stats` | Dashboard istatistikleri |

Örnek istekler için `src/EmailAnalyzer.Web/EmailAnalyzer.Web.http` dosyasını kullanabilirsin (VS / VS Code'dan tıkla-çalıştır).
