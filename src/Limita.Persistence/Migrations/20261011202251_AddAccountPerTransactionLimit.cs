using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Limita.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountPerTransactionLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PerTransactionLimit",
                table: "Accounts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PerTransactionLimit",
                table: "Accounts");
        }
    }
}
