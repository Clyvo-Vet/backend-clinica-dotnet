using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTriagemLunaTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TRIAGEM_LUNA",
                columns: table => new
                {
                    ID_TRIAGEM = table.Column<long>(type: "NUMBER(19)", nullable: false, defaultValueSql: "SEQ_TRIAGEM_LUNA.NEXTVAL"),
                    ID_CLINICA = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ID_TUTOR = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    ID_PET = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    DS_NIVEL_URGENCIA = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    DS_DESCRICAO = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    ST_ENCAMINHADO_VET = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    DT_TRIAGEM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ST_ATIVA = table.Column<string>(type: "CHAR(1)", nullable: false),
                    DT_CRIACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DT_ATUALIZACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRIAGEM_LUNA", x => x.ID_TRIAGEM);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TRIAGEM_LUNA");
        }
    }
}
