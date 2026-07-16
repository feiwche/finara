using Finara.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Finara.Infrastructure.Persistence;

// AuditLog kasıtlı olarak burada yok: o tablo Dapper ile yönetiliyor (bkz. AuditLogRepository).
public class FinaraDbContext : DbContext
{
    public FinaraDbContext(DbContextOptions<FinaraDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Segment> Segments => Set<Segment>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignOffer> CampaignOffers => Set<CampaignOffer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinaraDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
