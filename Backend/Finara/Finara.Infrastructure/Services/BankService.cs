using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Entities;
using Finara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Finara.Infrastructure.Services;

public class BankService(FinaraDbContext dbContext, ICurrentUserContext currentUser) : IBankService
{
    public async Task<IReadOnlyList<BankSummary>> GetBanksAsync(CancellationToken cancellationToken = default)
    {
        // CreateBankAsync/DeactivateBankAsync'te zaten olan SuperAdmin kısıtlaması burada
        // eksikti — auth'lı HERHANGİ bir kullanıcı (BankAdmin dahil) tüm bankaların
        // listesini görebiliyordu (route'ta sadece RequireAuthorization() vardı, rol
        // kontrolü yoktu — güvenlik incelemesinde bulundu). Bu uca meşru tek erişim
        // noktası zaten SuperAdmin-only bank-management ekranı; BankAdmin için hiçbir
        // frontend akışı bu listeyi kullanmıyor.
        if (!currentUser.IsSuperAdmin)
            throw new InvalidOperationException("Banka listesini görüntülemek için SuperAdmin yetkisi gerekli.");

        var banks = await dbContext.Banks.AsNoTracking().OrderBy(b => b.Name).ToListAsync(cancellationToken);
        return banks.Select(ToSummary).ToList();
    }

    public async Task<BankSummary?> GetMyBankAsync(CancellationToken cancellationToken = default)
    {
        if (currentUser.BankId is null)
            return null;

        var bank = await dbContext.Banks.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == currentUser.BankId, cancellationToken);
        return bank is null ? null : ToSummary(bank);
    }

    public async Task<BankSummary> CreateBankAsync(CreateBankRequest request, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsSuperAdmin)
            throw new InvalidOperationException("Yeni banka oluşturmak için SuperAdmin yetkisi gerekli.");

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var codeInUse = await dbContext.Banks.AnyAsync(b => b.Code == normalizedCode, cancellationToken);
        if (codeInUse)
            throw new InvalidOperationException($"'{request.Code}' kodlu bir banka zaten var.");

        var bank = Bank.Create(request.Name, request.Code);
        dbContext.Banks.Add(bank);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToSummary(bank);
    }

    public async Task<BankSummary?> DeactivateBankAsync(Guid bankId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsSuperAdmin)
            throw new InvalidOperationException("Banka pasifleştirmek için SuperAdmin yetkisi gerekli.");

        var bank = await dbContext.Banks.FirstOrDefaultAsync(b => b.Id == bankId, cancellationToken);
        if (bank is null)
            return null;

        bank.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToSummary(bank);
    }

    private static BankSummary ToSummary(Bank bank) => new(bank.Id, bank.Name, bank.Code, bank.IsActive, bank.DataProviderType);
}
