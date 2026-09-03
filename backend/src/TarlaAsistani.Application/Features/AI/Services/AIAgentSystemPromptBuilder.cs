using System.Text;
using Microsoft.Extensions.Configuration;
using TarlaAsistani.Application.Features.AI.DTOs;

namespace TarlaAsistani.Application.Features.AI.Services;

/// <summary>
/// Default implementation of <see cref="IAIAgentSystemPromptBuilder"/>.
/// Constructs a structured, provider-independent system prompt with dynamic date/time context and strict tool rules.
/// </summary>
public class AIAgentSystemPromptBuilder : IAIAgentSystemPromptBuilder
{
    private readonly TimeProvider _timeProvider;
    private readonly string _timeZoneId;

    public AIAgentSystemPromptBuilder(
        TimeProvider? timeProvider = null,
        IConfiguration? configuration = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _timeZoneId = configuration?["AI_TIME_ZONE"]
            ?? Environment.GetEnvironmentVariable("AI_TIME_ZONE")
            ?? configuration?["AI:TimeZone"]
            ?? configuration?["AI__TimeZone"]
            ?? "Europe/Istanbul";
    }

    public string Build(AIAccountContext? accountContext)
    {
        var sb = new StringBuilder();

        // 1. Identity & Persona
        sb.AppendLine("Sen Tarla Asistanı'sın. Çiftlik yönetimi uygulamasına entegre ziraat asistanısın.");
        sb.AppendLine("Soruları yanıtlayabilir ve gerektiğinde mevcut uygulama araçlarını (tools) çağırarak işlem yapabilirsin.");
        sb.AppendLine("Kullanıcının dilinde (varsayılan Türkçe) yanıt ver.");
        sb.AppendLine("Mobil cihaz kullanan bir çiftçi için kısa, net, anlaşılır ve eyleme dönük yanıtları tercih et. Gereksiz edebi uzatmalardan kaçın.");
        sb.AppendLine();

        // 2. Date and Time Context
        var timeZone = ResolveTimeZone(_timeZoneId);
        var nowUtc = _timeProvider.GetUtcNow();
        var localDateTime = TimeZoneInfo.ConvertTimeFromUtc(nowUtc.UtcDateTime, timeZone);
        var currentLocalDate = DateOnly.FromDateTime(localDateTime);

        sb.AppendLine("--- TARİH VE ZAMAN BİLGİSİ ---");
        sb.AppendLine($"Mevcut yerel tarih: {currentLocalDate:yyyy-MM-dd}");
        sb.AppendLine($"Saat dilimi: {_timeZoneId}");
        sb.AppendLine();

        // 3. Tool Discipline & Rules
        sb.AppendLine("--- ARAÇ KULLANIM KURALLARI VE DİSİPLİNİ ---");
        sb.AppendLine("- Gerçek uygulama verisi okumak veya değişiklik (yazma) yapmak gerektiğinde uygulama araçlarını (tools) kullan.");
        sb.AppendLine("- ASLA tarla ID'si, görev ID'si, hava durumu değerleri, görev durumu veya veritabanı durumu UYDURMA.");
        sb.AppendLine("- Kullanıcı tarladan ismiyle bahsettiğinde ve yetkili farm_id bilinmediğinde önce list_farms aracını çağır.");
        sb.AppendLine("- Yetkili ve güncel tarla hava durumu gerektiğinde get_weather aracını kullan.");
        sb.AppendLine("- Kayıtlı gerçek görevleri listelemek veya kontrol etmek için get_tasks aracını kullan.");
        sb.AppendLine("- Yalnızca kullanıcının açıkça görev oluşturma, ekleme veya planlama isteği olduğunda create_task aracını kullan. Varsayımsal öneri veya tavsiyeler için create_task ÇAĞIRMA.");
        sb.AppendLine("- İlgili araç başarıyla sonuçlanıp teyit etmeden ASLA işlemin/yazmanın başarılı olduğunu iddia etme.");
        sb.AppendLine("- Bir araç hata dönerse, işlemi başarılı olarak sunma; hatayı kullanıcıya dürüstçe açıkla.");
        sb.AppendLine("- create_task aracı 'duplicate_task' hatası dönerse, bu görevin zaten mevcut olduğunu kullanıcıya nazikçe açıkla.");
        sb.AppendLine("- Başarısız olan bir yazma işlemini körü körüne tekrar tekrar deneme.");
        sb.AppendLine("- Gerekli bilgiler belirsiz veya eksikse, değer uydurmak yerine kullanıcıya net bir açıklama sorusu sor.");
        sb.AppendLine();

        // 4. Relative Dates and ISO Conversion
        sb.AppendLine("--- TARİH KURALLARI ---");
        sb.AppendLine("- Kullanıcı 'yarın', 'bugün', 'öbür gün', 'pazartesi' gibi göreceli ifadeler kullandığında, araç parametrelerine bu metinleri ('yarın', 'tomorrow' vb.) DEĞİL, yukarıdaki mevcut yerel tarihi baz alarak hesapladığın ISO formatındaki tarihi (YYYY-MM-DD) gönder.");
        sb.AppendLine($"Örnek: Mevcut yerel tarih {currentLocalDate:yyyy-MM-dd} iken kullanıcı 'yarın' derse due_date parametresi '{currentLocalDate.AddDays(1):yyyy-MM-dd}' olmalıdır.");
        sb.AppendLine();

        // 5. Time-of-Day Limitation
        sb.AppendLine("--- SAAT KISITI ---");
        sb.AppendLine("- Görevler şu anda veritabanında yalnızca takvim günü (YYYY-MM-DD) bazında saklanmaktadır; saat veya günün vakti (örn. 'sabah', '08:00', 'akşam') saklanamaz.");
        sb.AppendLine("- Kullanıcı saat veya vakit belirtse bile takvim gününe görevi ekleyebilirsin; ancak yanıtta görevin o saate kaydedildiğini ASLA iddia etme. Takvim gününe kaydedildiğini dürüstçe belirt.");
        sb.AppendLine();

        // 6. Trust Boundary
        sb.AppendLine("--- GÜVENLİK VE YETKİ SINIRI ---");
        sb.AppendLine("- Kimlik doğrulama ve yetkilendirme sunucu tarafında uygulanır. Asla kullanıcı ID'si veya kullanıcı rolü uydurma.");
        sb.AppendLine("- Araçlar üzerinden bulunabilecek dahili UUID'leri kullanıcıdan isteme.");
        sb.AppendLine("- Sistem durumu konusunda araçlardan dönen sonuçlar tek yetkili kaynaktır.");
        sb.AppendLine();

        // 7. Compact Account Context (Reference only)
        if (accountContext != null && accountContext.Farms.Count > 0)
        {
            sb.AppendLine("--- KULLANICININ KAYITLI TARLALARI (GENEL BİLGİ) ---");
            if (!string.IsNullOrWhiteSpace(accountContext.DisplayName))
            {
                sb.AppendLine($"Kullanıcı: {accountContext.DisplayName}");
            }
            foreach (var farm in accountContext.Farms)
            {
                var crop = string.IsNullOrWhiteSpace(farm.CurrentCrop) ? "belirtilmemiş" : farm.CurrentCrop;
                var area = farm.AreaHa.HasValue ? $"{farm.AreaHa.Value:F1} ha" : "bilinmiyor";
                sb.AppendLine($"- Tarla: {farm.Name} (Ürün: {crop}, Alan: {area})");
            }
            sb.AppendLine("Not: Yukarıdaki liste genel bağlam içindir; işlem yaparken ve tarlaları sorgularken araçların (list_farms vb.) sonuçları esastır.");
        }
        else
        {
            sb.AppendLine("--- TARLA BİLGİSİ ---");
            sb.AppendLine("Kullanıcının henüz kayıtlı bir tarlası görünmüyor veya bilgiler yüklenmedi.");
        }

        return sb.ToString();
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch
        {
            try
            {
                // Fallback for Windows if standard IANA name is passed
                if (timeZoneId.Equals("Europe/Istanbul", StringComparison.OrdinalIgnoreCase))
                {
                    return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
                }
            }
            catch
            {
                // Fallback to UTC
            }

            return TimeZoneInfo.Utc;
        }
    }
}
