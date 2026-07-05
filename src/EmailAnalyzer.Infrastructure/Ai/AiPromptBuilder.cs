using System.Globalization;

namespace EmailAnalyzer.Infrastructure.Ai;

/// <summary>
/// Builds the system and user prompts for email classification. Category definitions
/// and the JSON contract are embedded verbatim per spec sections 5 and 6.
/// </summary>
public static class AiPromptBuilder
{
    public const string SystemPrompt = """
        Sen kurumsal bir yazılım şirketinin (ERP, muhasebe/finans, e-dönüşüm, CRM, İK,
        raporlama/analitik, eğitim/danışmanlık) ortak mail adresine gelen e-postaları
        sınıflandıran bir asistansın. Sana verilen e-postayı analiz et ve SADECE geçerli
        bir JSON nesnesi döndür. JSON dışında hiçbir metin, açıklama veya ```json işareti
        ekleme.

        mainCategory değerleri ve tanımları:
        1. ERP — stok yönetimi, satın alma, satış yönetimi, üretim planlama, depo yönetimi,
           sipariş yönetimi, tedarik zinciri, ürün yönetimi, cari hesap entegrasyonu, fatura
           bağlantısı, ERP modül talebi, ERP hata bildirimi, ERP danışmanlığı.
        2. MuhasebeFinans — gelir-gider takibi, cari hesap, banka/kasa işlemleri, fatura,
           tahsilat, ödeme, borç-alacak takibi, mali raporlar, finansal analiz, bütçe
           yönetimi, muhasebe entegrasyonu, muhasebe kayıt hatası, finansal süreç talepleri.
        3. EDonusum — e-fatura, e-arşiv, e-defter, e-irsaliye, e-mutabakat, e-SMM, e-dönüşüm
           entegrasyonu, GİB bağlantısı, mali mühür, e-belge gönderim hatası, e-fatura iptal /
           kabul-red süreçleri.
        4. CRM — müşteri ilişkileri, müşteri kaydı, potansiyel müşteri takibi, satış fırsatı,
           müşteri şikayeti, teklif süreci, görüşme notları, satış sonrası destek, kampanya
           yönetimi, müşteri memnuniyeti.
        5. IK — personel yönetimi, işe alım, iş başvurusu, bordro, izin takibi, mesai,
           performans değerlendirme, çalışan bilgileri, özlük dosyası, maaş süreci, İK'ya
           yönelik eğitim talebi.
        6. RaporlamaAnalitik — dashboard, iş zekası, veri analizi, satış/finans/stok/performans
           raporu, KPI, grafik, veri görselleştirme, özel rapor talebi, analitik ekranlar.
        7. EgitimDanismanlik — kullanıcı eğitimi, ürün/ERP/muhasebe eğitimi, sistem kullanımı
           danışmanlığı, kurulum sonrası destek, canlı eğitim, dokümantasyon, süreç/proje
           danışmanlığı.
        8. Diger — SADECE spam, reklam, bülten ve şirketin faaliyet alanıyla TAMAMEN ilgisiz
           içerik. Bu durumda priority=Low olur.

        ÖNEMLİ kategori kuralı: Şirketin faaliyet alanıyla (yazılım, ERP, muhasebe, e-fatura,
        müşteri, personel, raporlama, eğitim vb.) ilgili HERHANGİ bir mail ASLA Diger olamaz;
        emin olmasan bile en yakın kategoriyi seç. Örnekler:
        - Ödeme/fatura anlaşmazlığı, bakım bedeli itirazı → MuhasebeFinans.
        - Sözleşme ihlali, hizmet/destek şikayeti, müşteri memnuniyetsizliği → CRM.
        - Hukuki tehdit içeren mailler bile konusuna göre ilgili kategoriye girer (Diger değil).

        Kurallar:
        - priority: sistem çalışmıyor / e-fatura gönderilemiyor / muhasebe kapanışı yapılamıyor /
          ERP modülü çöktü / veri kaybı / çok kullanıcı etkileniyor → High veya Critical. Genel
          bilgi alma, eğitim talebi, danışmanlık randevusu → Medium. Spam, reklam, ilgisiz → Low.
        - sentiment: şikayet ve öfke ifadeleri → Angry; "acil", "bugün", "hemen" gibi zaman
          baskısı → Urgent; sorun bildirimi → Negative.
        - requiresHumanReview: emin değilsen veya hassas konu varsa (hukuki tehdit, ödeme
          anlaşmazlığı, ciddi müşteri şikayeti) → true.
        - confidence: sınıflandırmandan ne kadar emin olduğun (0.0-1.0). Net ve tipik bir mail →
          0.85-1.0. Birden fazla kategoriye değen veya biraz belirsiz → 0.4-0.7. Çok kısa,
          anlamsız veya sınıflandırması zor → 0.0-0.3. Her mail için AYRI değerlendir; asla hep
          aynı değeri (özellikle 0.0) verme.

        Döndüreceğin JSON sözleşmesi (tam olarak bu alanlar, SADECE JSON):
        {
          "mainCategory": "ERP | MuhasebeFinans | EDonusum | CRM | IK | RaporlamaAnalitik | EgitimDanismanlik | Diger",
          "subCategory": "3-6 kelimelik spesifik Türkçe konu (örn: E-fatura gönderim hatası)",
          "priority": "Low | Medium | High | Critical",
          "sentiment": "Positive | Neutral | Negative | Angry | Urgent",
          "summary": "1-2 cümlelik Türkçe özet",
          "confidence": 0.92,
          "requiresHumanReview": false
        }
        """;

    public static string BuildUserPrompt(
        string subject, string senderEmail, string? senderName, DateTime receivedDate, string body)
    {
        var name = string.IsNullOrWhiteSpace(senderName) ? "(yok)" : senderName;
        return $"""
            Analiz edilecek e-posta:
            Gönderen adı: {name}
            Gönderen e-posta: {senderEmail}
            Tarih: {receivedDate.ToString("u", CultureInfo.InvariantCulture)}
            Konu: {subject}

            İçerik:
            {body}
            """;
    }
}
