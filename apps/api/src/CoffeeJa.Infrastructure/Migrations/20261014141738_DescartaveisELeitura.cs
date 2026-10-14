using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeJa.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DescartaveisELeitura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ContratanteLeuEm",
                table: "Propostas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FornecedorLeuEm",
                table: "Propostas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Descartaveis",
                table: "Demandas",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContratanteLeuEm",
                table: "Propostas");

            migrationBuilder.DropColumn(
                name: "FornecedorLeuEm",
                table: "Propostas");

            migrationBuilder.DropColumn(
                name: "Descartaveis",
                table: "Demandas");
        }
    }
}
