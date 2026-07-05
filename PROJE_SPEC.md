# GÖREV: AI Destekli E-Posta Analiz ve Sınıflandırma Sistemi

Sen bu projeyi sıfırdan geliştirecek kıdemli bir .NET geliştiricisisin. Aşağıdaki spesifikasyonu uygula.

**Çalışma şekli:** Önce kısa bir uygulama planı çıkar ve onayımı bekle. Onaydan sonra Faz 1'den başla. Her fazın sonunda `dotnet build` çalıştır, hataları düzelt, bana 2-3 cümlelik özet ver ve bir sonraki faza geçmeden onayımı al.

---

## 1. Projenin Amacı

Kurumsal yazılım (ERP, muhasebe/finans, e-dönüşüm, CRM, İK, raporlama/analitik, eğitim/danışmanlık) hizmetleri sunan bir şirketin ortak mail adresine gelen e-postaları otomatik okuyan, OpenAI API ile analiz edip sınıflandıran, sonuçları MSSQL veritabanına kaydeden ve admin panelinde raporlayan bir sistem. Amaç: gelen mailleri manuel inceleme ihtiyacını azaltmak, doğru departmana yönlendirmek, tüm mail trafiğini analiz edilebilir hale getirmek.

## 2. Teknoloji Stack (değiştirme)

- .NET 8, C#
- ASP.NET Core Web API + Razor Pages admin panel (aynı projede)
- E-posta çekme: **MailKit** (IMAP, Gmail App Password ile)
- AI: **OpenAI Chat Completions API** — `POST https://api.openai.com/v1/chat/completions`, `HttpClient` + `IHttpClientFactory` ile (SDK kullanma)
- ORM: **EF Core 8 + SQL Server** (code-first, migrations)
- Loglama: Serilog (console + günlük rolling file)
- Swagger/OpenAPI aktif

## 3. Çözüm Yapısı

```
EmailAnalyzer.sln
└── src/
    ├── EmailAnalyzer.Domain          # Entity'ler, enum'lar, servis arayüzleri (bağımlılıksız)
    ├── EmailAnalyzer.Infrastructure  # DbContext, EF konfig, MailKit servisi, OpenAI API client
    └── EmailAnalyzer.Web             # Web API + Razor admin panel + BackgroundService
```

Tek çalıştırılabilir proje: `EmailAnalyzer.Web`. Mail dinleyen BackgroundService bu projede hosted service olarak koşar (`dotnet run` ile her şey ayağa kalkar).

## 4. Veri Modeli — tablo `EmailMessages`

| Alan | Tip | Açıklama |
|---|---|---|
| Id | int, PK, identity | |
| MessageId | nvarchar(300) | IMAP Message-Id header. **Unique index** — aynı mail iki kez işlenmez |
| SenderEmail | nvarchar(320) | |
| SenderName | nvarchar(200), null | |
| Subject | nvarchar(500) | |
| Body | nvarchar(max) | Plain text'e çevrilmiş içerik |
| ReceivedDate | datetime2 | |
| MainCategory | nvarchar(50) | Enum, string conversion (aşağıda) |
| SubCategory | nvarchar(300) | AI'ın ürettiği spesifik konu (3-6 kelime, Türkçe) |
| Priority | nvarchar(20) | Enum: Low, Medium, High, Critical |
| Sentiment | nvarchar(20) | Enum: Positive, Neutral, Negative, Angry, Urgent |
| Summary | nvarchar(1000) | 1-2 cümlelik Türkçe özet |
| AiConfidenceScore | decimal(4,3) | 0-1 arası |
| IsProcessed | bit | AI analizi başarılıysa true |
| ProcessedDate | datetime2, null | |
| RequiresHumanReview | bit | |
| AiRawResponse | nvarchar(max), null | Modelin ham JSON cevabı (debug için) |
| CreatedDate | datetime2 | UTC, kayıt anı |

Ek indexler: ReceivedDate, MainCategory, Priority, RequiresHumanReview.

## 5. Kategoriler (AI system promptuna aynen gömülecek)

`MainCategory` enum değerleri ve tanımları:

