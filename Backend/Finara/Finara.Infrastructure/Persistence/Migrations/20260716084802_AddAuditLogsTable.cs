using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AuditLogs bilinçli olarak EF Core'un DbSet/model sistemine dahil değil —
            // bu tablonun CRUD'u Dapper ile (bkz. AuditLogRepository) manuel yönetiliyor.
            // Şema versiyonlaması yine de EF migration geçmişinden takip edilsin diye
            // tablo burada ham SQL ile oluşturuluyor.
            migrationBuilder.Sql(@"
                CREATE TABLE [AuditLogs] (
                    [Id] uniqueidentifier NOT NULL,
                    [CustomerId] uniqueidentifier NOT NULL,
                    [ActionType] int NOT NULL,
                    [Details] nvarchar(max) NULL,
                    [PerformedByUserId] uniqueidentifier NULL,
                    [OccurredAtUtc] datetime2 NOT NULL,
                    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_AuditLogs_Customers_CustomerId] FOREIGN KEY ([CustomerId])
                        REFERENCES [Customers] ([Id]) ON DELETE CASCADE
                );

                CREATE INDEX [IX_AuditLogs_CustomerId] ON [AuditLogs] ([CustomerId]);
                CREATE INDEX [IX_AuditLogs_OccurredAtUtc] ON [AuditLogs] ([OccurredAtUtc]);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE [AuditLogs];");
        }
    }
}
