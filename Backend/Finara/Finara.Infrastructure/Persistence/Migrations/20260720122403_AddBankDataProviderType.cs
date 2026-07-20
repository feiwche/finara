using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBankDataProviderType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DataProviderType",
                table: "Banks",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataProviderType",
                table: "Banks");
        }
    }
}
