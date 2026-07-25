using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDemoAnalystUser : Migration
    {
        // Finara.Domain.Common.DemoAccount.Id/Email ve DefaultBank.Id ile birebir aynı
        // olmalı — migration'lar donmuş anlar olduğu için (AddBankMultiTenancy'deki
        // desenle aynı gerekçe) domain sınıflarına referans verilmez, literal kullanılır.
        private static readonly Guid DemoUserId = new("00000000-0000-0000-0000-000000000002");
        private static readonly Guid DefaultBankId = new("00000000-0000-0000-0000-000000000001");

        // Hiçbir yerde kullanılmıyor/belgelenmiyor — demo-login akışı PasswordHasher.Verify'ı
        // hiç çağırmadığı için tamamen inert, sadece User entity'sinin NOT NULL/biçim
        // kısıtını karşılıyor.
        private const string DemoPasswordHash = "DmRhB0c+mqmGYX8EiSjzag==.u6aavRMWfmrBhax8OOwb7zrhQ77p9cLbvpMVKyzR4Sc=";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[]
                {
                    "Id", "BankId", "CreatedAtUtc", "Email", "IsActive",
                    "LastWeeklyDigestSentAtUtc", "PasswordHash", "Role",
                    "TwoFactorEnabled", "TwoFactorEnabledAtUtc", "TwoFactorSecretEncrypted", "UpdatedAtUtc"
                },
                values: new object[]
                {
                    DemoUserId, DefaultBankId, DateTime.UtcNow, "demo@finara.dev", true,
                    null, DemoPasswordHash, 2,
                    false, null, null, null
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: DemoUserId);
        }
    }
}
