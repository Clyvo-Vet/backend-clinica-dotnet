using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kura.Infrastructure.Migrations
{
    // Evidência FIAP — nunca aplicada em produção (Flyway é a autoridade de DDL, ver CLAUDE.md).
    // Além das colunas de teleconsulta (TASK-10/V10), esta migration também mirra drift de
    // modelo acumulado desde a última migration EF (2026-05-13) que nunca havia sido
    // espelhado — mesmo padrão do V9 Flyway (schema drift catch-up).
    /// <inheritdoc />
    public partial class AddTeleconsultaFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DS_STATUS",
                table: "AGENDAMENTO");

            migrationBuilder.RenameColumn(
                name: "NR_TELEFONE",
                table: "TUTOR",
                newName: "DS_TELEFONE");

            migrationBuilder.RenameColumn(
                name: "DT_CRIACAO",
                table: "TUTOR",
                newName: "DT_CADASTRO");

            migrationBuilder.RenameColumn(
                name: "ID_INVITE_TUTOR",
                table: "INVITE_TUTOR",
                newName: "ID_INVITE");

            migrationBuilder.RenameColumn(
                name: "DS_TIPO_CONSULTA",
                table: "AGENDAMENTO",
                newName: "DS_TIPO");

            migrationBuilder.AddColumn<string>(
                name: "DS_VERSAO_AVISO",
                table: "TUTOR",
                type: "NVARCHAR2(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DT_AVISO_PRIVACIDADE",
                table: "TUTOR",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ID_CLINICA",
                table: "TUTOR",
                type: "NUMBER(19)",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "ST_AVISO_PRIVACIDADE",
                table: "TUTOR",
                type: "CHAR(1)",
                maxLength: 1,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "NM_RACA",
                table: "RACA",
                type: "NVARCHAR2(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "NM_PET",
                table: "PET",
                type: "NVARCHAR2(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "ST_STATUS",
                table: "AGENDAMENTO",
                type: "NVARCHAR2(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<int>(
                name: "NR_DURACAO_MINUTOS",
                table: "AGENDAMENTO",
                type: "NUMBER(10)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "NUMBER(10)");

            migrationBuilder.AlterColumn<string>(
                name: "NM_PACIENTE",
                table: "AGENDAMENTO",
                type: "NVARCHAR2(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "DS_SERVICO",
                table: "AGENDAMENTO",
                type: "NVARCHAR2(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "DS_ORIGEM",
                table: "AGENDAMENTO",
                type: "NVARCHAR2(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "DS_TIPO",
                table: "AGENDAMENTO",
                type: "NVARCHAR2(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<string>(
                name: "DS_MOTIVO_CANCEL",
                table: "AGENDAMENTO",
                type: "NVARCHAR2(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DS_OBSERVACOES",
                table: "AGENDAMENTO",
                type: "NVARCHAR2(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DS_PROVEDOR_VIDEO",
                table: "AGENDAMENTO",
                type: "NVARCHAR2(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DS_SALA_URL",
                table: "AGENDAMENTO",
                type: "NVARCHAR2(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DT_CANCELAMENTO",
                table: "AGENDAMENTO",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DT_CONFIRMACAO",
                table: "AGENDAMENTO",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DT_CRIACAO",
                table: "AGENDAMENTO",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DT_FIM_SESSAO",
                table: "AGENDAMENTO",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DT_INICIO_SESSAO",
                table: "AGENDAMENTO",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ID_EVENTO_GERADO",
                table: "AGENDAMENTO",
                type: "NUMBER(19)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ST_TELECONSULTA",
                table: "AGENDAMENTO",
                type: "CHAR(1)",
                maxLength: 1,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DS_VERSAO_AVISO",
                table: "TUTOR");

            migrationBuilder.DropColumn(
                name: "DT_AVISO_PRIVACIDADE",
                table: "TUTOR");

            migrationBuilder.DropColumn(
                name: "ID_CLINICA",
                table: "TUTOR");

            migrationBuilder.DropColumn(
                name: "ST_AVISO_PRIVACIDADE",
                table: "TUTOR");

            migrationBuilder.DropColumn(
                name: "DS_MOTIVO_CANCEL",
                table: "AGENDAMENTO");

            migrationBuilder.DropColumn(
                name: "DS_OBSERVACOES",
                table: "AGENDAMENTO");

            migrationBuilder.DropColumn(
                name: "DS_PROVEDOR_VIDEO",
                table: "AGENDAMENTO");

            migrationBuilder.DropColumn(
                name: "DS_SALA_URL",
                table: "AGENDAMENTO");

            migrationBuilder.DropColumn(
                name: "DT_CANCELAMENTO",
                table: "AGENDAMENTO");

            migrationBuilder.DropColumn(
                name: "DT_CONFIRMACAO",
                table: "AGENDAMENTO");

            migrationBuilder.DropColumn(
                name: "DT_CRIACAO",
                table: "AGENDAMENTO");

            migrationBuilder.DropColumn(
                name: "DT_FIM_SESSAO",
                table: "AGENDAMENTO");

            migrationBuilder.DropColumn(
                name: "DT_INICIO_SESSAO",
                table: "AGENDAMENTO");

            migrationBuilder.DropColumn(
                name: "ID_EVENTO_GERADO",
                table: "AGENDAMENTO");

            migrationBuilder.DropColumn(
                name: "ST_TELECONSULTA",
                table: "AGENDAMENTO");

            migrationBuilder.RenameColumn(
                name: "DT_CADASTRO",
                table: "TUTOR",
                newName: "DT_CRIACAO");

            migrationBuilder.RenameColumn(
                name: "DS_TELEFONE",
                table: "TUTOR",
                newName: "NR_TELEFONE");

            migrationBuilder.RenameColumn(
                name: "ID_INVITE",
                table: "INVITE_TUTOR",
                newName: "ID_INVITE_TUTOR");

            migrationBuilder.RenameColumn(
                name: "DS_TIPO",
                table: "AGENDAMENTO",
                newName: "DS_TIPO_CONSULTA");

            migrationBuilder.AlterColumn<string>(
                name: "NM_RACA",
                table: "RACA",
                type: "NVARCHAR2(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<string>(
                name: "NM_PET",
                table: "PET",
                type: "NVARCHAR2(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<string>(
                name: "ST_STATUS",
                table: "AGENDAMENTO",
                type: "NVARCHAR2(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "NR_DURACAO_MINUTOS",
                table: "AGENDAMENTO",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "NUMBER(10)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NM_PACIENTE",
                table: "AGENDAMENTO",
                type: "NVARCHAR2(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DS_SERVICO",
                table: "AGENDAMENTO",
                type: "NVARCHAR2(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DS_ORIGEM",
                table: "AGENDAMENTO",
                type: "NVARCHAR2(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DS_TIPO_CONSULTA",
                table: "AGENDAMENTO",
                type: "NVARCHAR2(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DS_STATUS",
                table: "AGENDAMENTO",
                type: "NVARCHAR2(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }
    }
}
