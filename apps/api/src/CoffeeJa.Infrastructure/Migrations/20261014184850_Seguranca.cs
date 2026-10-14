using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeJa.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Seguranca : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AceiteTermosEm",
                table: "Usuarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BloqueadoAte",
                table: "Usuarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodigosRecuperacaoHash",
                table: "Usuarios",
                type: "nvarchar(800)",
                maxLength: 800,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExclusaoSolicitadaEm",
                table: "Usuarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FalhasLogin",
                table: "Usuarios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "Usuarios",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "TotpAtivo",
                table: "Usuarios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TotpSegredoProtegido",
                table: "Usuarios",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TotpUltimoPasso",
                table: "Usuarios",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "VersaoTermos",
                table: "Usuarios",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LogsAuditoria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Acao = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Alvo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Detalhe = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Ip = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogsAuditoria", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogsAuditoria_Acao",
                table: "LogsAuditoria",
                column: "Acao");

            migrationBuilder.CreateIndex(
                name: "IX_LogsAuditoria_CriadoEm",
                table: "LogsAuditoria",
                column: "CriadoEm");

            migrationBuilder.CreateIndex(
                name: "IX_LogsAuditoria_UsuarioId_CriadoEm",
                table: "LogsAuditoria",
                columns: new[] { "UsuarioId", "CriadoEm" });
        
            // Usuários existentes recebem um carimbo de sessão único (a coluna nova nasce vazia).
            migrationBuilder.Sql("UPDATE [Usuarios] SET [SecurityStamp] = LOWER(REPLACE(CONVERT(varchar(36), NEWID()), '-', '')) WHERE [SecurityStamp] = ''");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogsAuditoria");

            migrationBuilder.DropColumn(
                name: "AceiteTermosEm",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "BloqueadoAte",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "CodigosRecuperacaoHash",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "ExclusaoSolicitadaEm",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "FalhasLogin",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "TotpAtivo",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "TotpSegredoProtegido",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "TotpUltimoPasso",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "VersaoTermos",
                table: "Usuarios");
        }
    }
}
