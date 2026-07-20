using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserBankIdAndIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BankId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            // Varsayılan true olmalı — false verilirse mevcut kullanıcılar (admin fixture
            // dahil) migration uygulanır uygulanmaz sessizce pasifleştirilmiş olurdu.
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Users");
        }
    }
}
