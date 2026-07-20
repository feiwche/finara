using Finara.Application.Interfaces;
using Finara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Finara.Infrastructure.Services;

public class BankDataProviderFactory(FinaraDbContext dbContext, IServiceProvider serviceProvider) : IBankDataProviderFactory
{
    public async Task<IBankDataProvider> GetProviderAsync(Guid bankId, CancellationToken cancellationToken = default)
    {
        var bank = await dbContext.Banks.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bankId, cancellationToken)
            ?? throw new InvalidOperationException($"Banka bulunamadı: {bankId}");

        return serviceProvider.GetRequiredKeyedService<IBankDataProvider>(bank.DataProviderType);
    }
}