1. **ERP** — stok yönetimi, satın alma, satış yönetimi, üretim planlama, depo yönetimi, sipariş yönetimi, tedarik zinciri, ürün yönetimi, cari hesap entegrasyonu, fatura bağlantısı, ERP modül talebi, ERP hata bildirimi, ERP danışmanlığı.
2. **MuhasebeFinans** — gelir-gider takibi, cari hesap, banka/kasa işlemleri, fatura, tahsilat, ödeme, borç-alacak takibi, mali raporlar, finansal analiz, bütçe yönetimi, muhasebe entegrasyonu, muhasebe kayıt hatası, finansal süreç talepleri.
3. **EDonusum** — e-fatura, e-arşiv, e-defter, e-irsaliye, e-mutabakat, e-SMM, e-dönüşüm entegrasyonu, GİB bağlantısı, mali mühür, e-belge gönderim hatası, e-fatura iptal / kabul-red süreçleri.
4. **CRM** — müşteri ilişkileri, müşteri kaydı, potansiyel müşteri takibi, satış fırsatı, müşteri şikayeti, teklif süreci, görüşme notları, satış sonrası destek, kampanya yönetimi, müşteri memnuniyeti.
5. **IK** — personel yönetimi, işe alım, iş başvurusu, bordro, izin takibi, mesai, performans değerlendirme, çalışan bilgileri, özlük dosyası, maaş süreci, İK'ya yönelik eğitim talebi.
6. **RaporlamaAnalitik** — dashboard, iş zekası, veri analizi, satış/finans/stok/performans raporu, KPI, grafik, veri görselleştirme, özel rapor talebi, analitik ekranlar.
7. **EgitimDanismanlik** — kullanıcı eğitimi, ürün/ERP/muhasebe eğitimi, sistem kullanımı danışmanlığı, kurulum sonrası destek, canlı eğitim, dokümantasyon, süreç/proje danışmanlığı.
8. **Diger** — spam, reklam, ilgisiz içerik. (Bu değer modelin sınıflandıramadığı mailler için çıkış kapısı; Priority=Low olur.)

Admin panelde enum → görünen ad eşlemesi kullan ("MuhasebeFinans" → "Muhasebe / Finans", "EDonusum" → "E-Dönüşüm" vb.).

## 6. AI Analiz Pipeline'ı

