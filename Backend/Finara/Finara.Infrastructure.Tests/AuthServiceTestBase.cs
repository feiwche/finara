using Finara.Application.Interfaces;
using Finara.Domain.Entities;
using Finara.Domain.Enums;
using Finara.Infrastructure.Persistence;
using Finara.Infrastructure.Security;
using Finara.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Finara.Infrastructure.Tests;

// AuthService.ResetPasswordAsync/AcceptInvitationAsync ham ExecuteSqlInterpolatedAsync SQL
// kullanıyor — EF Core InMemory sağlayıcısı bunu desteklemiyor, bu yüzden SQLite in-memory
// (açık bağlantı, testin ömrü boyunca canlı tutuluyor) kullanılıyor.
public abstract class AuthServiceTestBase : IDisposable
{
    private readonly SqliteConnection connection;
    protected FinaraDbContext DbContext { get; }
    protected IConfiguration Configuration { get; }
    protected IAdminEmailSender EmailSender { get; }
    protected IAuditLogRepository AuditLogRepository { get; }
    protected ITwoFactorService TwoFactorService { get; }
    protected AuthService Sut { get; }

    protected AuthServiceTestBase()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<FinaraDbContext>().UseSqlite(connection).Options;
        DbContext = new TestFinaraDbContext(options);
        DbContext.Database.EnsureCreated();

        Configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "test-only-signing-key-at-least-32-characters-long",
                ["Jwt:Issuer"] = "Finara.Api",
                ["Jwt:Audience"] = "Finara.Clients",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7"
            })
            .Build();

        EmailSender = Substitute.For<IAdminEmailSender>();
        AuditLogRepository = Substitute.For<IAuditLogRepository>();
        TwoFactorService = new TwoFactorService(DbContext, DataProtectionProvider.Create("Finara.Tests"));

        Sut = new AuthService(DbContext, Configuration, EmailSender, AuditLogRepository, TwoFactorService, NullLogger<AuthService>.Instance);
    }

    protected User CreateUser(
        string email = "user@example.com",
        string password = "Password123",
        UserRole role = UserRole.SuperAdmin,
        Guid? bankId = null,
        bool isActive = true)
    {
        var user = User.Create(email, PasswordHasher.Hash(password), role, bankId);
        if (!isActive)
            user.Deactivate();

        DbContext.Users.Add(user);
        DbContext.SaveChanges();
        return user;
    }

    public void Dispose()
    {
        DbContext.Dispose();
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    // AiInteractionLogConfiguration.cs, "nvarchar(max)" (SQL Server'ın zaten varsayılanı olan,
    // ama burada açıkça belirtilmiş) bir HasColumnType kullanıyor. SQLite'ın CREATE TABLE
    // ayrıştırıcısı bunu anlamıyor ("near max: syntax error") — üretim şemasını (SQL Server)
    // hiç etkilemeden, sadece bu test DbContext'inde ilgili anotasyonu temizliyoruz.
    private sealed class TestFinaraDbContext(DbContextOptions<FinaraDbContext> options) : FinaraDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
                foreach (var property in entityType.GetProperties())
                    if (property.GetColumnType() == "nvarchar(max)")
                        property.SetColumnType(null);
        }
    }
}
