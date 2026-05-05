using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicaAuthFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DS_EMAIL_ACESSO",
                table: "CLINICA",
                type: "NVARCHAR2(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DS_SENHA_HASH",
                table: "CLINICA",
                type: "NVARCHAR2(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "AGENDAMENTO",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ID_PET = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    ID_VETERINARIO = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    NM_PACIENTE = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    DT_AGENDAMENTO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DS_SERVICO = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    ST_STATUS = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false)
                },
                constraints: table =>
                {
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AGENDAMENTO");

            migrationBuilder.DropColumn(
                name: "DS_EMAIL_ACESSO",
                table: "CLINICA");

            migrationBuilder.DropColumn(
                name: "DS_SENHA_HASH",
                table: "CLINICA");
        }
    }
}
