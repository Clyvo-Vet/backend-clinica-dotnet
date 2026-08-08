using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Task67_InteracaoCanal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ID_INTERACAO",
                table: "TRIAGEM_LUNA",
                type: "NUMBER(19)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "INTERACAO_CANAL",
                columns: table => new
                {
                    ID_INTERACAO = table.Column<long>(type: "NUMBER(19)", nullable: false, defaultValueSql: "SEQ_INTERACAO_CANAL.NEXTVAL"),
                    ID_CLINICA = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ID_TUTOR = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    DS_CANAL = table.Column<string>(type: "VARCHAR2(20)", maxLength: 20, nullable: false),
                    DS_DIRECAO = table.Column<string>(type: "VARCHAR2(20)", maxLength: 20, nullable: false),
                    DS_CONTEUDO = table.Column<string>(type: "VARCHAR2(4000)", maxLength: 4000, nullable: false),
                    DT_RECEBIMENTO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DS_METADADOS = table.Column<string>(type: "CLOB", nullable: true),
                    ST_ATIVA = table.Column<string>(type: "CHAR(1)", maxLength: 1, nullable: false),
                    DT_CRIACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DT_ATUALIZACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INTERACAO_CANAL", x => x.ID_INTERACAO);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "INTERACAO_CANAL");

            migrationBuilder.DropColumn(
                name: "ID_INTERACAO",
                table: "TRIAGEM_LUNA");
        }
    }
}
