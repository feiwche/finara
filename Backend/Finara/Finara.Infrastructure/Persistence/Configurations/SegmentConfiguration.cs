using Finara.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finara.Infrastructure.Persistence.Configurations;

public class SegmentConfiguration : IEntityTypeConfiguration<Segment>
{
    public void Configure(EntityTypeBuilder<Segment> builder)
    {
        builder.ToTable("Segments");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.AiCommentary).HasMaxLength(2000);

        builder.HasOne(s => s.Customer)
            .WithMany()
            .HasForeignKey(s => s.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Bank>()
            .WithMany()
            .HasForeignKey(s => s.BankId)
            .OnDelete(DeleteBehavior.Restrict);

        // Bir müşterinin en fazla bir "aktif" segment kaydı olabilir — geçmiş kayıtlar
        // IsActive=false ile arşivlenir, bu yüzden filtrelenmiş benzersiz index kullanılıyor.
        builder.HasIndex(s => s.CustomerId)
            .IsUnique()
            .HasFilter("[IsActive] = 1")
            .HasDatabaseName("IX_Segments_CustomerId_ActiveOnly");
    }
}
