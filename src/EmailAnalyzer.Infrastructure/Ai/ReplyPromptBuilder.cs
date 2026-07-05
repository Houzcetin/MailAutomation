using System.Globalization;
using EmailAnalyzer.Domain.Entities;

namespace EmailAnalyzer.Infrastructure.Ai;

/// <summary>
/// Builds the system and user prompts for AI reply drafting. The reply is a human-reviewed
/// draft: the model must match the incoming mail's language, adapt tone to the analysed
/// category, and never invent commitments (prices, deadlines, names).
/// </summary>
public static class ReplyPromptBuilder
{
    public const string SystemPrompt = """
        Sen kurumsal bir yazılım şirketinin (ERP, muhasebe/finans, e-dönüşüm, CRM, İK,
        raporlama/analitik, eğitim/danışmanlık) müşteri iletişim asistanısın. Sana verilen
        gelen e-postaya, bir insan çalışanın gözden geçirip göndereceği profesyonel bir
        YANIT TASLAĞI yazacaksın.

        Dil kuralı: Yanıtı, gelen e-postanın yazıldığı dille AYNI dilde yaz (e-posta
        Türkçe ise Türkçe, İngilizce ise İngilizce, başka bir dilse o dilde).

        Ton: kurumsal, kibar, net ve çözüm odaklı. Kategoriye göre uyarla:
        - ERP / EDonusum → teknik destek dili; sorunun alındığını ve inceleneceğini belirt.
        - MuhasebeFinans → tutar, fatura ve ödeme konularında temkinli ol; kayıtların
          kontrol edileceğini söyle, tutar teyidi verme.
        - CRM (özellikle şikayet) → empatik bir açılış kullan; gerekiyorsa özür dile.
        - IK (iş başvurusu vb.) → başvurunun/talebin alındığını teyit et.
        - EgitimDanismanlik / RaporlamaAnalitik → talebin değerlendirilip planlamaya
          alınacağını belirt.
        - Duygu (sentiment) Angry veya Urgent ise anlayışlı ve önceliklendirici bir
          açılış yap.

        YASAKLAR (kritik):
        - Fiyat, indirim, tarih/termin, çözüm süresi veya herhangi bir taahhüt UYDURMA.
        - Olmayan kişi adı, ticket numarası veya teknik detay UYDURMA.
        - E-postada verilmeyen hiçbir bilgiyi varsayma.
        - Somut söz vermek yerine "en kısa sürede", "ekibimiz inceleyecek" gibi bağlayıcı
          olmayan ifadeler kullan.

        Yanıtın yapısı:
        1. Selamlama (gönderen adı biliniyorsa adıyla hitap et).
        2. Konuya atıf ve e-postanın alındığının teyidi.
        3. İçeriğe dönük 1-3 kısa paragraf.
        4. Sonraki adımı belirten bir cümle.
        5. Kapanış ve imza. İmza bilgisi (ad/firma) verilmişse onu kullan; verilmemişse
           [Ad Soyad] yer tutucusuyla bitir.
        Konu satırı yazma, HTML kullanma; düz metin üret.

        Döndüreceğin çıktı: SADECE geçerli bir JSON nesnesi, tam olarak şu biçimde:
        {"replyBody": "yanıt metni"}
        JSON dışında hiçbir metin, açıklama veya ```json işareti ekleme. Yanıt metnindeki
        satır sonları için \n kullan.
        """;

    public static string BuildUserPrompt(
        EmailMessage email, string truncatedBody, string companyName, string signOffName)
    {
        var name = string.IsNullOrWhiteSpace(email.SenderName) ? "(yok)" : email.SenderName;
        var subCategory = string.IsNullOrWhiteSpace(email.SubCategory) ? "(yok)" : email.SubCategory;
        var summary = string.IsNullOrWhiteSpace(email.Summary) ? "(yok)" : email.Summary;
        var company = string.IsNullOrWhiteSpace(companyName) ? "(yok)" : companyName;
        var signOff = string.IsNullOrWhiteSpace(signOffName) ? "(yok)" : signOffName;

        return $"""
            Yanıtlanacak e-posta:
            Gönderen adı: {name}
            Gönderen e-posta: {email.SenderEmail}
            Tarih: {email.ReceivedDate.ToString("u", CultureInfo.InvariantCulture)}
            Konu: {email.Subject}

            İçerik:
            {truncatedBody}

            AI analiz sonucu (yanıtın tonunu buna göre uyarla):
            Kategori: {email.MainCategory}
            Alt konu: {subCategory}
            Öncelik: {email.Priority}
            Duygu: {email.Sentiment}
            Özet: {summary}

            İmza bilgisi:
            Firma adı: {company}
            Gönderen çalışan adı: {signOff}
            """;
    }
}
