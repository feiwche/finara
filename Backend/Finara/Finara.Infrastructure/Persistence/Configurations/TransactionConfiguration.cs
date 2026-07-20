using Finara.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finara.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Amount).HasColumnType("decimal(18,2)");
        builder.Property(t => t.Currency).HasMaxLength(3).IsRequired();
        builder.Property(t => t.MerchantName).HasMaxLength(200).IsRequired();

        builder.HasOne(t => t.Customer)
            .WithMany(c => c.Transactions)
            .HasForeignKey(t => t.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Bank>()
            .WithMany()
            .HasForeignKey(t => t.BankId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.OccurredAtUtc);
        builder.HasIndex(t => t.Category);
        builder.HasIndex(t => t.BankId);
    }
}
