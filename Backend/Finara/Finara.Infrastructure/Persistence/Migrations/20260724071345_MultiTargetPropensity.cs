using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MultiTargetPropensity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TargetPropensities",
                table: "Campaigns",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // Var olan tekil hedefleri kaybetmeden yeni çoklu-hedef kolonuna taşı.
            migrationBuilder.Sql(
                "UPDATE Campaigns SET TargetPropensities = CAST(TargetPropensity AS nvarchar(200)) WHERE TargetPropensity IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "TargetPropensity",
                table: "Campaigns");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TargetPropensity",
                table: "Campaigns",
                type: "int",
                nullable: true);

            // Down migration'da çoklu hedefin yalnızca ilk değeri korunur (tekil kolona geri dönüldüğü için).
            migrationBuilder.Sql(
                "UPDATE Campaigns SET TargetPropensity = TRY_CAST(LEFT(TargetPropensities, CHARINDEX(',', TargetPropensities + ',') - 1) AS int) WHERE TargetPropensities <> '';");

            migrationBuilder.DropColumn(
                name: "TargetPropensities",
                table: "Campaigns");
        }
    }
}
