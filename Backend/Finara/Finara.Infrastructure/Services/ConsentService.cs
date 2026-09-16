using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Entities;
using Finara.Domain.Enums;
using Finara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Finara.Infrastructure.Services;

// IConsentGate'i de implemente ediyor — dispatcher'lar sadece o dar arayüzü görüyor,
// Grant/Revoke yapamıyor (bkz. IConsentGate XML yorumu).
public class ConsentService(FinaraDbContext dbContext, IAuditLogRepository auditLogRepository, ICurrentUserContext currentUser)
    : IConsentService, IConsentGate
{
    public async Task<bool> IsConsentGrantedAsync(Guid customerId, ConsentChannel channel, CancellationToken cancellationToken = default)
    {
        var consent = await dbContext.CustomerConsents
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.Channel == channel, cancellationToken);

        return consent?.IsGranted ?? false;
    }

    public async Task<IReadOnlyList<ConsentSummary>?> GetConsentsAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        if (customer is null || (!currentUser.IsSuperAdmin && customer.BankId != currentUser.BankId))
            return null;

        var consents = await dbContext.CustomerConsents
            .AsNoTracking()
            .Where(c => c.CustomerId == customerId)
            .ToListAsync(cancellationToken);

        // Hiç kaydı olmayan kanallar da listede görünsün diye (rıza sorulmamış = rıza yok),
        // eksik kanallar IsGranted=false ile dolduruluyor.
        var byChannel = consents.ToDictionary(c => c.Channel);
        return Enum.GetValues<ConsentChannel>()
            .Select(channel => byChannel.TryGetValue(channel, out var c)
                ? new ConsentSummary(c.Channel, c.IsGranted, c.GrantedAtUtc, c.RevokedAtUtc)
                : new ConsentSummary(channel, false, null, null))
            .ToList();
    }

    public async Task<ConsentSummary?> UpdateConsentAsync(Guid customerId, UpdateConsentRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        if (customer is null || (!currentUser.IsSuperAdmin && customer.BankId != currentUser.BankId))
            return null;

        var consent = await dbContext.CustomerConsents
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.Channel == request.Channel, cancellationToken);

        if (consent is null)
        {
            consent = CustomerConsent.Create(customer.BankId, customerId, request.Channel, request.IsGranted);
            dbContext.CustomerConsents.Add(consent);
        }
        else if (request.IsGranted)
        {
            consent.Grant();
        }
        else
        {
            consent.Revoke();
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Denetim Kaydı, rıza değişikliği kalıcı olduktan SONRA yazılıyor — bkz.
        // NextBestOfferService/SegmentationService'teki aynı gerekçe (denetimde bulundu:
        // önceden SaveChangesAsync'ten önce yazılıyordu).
        await auditLogRepository.InsertAsync(
            AuditLog.Create(
                customer.BankId, customerId,
                request.IsGranted ? AuditActionType.ConsentGranted : AuditActionType.ConsentRevoked,
                request.Channel.ToString(), currentUser.UserId),
            cancellationToken);

        return new ConsentSummary(consent.Channel, consent.IsGranted, consent.GrantedAtUtc, consent.RevokedAtUtc);
    }
}
