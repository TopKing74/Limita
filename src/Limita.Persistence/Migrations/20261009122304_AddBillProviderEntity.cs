using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Limita.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBillProviderEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_BillProviders_Name",
                table: "BillProviders",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BillProviders_Name",
                table: "BillProviders");
        }
    }
}
