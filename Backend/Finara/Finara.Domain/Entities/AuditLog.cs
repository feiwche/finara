using Finara.Domain.Enums;

namespace Finara.Domain.Entities;

// BaseEntity'den bilerek türetilmedi: AuditLog hiç güncellenmez (UpdatedAtUtc anlamsız)
// ve bu tablo EF Core değil Dapper ile yönetiliyor — init-only property'ler Dapper'ın
// reflection ile doldurabildiği, ama uygulama kodunun oluşturduktan sonra değiştiremediği
// bir orta yol sağlıyor.
public class AuditLog
{
    public Guid Id { get; init; }
    public Guid CustomerId { get; init; }
    public AuditActionType ActionType { get; init; }
    public string? Details { get; init; }
    public Guid? PerformedByUserId { get; init; }
    public DateTime OccurredAtUtc { get; init; }

    public static AuditLog Create(
        Guid customerId,
        AuditActionType actionType,
        string? details = null,
        Guid? performedByUserId = null)
    {
        if (customerId == Guid.Empty)
            throw new ArgumentException("Geçerli bir müşteri kimliği gerekli.", nameof(customerId));

        return new AuditLog
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            ActionType = actionType,
            Details = details,
            PerformedByUserId = performedByUserId,
            OccurredAtUtc = DateTime.UtcNow
        };
    }
}
