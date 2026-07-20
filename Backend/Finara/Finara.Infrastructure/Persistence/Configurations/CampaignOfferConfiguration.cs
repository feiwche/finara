using Finara.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finara.Infrastructure.Persistence.Configurations;

public class CampaignOfferConfiguration : IEntityTypeConfiguration<CampaignOffer>
{
    public void Configure(EntityTypeBuilder<CampaignOffer> builder)
    {
        builder.ToTable("CampaignOffers");
        builder.HasKey(o => o.Id);

        builder.HasOne(o => o.Campaign)
            .WithMany(c => c.Offers)
            .HasForeignKey(o => o.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.Customer)
            .WithMany()
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Bank>()
            .WithMany()
            .HasForeignKey(o => o.BankId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => o.Response);
        builder.HasIndex(o => o.BankId);
    }
}
