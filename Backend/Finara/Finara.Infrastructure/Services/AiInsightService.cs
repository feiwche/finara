using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Entities;
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
    private const string DefaultQuestion =
        "Bu müşteri hakkında kısa bir özet çıkar: harcama profili nedir, hangi kampanyayı neden önerirsin? " +
        "Gerekli bilgileri araçları kullanarak topla, sonra Türkçe ve kısa (en fazla 4-5 cümle) bir yanıt ver.";

    public async Task<AiInsightResult?> GenerateInsightAsync(Guid customerId, string? question, CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        if (customer is null || (!currentUser.IsSuperAdmin && customer.BankId != currentUser.BankId))
            return null;

        var effectiveQuestion = string.IsNullOrWhiteSpace(question) ? DefaultQuestion : question;

        List<AITool> customerTools =
        [
            AIFunctionFactory.Create(() => tools.GetCustomerProfile(customerId),
                name: "get_customer_profile",
                description: "Bu müşterinin profilini (yaş, yatırım/SWIFT bayrakları) ve aktif segmentini (RFM skoru, harcama eğilimi, baskın kategori) getirir."),
            AIFunctionFactory.Create((int days) => tools.GetCustomerTransactions(customerId, days),
                name: "get_customer_transactions",
                description: "Bu müşterinin son harcama işlemlerini listeler. days: kaç gün geriye bakılacağı, varsayılan 90."),
            AIFunctionFactory.Create(() => tools.GetCustomerOffers(customerId),
                name: "get_customer_offers",
                description: "Bu müşteriye daha önce sunulmuş kampanya tekliflerini ve güven skorlarını listeler."),
            AIFunctionFactory.Create(() => tools.GetActiveCampaigns(),
                name: "get_active_campaigns",
                description: "Şu anda aktif olan tüm kampanyaları listeler."),
        ];

        var chatOptions = new ChatOptions { Tools = customerTools };
        var response = await chatClient.GetResponseAsync(effectiveQuestion, chatOptions, cancellationToken);
        var answer = response.Text;

        var interactionLog = AiInteractionLog.Create(customerId, effectiveQuestion, answer);
        dbContext.AiInteractionLogs.Add(interactionLog);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AiInsightResult(customerId, effectiveQuestion, answer, interactionLog.CreatedAtUtc);
    }
}
