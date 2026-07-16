using Finara.Domain.Entities;
using Microsoft.EntityFrameworkCore;
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

        // Campaign.Offers salt-okunur bir IReadOnlyCollection — CampaignOfferConfiguration
        // tarafında WithMany(c => c.Offers) ile eşleştiği için burada field access mode yeterli.
        builder.Metadata
            .FindNavigation(nameof(Campaign.Offers))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
