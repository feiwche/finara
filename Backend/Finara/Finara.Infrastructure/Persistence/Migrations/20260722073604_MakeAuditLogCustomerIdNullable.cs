using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeAuditLogCustomerIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AuditLogs, EF'in DbSet/model sistemine dahil değil (bkz. AddAuditLogsTable
            // migration'ı) — bu yüzden kolon değişikliği burada ham SQL ile yapılıyor.
            // CustomerId artık nullable: yönetici hesabı oluşturma/pasifleştirme gibi
            // hiçbir müşteriyle ilgili olmayan denetim olayları da kaydedilebilsin diye
            // (bkz. AuditLog.cs, AdminUserService.cs).
            migrationBuilder.Sql("""
                ALTER TABLE [AuditLogs] DROP CONSTRAINT [FK_AuditLogs_Customers_CustomerId];

                ALTER TABLE [AuditLogs] ALTER COLUMN [CustomerId] uniqueidentifier NULL;

                ALTER TABLE [AuditLogs] ADD CONSTRAINT [FK_AuditLogs_Customers_CustomerId]
                    FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE CASCADE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Geri alma: nullable satırlar varsa NOT NULL'a çevirmeden önce silinmesi
            // gerekir (aksi halde ALTER COLUMN başarısız olur) — bilinçli olarak veri
            // kaybına yol açabilecek bu adım burada otomatik yapılmıyor, gerekirse elle
            // temizlenip tekrar denenmeli.
            migrationBuilder.Sql("""
                ALTER TABLE [AuditLogs] DROP CONSTRAINT [FK_AuditLogs_Customers_CustomerId];

                ALTER TABLE [AuditLogs] ALTER COLUMN [CustomerId] uniqueidentifier NOT NULL;

                ALTER TABLE [AuditLogs] ADD CONSTRAINT [FK_AuditLogs_Customers_CustomerId]
                    FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE CASCADE;
                """);
        }
    }
}