**API çağrısı:**
- Endpoint: `POST https://api.openai.com/v1/chat/completions`
- Header'lar: `Authorization: Bearer <ApiKey>`, `content-type: application/json`
- Body: `model` (config'den, varsayılan `gpt-4o-mini` — güncel model adları için https://platform.openai.com/docs/models kontrol edilebilir), `max_tokens: 1024`, `temperature: 0`, `response_format: { "type": "json_object" }`, `messages` = system rolü (sınıflandırma talimatı) + user rolü (mailin subject/sender/date/body bilgisi).
- Yanıtta `choices[0].message.content` içinden JSON parse edilir. Model başına/sonuna metin veya ```json fence eklerse temizle: ilk `{` ile son `}` arasını al, güvenli parse et.

**Modelden istenecek JSON sözleşmesi (system promptta zorunlu kıl — SADECE geçerli JSON, başka hiçbir şey yok):**

```json
{
  "mainCategory": "ERP | MuhasebeFinans | EDonusum | CRM | IK | RaporlamaAnalitik | EgitimDanismanlik | Diger",
  "subCategory": "3-6 kelimelik spesifik Türkçe konu (örn: E-fatura gönderim hatası)",
  "priority": "Low | Medium | High | Critical",
  "sentiment": "Positive | Neutral | Negative | Angry | Urgent",
  "summary": "1-2 cümlelik Türkçe özet",
  "confidence": 0.0,
  "requiresHumanReview": false
}
```

**System prompta gömülecek kurallar:**
- Kategori tanımları bölüm 5'teki gibi verilecek.
- Priority: sistem çalışmıyor / e-fatura gönderilemiyor / muhasebe kapanışı yapılamıyor / ERP modülü çöktü / veri kaybı / çok kullanıcı etkileniyor → High veya Critical. Genel bilgi alma, eğitim talebi, danışmanlık randevusu → Medium. Spam, reklam, ilgisiz → Low.
- Sentiment: şikayet ve öfke ifadeleri → Angry; "acil", "bugün", "hemen" gibi zaman baskısı → Urgent; sorun bildirimi → Negative.
- requiresHumanReview: model emin değilse veya hassas konu varsa (hukuki tehdit, ödeme anlaşmazlığı, ciddi müşteri şikayeti) → true.

**Uygulama tarafı kurallar:**
- `confidence < ConfidenceThreshold (varsayılan 0.70)` → RequiresHumanReview=true (model false demiş olsa bile).
- Retry: 429 ve 5xx için 3 deneme, exponential backoff (2s / 4s / 8s).
- API veya parse hatası → mail yine kaydedilir: IsProcessed=false, RequiresHumanReview=true, kategori=Diger. Hata loglanır, pipeline durmasın.
- Servis arayüzü: `IEmailAnalysisService.AnalyzeAsync(subject, senderEmail, senderName, receivedDate, body)` → analiz sonucu DTO döner.

## 7. E-Posta Çekme (Worker)

- `EmailPollingService : BackgroundService`, `PollingIntervalSeconds` (varsayılan 60) aralıklarla çalışır.
- MailKit `ImapClient` → `imap.gmail.com:993` SSL, e-posta + App Password ile login.
- INBOX'taki **UNSEEN** mailler alınır. Her mail için:
  1. MessageId DB'de varsa atla.
  2. Body'yi plain text'e çevir (TextBody yoksa HtmlBody'den HTML strip'le), `MaxBodyChars` (8000) ile kırp.
  3. AI analizini çağır, sonucu entity'ye map'le, kaydet.
  4. Maili `\Seen` işaretle.
- Ekler indirilmez, sadece sayısı loglanır.
- Tek mail'de hata olursa yakala, logla, sonraki maile geç. IMAP bağlantı kopmalarına dayanıklı ol (her döngüde bağlan/kapat yeterli).

## 8. API Endpoint'leri

- `GET /api/emails` — sayfalama (`page`, `pageSize`) + filtreler: `mainCategory`, `priority`, `sentiment`, `requiresHumanReview`, `dateFrom`, `dateTo`, `search` (subject/sender/summary içinde arar).
- `GET /api/emails/{id}` — tek kayıt detayı.
- `PUT /api/emails/{id}/review` — insan düzeltmesi: mainCategory, subCategory, priority güncellenebilir; RequiresHumanReview=false yapılır.
- `POST /api/emails/analyze` — manuel test: body'de subject + body (+ opsiyonel sender) alır, AI analiz sonucunu döner; `save=true` query parametresiyle DB'ye de kaydeder. (Gmail bağlamadan uçtan uca test için kritik.)
- `GET /api/dashboard/stats` — toplam mail, kategori dağılımı, öncelik dağılımı, sentiment dağılımı, human review bekleyen sayısı, son 30 gün günlük mail hacmi.

## 9. Admin Panel

- Razor Pages, Bootstrap 5 (CDN), Chart.js (CDN). Arayüz dili Türkçe.
- `/admin` — dashboard: özet kartlar (toplam, Critical sayısı, review bekleyen) + 3 grafik: kategori dağılımı (pie), öncelik dağılımı (bar), son 30 gün hacim (line).
- `/admin/emails` — filtre formu + sayfalı tablo. Priority renk kodlu badge (Critical kırmızı, High turuncu, Medium sarı, Low gri). Satıra tıklayınca detay sayfası: tüm alanlar + kategori/öncelik düzeltme ve "İncelendi olarak işaretle" formu (review endpoint'ini kullanır).

## 10. Konfigürasyon

`appsettings.json` şablonu (gerçek değerler `dotnet user-secrets` ile, koda/repoya asla gömülmez):

```json
{
  "ConnectionStrings": { "Default": "Server=localhost;Database=EmailAnalyzerDb;Trusted_Connection=True;TrustServerCertificate=True" },
  "Gmail": { "Host": "imap.gmail.com", "Port": 993, "Email": "", "AppPassword": "", "PollingIntervalSeconds": 60, "Folder": "INBOX" },
  "OpenAi": { "ApiKey": "", "Model": "gpt-4o-mini", "MaxTokens": 1024, "BaseUrl": "https://api.openai.com/v1/" },
  "Analysis": { "ConfidenceThreshold": 0.70, "MaxBodyChars": 8000 }
}
```

README'de anlat: Gmail App Password nasıl alınır (2FA şart), OpenAI API key nereden alınır (platform.openai.com), user-secrets komutları, `dotnet ef database update`, projeyi çalıştırma.

## 11. Uygulama Fazları (bu sırayla, her fazda onay al)

1. **Faz 1 — İskelet & Veritabanı:** Solution, 3 proje, NuGet paketleri, entity + enum'lar, DbContext + konfigürasyonlar, ilk migration. Kabul: build temiz, `dotnet ef database update` DB'yi oluşturuyor.
2. **Faz 2 — AI Servisi:** OpenAI API client + `IEmailAnalysisService` implementasyonu, JSON parse + validasyon + retry, `POST /api/emails/analyze`. Kabul: Swagger'dan örnek Türkçe mail metniyle uçtan uca doğru sonuç.
3. **Faz 3 — Mail Worker:** MailKit servisi + BackgroundService + idempotency. Kabul: gerçek Gmail hesabına gelen mail otomatik analiz edilip DB'de görünüyor, ikinci döngüde tekrar işlenmiyor.
4. **Faz 4 — Sorgu API'leri:** listeleme/filtre, detay, review, dashboard stats.
5. **Faz 5 — Admin Panel.**
6. **Faz 6 — Cila:** Serilog, global exception handling, README, örnek istekler için `.http` dosyası, JSON parser için birkaç unit test.

## 12. Kapsam DIŞI (yapma)

- Authentication/authorization (demo aşaması, sonra eklenecek)
- Docker, CI/CD, message queue, mikroservis mimarisi
- Otomatik mail cevaplama / mail gönderme
- React/Angular gibi ayrı frontend — Razor yeterli
- Gereksiz abstraction ve over-engineering; kod temiz ama pragmatik olsun

## 13. Genel Kurallar

- Kullanıcıya görünen tüm metinler (admin panel, summary) Türkçe; kod ve kod yorumları İngilizce.
- Tutarlı async/await, CancellationToken kullan.
- DTO'lar ile entity'yi API'de doğrudan expose etme.
- Her fazın sonunda derlenen, çalışan kod bırak.
