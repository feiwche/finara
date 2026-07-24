using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserWeeklyDigestTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastWeeklyDigestSentAtUtc",
                table: "Users",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastWeeklyDigestSentAtUtc",
                table: "Users");
        }
    }
}
