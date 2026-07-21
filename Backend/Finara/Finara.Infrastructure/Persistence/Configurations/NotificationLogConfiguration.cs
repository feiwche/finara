using Finara.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finara.Infrastructure.Persistence.Configurations;

public class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> builder)
    {
        builder.ToTable("NotificationLogs");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Message).HasMaxLength(1000).IsRequired();
        builder.Property(n => n.SkippedReason).HasMaxLength(500);

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(n => n.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Bank>()
            .WithMany()
            .HasForeignKey(n => n.BankId)
            .OnDelete(DeleteBehavior.Restrict);

        // NoAction (SetNull DEĞİL): CampaignOfferId'den Customers'a dolaylı bir yol zaten
        // CustomerId FK'sı (Cascade) üzerinden var — SQL Server aynı tabloya (Customers)
        // birden fazla cascade/set-null yolu (Error 1785) kabul etmiyor, migration
        // uygulanırken bulundu ve düzeltildi.
        builder.HasOne<CampaignOffer>()
            .WithMany()
            .HasForeignKey(n => n.CampaignOfferId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
