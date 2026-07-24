using Finara.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finara.Infrastructure.Persistence.Configurations;

public class UserInvitationConfiguration : IEntityTypeConfiguration<UserInvitation>
{
    public void Configure(EntityTypeBuilder<UserInvitation> builder)
    {
        builder.ToTable("UserInvitations");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Email).HasMaxLength(256).IsRequired();
        builder.Property(i => i.TokenHash).HasMaxLength(512).IsRequired();
        builder.HasIndex(i => i.TokenHash).IsUnique();
        builder.HasIndex(i => i.Email);

        // Hedeflenen banka — SuperAdmin daveti için null (User.BankId ile aynı kural).
        builder.HasOne<Bank>()
            .WithMany()
            .HasForeignKey(i => i.BankId)
            .OnDelete(DeleteBehavior.Restrict);

        // Daveti oluşturan yönetici — o hesap silinemediği için (Deactivate var, delete yok)
        // Restrict yeterli, döngüsel cascade riski yok.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(i => i.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
