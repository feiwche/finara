using AwesomeAssertions;
using Finara.Domain.Entities;
using Finara.Infrastructure.Persistence;
using Finara.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Finara.Infrastructure.Tests;

// Program.cs'teki JwtBearerEvents.OnTokenValidated'ın çekirdek mantığını doğrular — anlık
// hesap kilitleme (bir hesap Deactivate edildiğinde canlı access token'ının artık kabul
// edilmemesi) daha önce hiçbir otomatik testle korunmuyordu (denetimde bulundu, yalnızca
// manuel/canlı testle doğrulanmıştı: aynı token pasifleştirme öncesi 200, sonrası 401).
public class ActiveUserTokenValidatorTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly FinaraDbContext dbContext;
    private readonly ActiveUserTokenValidator sut;

    public ActiveUserTokenValidatorTests()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<FinaraDbContext>().UseSqlite(connection).Options;
        dbContext = new TestFinaraDbContext(options);
        dbContext.Database.EnsureCreated();

        sut = new ActiveUserTokenValidator(dbContext);
    }

    [Fact]
    public async Task IsUserActiveAsync_AktifKullanici_TrueDoner()
    {
        var user = CreateUser();

        var result = await sut.IsUserActiveAsync(user.Id);

        result.Should().BeTrue();
    }

    // Bu, JWT'nin kendisi hâlâ geçerliyken (süresi dolmamışken) hesap devre dışı bırakılırsa
    // artık kabul edilmemesi gerektiğini doğrulayan testin ta kendisi.
    [Fact]
    public async Task IsUserActiveAsync_PasifHesap_FalseDoner()
    {
        var user = CreateUser();
        user.Deactivate();
        dbContext.SaveChanges();

        var result = await sut.IsUserActiveAsync(user.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsUserActiveAsync_OlmayanKullaniciId_FalseDoner()
    {
        var result = await sut.IsUserActiveAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }

    private User CreateUser()
    {
        var user = User.Create("aktif@example.com", "unused-hash");
        dbContext.Users.Add(user);
        dbContext.SaveChanges();
        return user;
    }

    public void Dispose()
    {
        dbContext.Dispose();
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

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
