using Finara.Domain.Entities;
using Finara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finara.Infrastructure.Persistence.Configurations;

public class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("Campaigns");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(2000);
        builder.Property(c => c.VariantId).HasMaxLength(50).IsRequired();

        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.BankId);

        // Genel salt-okunur TargetPropensities property'si EF'in kendi (JSON/array) primitive
        // collection eşlemesine düşmesin diye yok sayılıyor; asıl kalıcılık, aşağıdaki backing
        // field üzerinden. Ayrı bir tabloya gerek yok — az sayıda enum değeri, "IN" filtresiyle
        // sorgulanmıyor, sadece skorlama sırasında bellekte okunuyor. Virgülle ayrılmış string
        // olarak tek kolonda saklanıyor (BackgroundService/Dapper tercihiyle aynı minimal-bağımlılık çizgisi).
        builder.Ignore(c => c.TargetPropensities);

        builder.Property<List<SpendingPropensity>>("_targetPropensities")
            .HasColumnName("TargetPropensities")
            .HasMaxLength(200)
            .HasConversion(
                v => string.Join(',', v.Select(p => (int)p)),
                v => string.IsNullOrEmpty(v)
                    ? new List<SpendingPropensity>()
                    : v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => (SpendingPropensity)int.Parse(s)).ToList(),
                new ValueComparer<List<SpendingPropensity>>(
                    (a, b) => a!.SequenceEqual(b!),
                    v => v.Aggregate(0, (hash, p) => HashCode.Combine(hash, p)),
                    v => v.ToList()))
            .IsRequired();

        builder.HasOne<Bank>()
            .WithMany()
            .HasForeignKey(c => c.BankId)
            .OnDelete(DeleteBehavior.Restrict);

        // Campaign.Offers salt-okunur bir IReadOnlyCollection — CampaignOfferConfiguration
        // tarafında WithMany(c => c.Offers) ile eşleştiği için burada field access mode yeterli.
        builder.Metadata
            .FindNavigation(nameof(Campaign.Offers))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
