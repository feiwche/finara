using Finara.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finara.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();

        // SuperAdmin için null (tüm bankaları görür) — bu yüzden FK nullable.
        builder.HasOne<Bank>()
            .WithMany()
            .HasForeignKey(u => u.BankId)
            .OnDelete(DeleteBehavior.Restrict);

        // Tek bir JSON kolonu (EF Core 8 ToJson) — yeni bir tercih eklemek migration gerektirmiyor,
        // bkz. UserPreferences'taki gerekçe.
        builder.OwnsOne(u => u.Preferences, p => p.ToJson());
    }
}
