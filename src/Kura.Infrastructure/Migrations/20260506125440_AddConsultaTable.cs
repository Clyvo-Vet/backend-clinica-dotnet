using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConsultaTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CONSULTA",
                columns: table => new
                {
                    ID_CONSULTA = table.Column<long>(type: "NUMBER(19)", nullable: false, defaultValueSql: "SEQ_CONSULTA.NEXTVAL"),
                    ID_EVENTO_CLINICO = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    DS_MOTIVO = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    DS_ANAMNESE = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    DS_EXAME_FISICO = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    DS_DIAGNOSTICO = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    DT_CONSULTA = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ST_ATIVA = table.Column<string>(type: "CHAR(1)", nullable: false),
                    DT_CRIACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DT_ATUALIZACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CONSULTA", x => x.ID_CONSULTA);
                    table.ForeignKey(
                        name: "FK_CONSULTA_EVENTO_CLINICO_ID_EVENTO_CLINICO",
                        column: x => x.ID_EVENTO_CLINICO,
                        principalTable: "EVENTO_CLINICO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CONSULTA_ID_EVENTO_CLINICO",
                table: "CONSULTA",
                column: "ID_EVENTO_CLINICO",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CONSULTA");
        }
    }
}
