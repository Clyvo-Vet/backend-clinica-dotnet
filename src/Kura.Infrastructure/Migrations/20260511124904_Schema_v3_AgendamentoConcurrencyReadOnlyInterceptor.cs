using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Schema_v3_AgendamentoConcurrencyReadOnlyInterceptor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ST_ENCAMINHADO_VET",
                table: "TRIAGEM_LUNA",
                type: "NUMBER(10)",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "BOOLEAN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "ST_ENCAMINHADO_VET",
                table: "TRIAGEM_LUNA",
                type: "BOOLEAN",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "NUMBER(10)");
        }
    }
}
