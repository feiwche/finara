using Finara.Domain.Common;

namespace Finara.Domain.Entities;

// Bir müşteri hakkında AI'ya sorulan soru + üretilen yanıt. AuditLog'un aksine
// (o iş olaylarını loglar) bu tablo AI etkileşim geçmişini tutar — ileride "bu
// müşteriye daha önce ne sorulmuştu" gibi context olarak kullanılabilir. DB'de
// tutuluyor, dosya sisteminde değil (kullanıcının Faz 5 kararı).
public class AiInteractionLog : BaseEntity
{
    public Guid CustomerId { get; private set; }
    public string Prompt { get; private set; } = string.Empty;
    public string Response { get; private set; } = string.Empty;

    private AiInteractionLog() { }

    public static AiInteractionLog Create(Guid customerId, string prompt, string response)
    {
        if (customerId == Guid.Empty)
            throw new ArgumentException("Geçerli bir müşteri kimliği gerekli.", nameof(customerId));

        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt boş olamaz.", nameof(prompt));

        return new AiInteractionLog
        {
            CustomerId = customerId,
            Prompt = prompt,
            Response = response ?? string.Empty
        };
    }
}
