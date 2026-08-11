using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Task77_InteracaoCanalClinicaNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Esta migration NUNCA é executada contra o Oracle real. Ela existe só como
            // evidência de rastreamento de modelo (MIGRATIONS_POLICY.md: "EF Core é o
            // GERADOR" — Database.Migrate() nunca é chamado em Program.cs). A DDL
            // correspondente (INTERACAO_CANAL.ID_CLINICA nullable) já foi aplicada em
            // produção pelo Flyway via V16__interacao_canal_clinica_nullable.sql
            // (backend-tutor-java, TASK-76/FIX_7) — Flyway é a única autoridade de DDL
            // deste projeto. Rodar este Up() manualmente contra o schema real seria
            // redundante na melhor hipótese (a coluna já é nullable) e um erro de
            // processo na pior (duas ferramentas de migration disputando o mesmo schema
            // compartilhado com o Java).
            migrationBuilder.AlterColumn<long>(
                name: "ID_CLINICA",
                table: "INTERACAO_CANAL",
                type: "NUMBER(19)",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "NUMBER(19)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "ID_CLINICA",
                table: "INTERACAO_CANAL",
                type: "NUMBER(19)",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "NUMBER(19)",
                oldNullable: true);
        }
    }
}
