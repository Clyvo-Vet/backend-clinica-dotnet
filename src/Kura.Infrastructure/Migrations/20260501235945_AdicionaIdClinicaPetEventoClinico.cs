using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaIdClinicaPetEventoClinico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ID_CLINICA",
                table: "PET",
                type: "NUMBER(19)",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "ID_CLINICA",
                table: "EVENTO_CLINICO",
                type: "NUMBER(19)",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ID_CLINICA",
                table: "PET");

            migrationBuilder.DropColumn(
                name: "ID_CLINICA",
                table: "EVENTO_CLINICO");
        }
    }
}
