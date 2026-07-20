using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBankMultiTenancy : Migration
    {
        // Finara.Domain.Common.DefaultBank.Id ile birebir aynı olmalı — mevcut tüm
        // Customer/Transaction/Segment/Campaign/CampaignOffer satırları bu bankaya bağlanıyor.
        private static readonly Guid DefaultBankId = new("00000000-0000-0000-0000-000000000001");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Banks tablosu ve seed satırı, BankId kolonlarını eklemeden ÖNCE var olmalı —
            // aksi halde hem FK constraint'i (aşağıda) ihlal edilir hem de geriye dönük
            // doldurulan satırlar domain'in "BankId boş olamaz" kuralına aykırı düşerdi.
            migrationBuilder.CreateTable(
                name: "Banks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Banks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Banks_Code",
                table: "Banks",
                column: "Code",
                unique: true);

            migrationBuilder.InsertData(
                table: "Banks",
                columns: new[] { "Id", "Name", "Code", "IsActive", "CreatedAtUtc", "UpdatedAtUtc" },
                values: new object[] { DefaultBankId, "Varsayılan Banka", "DEFAULT", true, DateTime.UtcNow, null });

            // AuditLogs, EF'in DbSet/model sistemine dahil değil (bkz. AddAuditLogsTable
            // migration'ı) — bu yüzden BankId de burada ham SQL ile ekleniyor, Banks'a FK
            // dahil (diğer tüm tablolarla tutarlı).
            migrationBuilder.Sql($"""
                ALTER TABLE [AuditLogs] ADD [BankId] uniqueidentifier NOT NULL
                    CONSTRAINT [DF_AuditLogs_BankId] DEFAULT '{DefaultBankId}';

                ALTER TABLE [AuditLogs] ADD CONSTRAINT [FK_AuditLogs_Banks_BankId]
                    FOREIGN KEY ([BankId]) REFERENCES [Banks] ([Id]);

                CREATE INDEX [IX_AuditLogs_BankId] ON [AuditLogs] ([BankId]);
                """);

            migrationBuilder.DropIndex(
                name: "IX_Customers_Email",
                table: "Customers");

            migrationBuilder.AddColumn<Guid>(
                name: "BankId",
                table: "Transactions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: DefaultBankId);

            migrationBuilder.AddColumn<Guid>(
                name: "BankId",
                table: "Segments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: DefaultBankId);

            migrationBuilder.AddColumn<Guid>(
                name: "BankId",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: DefaultBankId);

            migrationBuilder.AddColumn<Guid>(
                name: "BankId",
                table: "Campaigns",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: DefaultBankId);

            migrationBuilder.AddColumn<Guid>(
                name: "BankId",
                table: "CampaignOffers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: DefaultBankId);

            migrationBuilder.CreateIndex(
                name: "IX_Users_BankId",
                table: "Users",
                column: "BankId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BankId",
                table: "Transactions",
                column: "BankId");

            migrationBuilder.CreateIndex(
                name: "IX_Segments_BankId",
                table: "Segments",
                column: "BankId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_BankId_Email",
                table: "Customers",
                columns: new[] { "BankId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_BankId",
                table: "Campaigns",
                column: "BankId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignOffers_BankId",
                table: "CampaignOffers",
                column: "BankId");

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignOffers_Banks_BankId",
                table: "CampaignOffers",
                column: "BankId",
                principalTable: "Banks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Campaigns_Banks_BankId",
                table: "Campaigns",
                column: "BankId",
                principalTable: "Banks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Banks_BankId",
                table: "Customers",
                column: "BankId",
                principalTable: "Banks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Segments_Banks_BankId",
                table: "Segments",
                column: "BankId",
                principalTable: "Banks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Banks_BankId",
                table: "Transactions",
                column: "BankId",
                principalTable: "Banks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Banks_BankId",
                table: "Users",
                column: "BankId",
                principalTable: "Banks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE [AuditLogs] DROP CONSTRAINT [FK_AuditLogs_Banks_BankId];
                DROP INDEX [IX_AuditLogs_BankId] ON [AuditLogs];
                ALTER TABLE [AuditLogs] DROP CONSTRAINT [DF_AuditLogs_BankId];
                ALTER TABLE [AuditLogs] DROP COLUMN [BankId];
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_CampaignOffers_Banks_BankId",
                table: "CampaignOffers");

            migrationBuilder.DropForeignKey(
                name: "FK_Campaigns_Banks_BankId",
                table: "Campaigns");

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Banks_BankId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_Segments_Banks_BankId",
                table: "Segments");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Banks_BankId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Banks_BankId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Banks");

            migrationBuilder.DropIndex(
                name: "IX_Users_BankId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_BankId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Segments_BankId",
                table: "Segments");

            migrationBuilder.DropIndex(
                name: "IX_Customers_BankId_Email",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Campaigns_BankId",
                table: "Campaigns");

            migrationBuilder.DropIndex(
                name: "IX_CampaignOffers_BankId",
                table: "CampaignOffers");

            migrationBuilder.DropColumn(
                name: "BankId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "BankId",
                table: "Segments");

            migrationBuilder.DropColumn(
                name: "BankId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "BankId",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "BankId",
                table: "CampaignOffers");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Email",
                table: "Customers",
                column: "Email",
                unique: true);
        }
    }
}
