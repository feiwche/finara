using Finara.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finara.Infrastructure.Persistence.Configurations;

public class CustomerConsentConfiguration : IEntityTypeConfiguration<CustomerConsent>
{
    public void Configure(EntityTypeBuilder<CustomerConsent> builder)
    {
        builder.ToTable("CustomerConsents");
        builder.HasKey(c => c.Id);

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(c => c.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Bank>()
            .WithMany()
            .HasForeignKey(c => c.BankId)
            .OnDelete(DeleteBehavior.Restrict);

        // Müşteri başına kanal başına tek satır — bkz. CustomerConsent XML yorumu.
        builder.HasIndex(c => new { c.CustomerId, c.Channel }).IsUnique();
    }
}
