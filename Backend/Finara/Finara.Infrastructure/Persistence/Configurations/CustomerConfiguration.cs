using Finara.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finara.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(c => c.LastName).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(256).IsRequired();
        builder.HasIndex(c => c.Email).IsUnique();

        // Customer.Transactions salt-okunur bir IReadOnlyCollection — EF'e özel Add metodu
        // sunmuyor, bu yüzden private _transactions alanına doğrudan erişmesini söylüyoruz.
        builder.Metadata
            .FindNavigation(nameof(Customer.Transactions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
