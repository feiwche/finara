using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Common;
using Finara.Domain.Entities;
using Finara.Domain.Enums;
using Finara.Infrastructure.Ai;
using Finara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace Finara.Infrastructure.Services;

// MCP tool'larıyla (CustomerMcpTools) aynı alttaki CustomerContextTools metodlarını
// kullanır — ama dış bir MCP round-trip'i üzerinden değil, Microsoft.Extensions.AI'nin
// native function-calling'i ile doğrudan. customerId burada closure'a alınıyor, LLM'in
// GUID'i doğru yazmasına güvenmek yerine — daha güvenilir.
//
// IChatClient DI'dan Singleton olarak enjekte ediliyor (bkz. Program.cs), AiChatService
// ile PAYLAŞILAN aynı bağlantı — öncesinde her çağrıda yeni bir OllamaChatClient
// oluşturulup hiç Dispose edilmiyordu (kaynak sızıntısı, denetimde bulundu).
//
// AiInteractionLog kaydı bu etkileşimin izini zaten tutuyor; ayrıca genel AuditLog'a
// yazmıyoruz (AuditActionType bu senaryo için henüz bir değer içermiyor — gerekirse
// ileride eklenir).
public class AiInsightService(FinaraDbContext dbContext, CustomerContextTools tools, IChatClient chatClient, ICurrentUserContext currentUser)
    : IAiInsightService
{
    // Varsayılan soru ve modele verilen context, kullanıcının o an kullandığı arayüz diline
    // göre üretiliyor — Almanca arayüz kullanan biri Almanca yanıt alır. Önceden soru sabit
    // "Türkçe yanıt ver" diyordu ve context ham İngilizce enum'lar içeriyordu.
    private static string DefaultQuestion(UiLanguage language) => language switch
    {
        UiLanguage.English =>
            "Give a short summary of this customer: what is their spending profile, which campaign would you recommend and why? " +
            "Gather what you need using the tools, then answer in English, briefly (at most 4-5 sentences).",
        UiLanguage.German =>
            "Fasse diesen Kunden kurz zusammen: Wie ist sein Ausgabenprofil, welche Kampagne würdest du warum empfehlen? " +
            "Sammle die nötigen Informationen mit den Werkzeugen und antworte dann auf Deutsch, kurz (höchstens 4-5 Sätze).",
        _ =>
            "Bu müşteri hakkında kısa bir özet çıkar: harcama profili nedir, hangi kampanyayı neden önerirsin? " +
            "Gerekli bilgileri araçları kullanarak topla, sonra Türkçe ve kısa (en fazla 4-5 cümle) bir yanıt ver."
    };

    // Kullanıcı kendi sorusunu yazdığında da yanıt dilini garanti altına alan ek talimat —
    // aksi halde model, sorunun yazıldığı dile göre savrulabiliyor.
    private static string LanguageInstruction(UiLanguage language) => language switch
    {
        UiLanguage.English => " Answer in English.",
        UiLanguage.German => " Antworte auf Deutsch.",
        _ => " Yanıtı Türkçe ver."
    };

    public async Task<AiInsightResult?> GenerateInsightAsync(Guid customerId, string? question, CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        if (customer is null || (!currentUser.IsSuperAdmin && customer.BankId != currentUser.BankId))
            return null;

        // Arayüz dili kullanıcının kendi tercihinde tutuluyor (User.Preferences.Language) ve
        // uygulama açılışta/ayar kaydında arayüzü hep buna göre ayarlıyor — yani bu, kullanıcının
        // O AN kullandığı dil. Kimlik yoksa (olmaması beklenmez, uç auth'lu) varsayılana düşer.
        var language = currentUser.UserId is { } userId
            ? await dbContext.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.Preferences.Language)
                .FirstOrDefaultAsync(cancellationToken)
            : UiLanguage.Turkish;

        var effectiveQuestion = string.IsNullOrWhiteSpace(question)
            ? DefaultQuestion(language)
            : question + LanguageInstruction(language);

        List<AITool> customerTools =
        [
            AIFunctionFactory.Create(() => tools.GetCustomerProfile(customerId, language),
                name: "get_customer_profile",
                description: "Bu müşterinin profilini (yaş, yatırım/SWIFT bayrakları) ve aktif segmentini (RFM skoru, harcama eğilimi, baskın kategori) getirir."),
            AIFunctionFactory.Create((int days) => tools.GetCustomerTransactions(customerId, days, language),
                name: "get_customer_transactions",
                description: "Bu müşterinin son harcama işlemlerini listeler. days: kaç gün geriye bakılacağı, varsayılan 90."),
            AIFunctionFactory.Create(() => tools.GetCustomerOffers(customerId, language),
                name: "get_customer_offers",
                description: "Bu müşteriye daha önce sunulmuş kampanya tekliflerini ve güven skorlarını listeler."),
            AIFunctionFactory.Create(() => tools.GetActiveCampaigns(language),
                name: "get_active_campaigns",
                description: "Şu anda aktif olan tüm kampanyaları listeler."),
        ];

        var chatOptions = new ChatOptions { Tools = customerTools };

        // Ollama ayakta değilse ya da yapılandırılmış süre içinde yanıt vermezse, bu ham
        // altyapı hatası (TaskCanceledException/HttpRequestException) genel bir 500'e
        // düşüyordu — kullanıcı "Beklenmeyen bir hata oluştu" görüyordu. Artık stabil bir
        // hata koduyla 503'e çevriliyor, frontend errors.AI_SERVICE_UNAVAILABLE anahtarıyla
        // kullanıcının kendi dilinde anlamlı bir mesaj gösterebiliyor. İsteğin GERÇEKTEN
        // iptal edildiği durum (kullanıcı sayfadan ayrıldı) ayrı tutuluyor — o bir hata değil.
        ChatResponse response;
        try
        {
            response = await chatClient.GetResponseAsync(effectiveQuestion, chatOptions, cancellationToken);
        }
        catch (Exception ex) when (ex is TaskCanceledException or TimeoutException or HttpRequestException
                                   && !cancellationToken.IsCancellationRequested)
        {
            throw new DomainException(
                ErrorCodes.AiServiceUnavailable,
                "Yapay zekâ servisi şu anda yanıt vermiyor. Lütfen daha sonra tekrar deneyin.",
                503);
        }

        var answer = response.Text;

        var interactionLog = AiInteractionLog.Create(customerId, effectiveQuestion, answer);
        dbContext.AiInteractionLogs.Add(interactionLog);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AiInsightResult(customerId, effectiveQuestion, answer, interactionLog.CreatedAtUtc);
    }
}
