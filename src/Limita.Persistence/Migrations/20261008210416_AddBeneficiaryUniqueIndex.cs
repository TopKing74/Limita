using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Limita.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBeneficiaryUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Beneficiaries_OwnerUserId",
                table: "Beneficiaries");

            migrationBuilder.CreateIndex(
                name: "IX_Beneficiaries_OwnerUserId_AccountNumber",
                table: "Beneficiaries",
                columns: new[] { "OwnerUserId", "AccountNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Beneficiaries_OwnerUserId_AccountNumber",
                table: "Beneficiaries");

            migrationBuilder.CreateIndex(
                name: "IX_Beneficiaries_OwnerUserId",
                table: "Beneficiaries",
                column: "OwnerUserId");
        }
    }
}
