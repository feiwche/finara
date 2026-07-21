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
        builder.Property(c => c.PhoneNumber).HasMaxLength(20);

        // Banka bazlı benzersizlik — iki farklı banka aynı e-postayı kendi müşterisi
        // olarak ayrı ayrı kaydedebilir (bkz. CustomerService.CreateCustomerAsync).
        builder.HasIndex(c => new { c.BankId, c.Email }).IsUnique();

        builder.HasOne<Bank>()
            .WithMany()
            .HasForeignKey(c => c.BankId)
            .OnDelete(DeleteBehavior.Restrict);

        // Customer.Transactions salt-okunur bir IReadOnlyCollection — EF'e özel Add metodu
        // sunmuyor, bu yüzden private _transactions alanına doğrudan erişmesini söylüyoruz.
        builder.Metadata
            .FindNavigation(nameof(Customer.Transactions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
