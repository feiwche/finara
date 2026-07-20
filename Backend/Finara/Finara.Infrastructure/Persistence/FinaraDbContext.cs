using Finara.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Finara.Infrastructure.Persistence;

// AuditLog kasıtlı olarak burada yok: o tablo Dapper ile yönetiliyor (bkz. AuditLogRepository).
public class FinaraDbContext : DbContext
{
    public FinaraDbContext(DbContextOptions<FinaraDbContext> options) : base(options)
    {
    }

    public DbSet<Bank> Banks => Set<Bank>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Segment> Segments => Set<Segment>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignOffer> CampaignOffers => Set<CampaignOffer>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AiInteractionLog> AiInteractionLogs => Set<AiInteractionLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinaraDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    // Tüm DateTime/DateTime? kolonlarına global olarak uygulanıyor — bkz. UtcDateTimeConverter
    // XML yorumu (okurken Kind=Utc damgalama, SQL Server datetime2'nin bunu saklamaması sorunu).
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }
}
