using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInviteTutor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "INVITE_TUTOR",
                columns: table => new
                {
                    ID_INVITE_TUTOR = table.Column<long>(type: "NUMBER(19)", nullable: false, defaultValueSql: "SEQ_INVITE_TUTOR.NEXTVAL"),
                    ID_TUTOR = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    NR_TOKEN = table.Column<Guid>(type: "RAW(16)", maxLength: 36, nullable: false),
                    DT_EXPIRACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DS_CANAL = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    ST_UTILIZADO = table.Column<string>(type: "CHAR(1)", nullable: false),
                    ST_ATIVA = table.Column<string>(type: "CHAR(1)", nullable: false),
                    DT_CRIACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DT_ATUALIZACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INVITE_TUTOR", x => x.ID_INVITE_TUTOR);
                    table.ForeignKey(
                        name: "FK_INVITE_TUTOR_TUTOR_ID_TUTOR",
                        column: x => x.ID_TUTOR,
                        principalTable: "TUTOR",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_INVITE_TUTOR_ID_TUTOR",
                table: "INVITE_TUTOR",
                column: "ID_TUTOR");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "INVITE_TUTOR");
        }
    }
}
