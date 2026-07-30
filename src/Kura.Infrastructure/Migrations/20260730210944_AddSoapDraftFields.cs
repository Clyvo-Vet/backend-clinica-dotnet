using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSoapDraftFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DS_SOAP_A",
                table: "EVENTO_CLINICO",
                type: "CLOB",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DS_SOAP_O",
                table: "EVENTO_CLINICO",
                type: "CLOB",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DS_SOAP_P",
                table: "EVENTO_CLINICO",
                type: "CLOB",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DS_SOAP_S",
                table: "EVENTO_CLINICO",
                type: "CLOB",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DS_TRANSCRICAO",
                table: "EVENTO_CLINICO",
                type: "CLOB",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ST_SOAP_CONFIRMADO",
                table: "EVENTO_CLINICO",
                type: "CHAR(1)",
                maxLength: 1,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DS_SOAP_A",
                table: "EVENTO_CLINICO");

            migrationBuilder.DropColumn(
                name: "DS_SOAP_O",
                table: "EVENTO_CLINICO");

            migrationBuilder.DropColumn(
                name: "DS_SOAP_P",
                table: "EVENTO_CLINICO");

            migrationBuilder.DropColumn(
                name: "DS_SOAP_S",
                table: "EVENTO_CLINICO");

            migrationBuilder.DropColumn(
                name: "DS_TRANSCRICAO",
                table: "EVENTO_CLINICO");

            migrationBuilder.DropColumn(
                name: "ST_SOAP_CONFIRMADO",
                table: "EVENTO_CLINICO");
        }
    }
}
