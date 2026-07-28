using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Boş string ("") geçerli bir JSON değeri DEĞİL — var olan satırlar için bu
            // varsayılanla EF, Preferences kolonunu okurken JsonReaderException ile
            // patlıyordu (canlı ortamda bulundu: demo-login/normal login artık her zaman
            // User'ı tam materialize ediyor, JSON-owned Preferences dahil). Varsayılan,
            // UserPreferences'ın gerçek varsayılan değerleriyle EF'in ürettiği JSON'un
            // birebir aynısı olmalı (enum'lar EF'in JSON kolon desteğinde varsayılan
            // olarak sayısal değer olarak yazılıyor, string değil).
            migrationBuilder.AddColumn<string>(
                name: "Preferences",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{\"DateFormat\":0,\"DefaultPageSize\":20,\"Language\":0,\"ReceiveWeeklyDigestEmail\":true,\"TableDensity\":0,\"TimeZoneId\":\"Europe/Istanbul\"}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Preferences",
                table: "Users");
        }
    }
}
