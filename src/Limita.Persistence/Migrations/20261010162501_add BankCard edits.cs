using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Limita.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class addBankCardedits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BlockReason",
                table: "BankCards",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BlockType",
                table: "BankCards",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BlockedAt",
                table: "BankCards",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DailyAtmLimit",
                table: "BankCards",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DailyPosLimit",
                table: "BankCards",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Form",
                table: "BankCards",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PinHash",
                table: "BankCards",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlockReason",
                table: "BankCards");

            migrationBuilder.DropColumn(
                name: "BlockType",
                table: "BankCards");

            migrationBuilder.DropColumn(
                name: "BlockedAt",
                table: "BankCards");

            migrationBuilder.DropColumn(
                name: "DailyAtmLimit",
                table: "BankCards");

            migrationBuilder.DropColumn(
                name: "DailyPosLimit",
                table: "BankCards");

            migrationBuilder.DropColumn(
                name: "Form",
                table: "BankCards");

            migrationBuilder.DropColumn(
                name: "PinHash",
                table: "BankCards");
        }
    }
}
