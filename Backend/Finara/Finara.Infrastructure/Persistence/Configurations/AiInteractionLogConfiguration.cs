using Finara.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finara.Infrastructure.Persistence.Configurations;

public class AiInteractionLogConfiguration : IEntityTypeConfiguration<AiInteractionLog>
{
    public void Configure(EntityTypeBuilder<AiInteractionLog> builder)
    {
        builder.ToTable("AiInteractionLogs");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Prompt).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(l => l.Response).HasColumnType("nvarchar(max)").IsRequired();

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(l => l.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => l.CustomerId);
    }
}
