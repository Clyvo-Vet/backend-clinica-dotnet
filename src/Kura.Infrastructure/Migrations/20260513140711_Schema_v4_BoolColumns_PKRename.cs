using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Schema_v4_BoolColumns_PKRename : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LOG_ERRO");

            migrationBuilder.DropColumn(
                name: "ST_ATIVA",
                table: "VACINA");

            migrationBuilder.DropColumn(
                name: "ST_ATIVA",
                table: "TIPO_EVENTO");

            migrationBuilder.DropColumn(
                name: "ST_ATIVA",
                table: "RACA");

            migrationBuilder.DropColumn(
                name: "ST_ATIVA",
                table: "PRESCRICAO");

            migrationBuilder.DropColumn(
                name: "ST_ATIVA",
                table: "NOTIFICACAO");

            migrationBuilder.DropColumn(
                name: "ST_ATIVA",
                table: "MEDICAMENTO");

            migrationBuilder.DropColumn(
                name: "ST_ATIVA",
                table: "LEITURA_TEMPERATURA");

            migrationBuilder.DropColumn(
                name: "ST_ATIVA",
                table: "EXAME");

            migrationBuilder.DropColumn(
                name: "ST_ATIVA",
                table: "EVENTO_CLINICO");

            migrationBuilder.DropColumn(
                name: "ST_ATIVA",
                table: "ESPECIE");

            migrationBuilder.DropColumn(
                name: "ST_ATIVA",
                table: "DOCUMENTO");

            migrationBuilder.DropColumn(
                name: "ST_ATIVA",
                table: "DISPOSITIVO_IOT");

            migrationBuilder.DropColumn(
                name: "DS_SENHA",
                table: "CLINICA");

            migrationBuilder.DropColumn(
                name: "DT_ATUALIZACAO",
                table: "CLINICA");

            migrationBuilder.DropColumn(
                name: "ST_ATIVA",
                table: "ALERTA_TEMPERATURA");

            migrationBuilder.DropColumn(
                name: "ST_ATIVA",
                table: "AGENDAMENTO");

            migrationBuilder.RenameColumn(
                name: "ST_ATIVA",
                table: "VETERINARIO",
                newName: "ST_ATIVO");

            migrationBuilder.RenameColumn(
                name: "ID",
                table: "VETERINARIO",
                newName: "ID_VETERINARIO");

            migrationBuilder.RenameColumn(
                name: "ID",
                table: "VACINA",
                newName: "ID_VACINA");

            migrationBuilder.RenameColumn(
                name: "ST_ATIVA",
                table: "TUTOR",
                newName: "ST_ATIVO");

            migrationBuilder.RenameColumn(
                name: "ID",
                table: "TUTOR",
                newName: "ID_TUTOR");

            migrationBuilder.RenameColumn(
                name: "ID",
                table: "TIPO_EVENTO",
                newName: "ID_TIPO_EVENTO");

            migrationBuilder.RenameColumn(
                name: "ID",
                table: "RACA",
                newName: "ID_RACA");

            migrationBuilder.RenameColumn(
                name: "ID",
                table: "PRESCRICAO",
                newName: "ID_PRESCRICAO");

            migrationBuilder.RenameColumn(
                name: "ST_ATIVA",
                table: "PET",
                newName: "ST_ATIVO");

            migrationBuilder.RenameColumn(
                name: "ID",
                table: "PET",
                newName: "ID_PET");

            migrationBuilder.RenameColumn(
                name: "ID",
                table: "NOTIFICACAO",
                newName: "ID_NOTIFICACAO");

            migrationBuilder.RenameColumn(
                name: "ID",
                table: "MEDICAMENTO",
                newName: "ID_MEDICAMENTO");

            migrationBuilder.RenameColumn(
                name: "ID",
                table: "LEITURA_TEMPERATURA",
                newName: "ID_LEITURA");

            migrationBuilder.RenameColumn(
                name: "ST_ATIVA",
                table: "INVITE_TUTOR",
                newName: "ST_ATIVO");

            migrationBuilder.RenameColumn(
                name: "ID",
                table: "EXAME",
                newName: "ID_EXAME");

            migrationBuilder.RenameColumn(
                name: "ID",
                table: "EVENTO_CLINICO",
                newName: "ID_EVENTO");

            migrationBuilder.RenameColumn(
                name: "ID",
                table: "ESPECIE",
                newName: "ID_ESPECIE");

            migrationBuilder.RenameColumn(
                name: "ID",
                table: "DOCUMENTO",
                newName: "ID_DOCUMENTO");

            migrationBuilder.RenameColumn(
                name: "ID",
                table: "DISPOSITIVO_IOT",
                newName: "ID_DISPOSITIVO");

            migrationBuilder.RenameColumn(
                name: "NR_TELEFONE",
                table: "CLINICA",
                newName: "DS_TELEFONE");

            migrationBuilder.RenameColumn(
                name: "ID",
                table: "CLINICA",
                newName: "ID_CLINICA");

            migrationBuilder.RenameColumn(
                name: "DT_CRIACAO",
                table: "CLINICA",
                newName: "DT_CADASTRO");

            migrationBuilder.RenameColumn(
                name: "ID",
                table: "ALERTA_TEMPERATURA",
                newName: "ID_ALERTA");

            migrationBuilder.AlterColumn<string>(
                name: "ST_ENCAMINHADO_VET",
                table: "TRIAGEM_LUNA",
                type: "CHAR(1)",
                maxLength: 1,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "NUMBER(10)");

            migrationBuilder.AlterColumn<long>(
                name: "ID_LEITURA",
                table: "LEITURA_TEMPERATURA",
                type: "NUMBER(19)",
                nullable: false,
                defaultValueSql: "SEQ_LEITURA_TEMP.NEXTVAL",
                oldClrType: typeof(long),
                oldType: "NUMBER(19)",
                oldDefaultValueSql: "SEQ_LEITURA_TEMPERATURA.NEXTVAL");

            migrationBuilder.AlterColumn<string>(
                name: "NR_CNPJ",
                table: "CLINICA",
                type: "NVARCHAR2(18)",
                maxLength: 18,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(14)",
                oldMaxLength: 14);

            migrationBuilder.AlterColumn<string>(
                name: "NM_CLINICA",
                table: "CLINICA",
                type: "NVARCHAR2(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "DS_SENHA_HASH",
                table: "CLINICA",
                type: "NVARCHAR2(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "DS_ENDERECO",
                table: "CLINICA",
                type: "NVARCHAR2(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "DS_EMAIL_ACESSO",
                table: "CLINICA",
                type: "NVARCHAR2(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "DS_EMAIL",
                table: "CLINICA",
                type: "NVARCHAR2(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "DS_TELEFONE",
                table: "CLINICA",
                type: "NVARCHAR2(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<string>(
                name: "NM_CIDADE",
                table: "CLINICA",
                type: "NVARCHAR2(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NM_RAZAO_SOCIAL",
                table: "CLINICA",
                type: "NVARCHAR2(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NR_CEP",
                table: "CLINICA",
                type: "NVARCHAR2(9)",
                maxLength: 9,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SG_UF",
                table: "CLINICA",
                type: "NVARCHAR2(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<long>(
                name: "ID_ALERTA",
                table: "ALERTA_TEMPERATURA",
                type: "NUMBER(19)",
                nullable: false,
                defaultValueSql: "SEQ_ALERTA_TEMP.NEXTVAL",
                oldClrType: typeof(long),
                oldType: "NUMBER(19)",
                oldDefaultValueSql: "SEQ_ALERTA_TEMPERATURA.NEXTVAL");

            migrationBuilder.CreateIndex(
                name: "IX_CLINICA_DS_EMAIL_ACESSO",
                table: "CLINICA",
                column: "DS_EMAIL_ACESSO",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CLINICA_DS_EMAIL_ACESSO",
                table: "CLINICA");

            migrationBuilder.DropColumn(
                name: "NM_CIDADE",
                table: "CLINICA");

            migrationBuilder.DropColumn(
                name: "NM_RAZAO_SOCIAL",
                table: "CLINICA");

            migrationBuilder.DropColumn(
                name: "NR_CEP",
                table: "CLINICA");

            migrationBuilder.DropColumn(
                name: "SG_UF",
                table: "CLINICA");

            migrationBuilder.RenameColumn(
                name: "ST_ATIVO",
                table: "VETERINARIO",
                newName: "ST_ATIVA");

            migrationBuilder.RenameColumn(
                name: "ID_VETERINARIO",
                table: "VETERINARIO",
                newName: "ID");

            migrationBuilder.RenameColumn(
                name: "ID_VACINA",
                table: "VACINA",
                newName: "ID");

            migrationBuilder.RenameColumn(
                name: "ST_ATIVO",
                table: "TUTOR",
                newName: "ST_ATIVA");

            migrationBuilder.RenameColumn(
                name: "ID_TUTOR",
                table: "TUTOR",
                newName: "ID");

            migrationBuilder.RenameColumn(
                name: "ID_TIPO_EVENTO",
                table: "TIPO_EVENTO",
                newName: "ID");

            migrationBuilder.RenameColumn(
                name: "ID_RACA",
                table: "RACA",
                newName: "ID");

            migrationBuilder.RenameColumn(
                name: "ID_PRESCRICAO",
                table: "PRESCRICAO",
                newName: "ID");

            migrationBuilder.RenameColumn(
                name: "ST_ATIVO",
                table: "PET",
                newName: "ST_ATIVA");

            migrationBuilder.RenameColumn(
                name: "ID_PET",
                table: "PET",
                newName: "ID");

            migrationBuilder.RenameColumn(
                name: "ID_NOTIFICACAO",
                table: "NOTIFICACAO",
                newName: "ID");

            migrationBuilder.RenameColumn(
                name: "ID_MEDICAMENTO",
                table: "MEDICAMENTO",
                newName: "ID");

            migrationBuilder.RenameColumn(
                name: "ID_LEITURA",
                table: "LEITURA_TEMPERATURA",
                newName: "ID");

            migrationBuilder.RenameColumn(
                name: "ST_ATIVO",
                table: "INVITE_TUTOR",
                newName: "ST_ATIVA");

            migrationBuilder.RenameColumn(
                name: "ID_EXAME",
                table: "EXAME",
                newName: "ID");

            migrationBuilder.RenameColumn(
                name: "ID_EVENTO",
                table: "EVENTO_CLINICO",
                newName: "ID");

            migrationBuilder.RenameColumn(
                name: "ID_ESPECIE",
                table: "ESPECIE",
                newName: "ID");

            migrationBuilder.RenameColumn(
                name: "ID_DOCUMENTO",
                table: "DOCUMENTO",
                newName: "ID");

            migrationBuilder.RenameColumn(
                name: "ID_DISPOSITIVO",
                table: "DISPOSITIVO_IOT",
                newName: "ID");

            migrationBuilder.RenameColumn(
                name: "DS_TELEFONE",
                table: "CLINICA",
                newName: "NR_TELEFONE");

            migrationBuilder.RenameColumn(
                name: "ID_CLINICA",
                table: "CLINICA",
                newName: "ID");

            migrationBuilder.RenameColumn(
                name: "DT_CADASTRO",
                table: "CLINICA",
                newName: "DT_CRIACAO");

            migrationBuilder.RenameColumn(
                name: "ID_ALERTA",
                table: "ALERTA_TEMPERATURA",
                newName: "ID");

            migrationBuilder.AddColumn<string>(
                name: "ST_ATIVA",
                table: "VACINA",
                type: "CHAR(1)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "ST_ENCAMINHADO_VET",
                table: "TRIAGEM_LUNA",
                type: "NUMBER(10)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "CHAR(1)",
                oldMaxLength: 1);

            migrationBuilder.AddColumn<string>(
                name: "ST_ATIVA",
                table: "TIPO_EVENTO",
                type: "CHAR(1)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ST_ATIVA",
                table: "RACA",
                type: "CHAR(1)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ST_ATIVA",
                table: "PRESCRICAO",
                type: "CHAR(1)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ST_ATIVA",
                table: "NOTIFICACAO",
                type: "CHAR(1)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ST_ATIVA",
                table: "MEDICAMENTO",
                type: "CHAR(1)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<long>(
                name: "ID",
                table: "LEITURA_TEMPERATURA",
                type: "NUMBER(19)",
                nullable: false,
                defaultValueSql: "SEQ_LEITURA_TEMPERATURA.NEXTVAL",
                oldClrType: typeof(long),
                oldType: "NUMBER(19)",
                oldDefaultValueSql: "SEQ_LEITURA_TEMP.NEXTVAL");

            migrationBuilder.AddColumn<string>(
                name: "ST_ATIVA",
                table: "LEITURA_TEMPERATURA",
                type: "CHAR(1)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ST_ATIVA",
                table: "EXAME",
                type: "CHAR(1)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ST_ATIVA",
                table: "EVENTO_CLINICO",
                type: "CHAR(1)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ST_ATIVA",
                table: "ESPECIE",
                type: "CHAR(1)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ST_ATIVA",
                table: "DOCUMENTO",
                type: "CHAR(1)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ST_ATIVA",
                table: "DISPOSITIVO_IOT",
                type: "CHAR(1)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "NR_CNPJ",
                table: "CLINICA",
                type: "NVARCHAR2(14)",
                maxLength: 14,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(18)",
                oldMaxLength: 18);

            migrationBuilder.AlterColumn<string>(
                name: "NM_CLINICA",
                table: "CLINICA",
                type: "NVARCHAR2(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "DS_SENHA_HASH",
                table: "CLINICA",
                type: "NVARCHAR2(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "DS_ENDERECO",
                table: "CLINICA",
                type: "NVARCHAR2(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "DS_EMAIL_ACESSO",
                table: "CLINICA",
                type: "NVARCHAR2(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "DS_EMAIL",
                table: "CLINICA",
                type: "NVARCHAR2(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "NR_TELEFONE",
                table: "CLINICA",
                type: "NVARCHAR2(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DS_SENHA",
                table: "CLINICA",
                type: "NVARCHAR2(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DT_ATUALIZACAO",
                table: "CLINICA",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "ID",
                table: "ALERTA_TEMPERATURA",
                type: "NUMBER(19)",
                nullable: false,
                defaultValueSql: "SEQ_ALERTA_TEMPERATURA.NEXTVAL",
                oldClrType: typeof(long),
                oldType: "NUMBER(19)",
                oldDefaultValueSql: "SEQ_ALERTA_TEMP.NEXTVAL");

            migrationBuilder.AddColumn<string>(
                name: "ST_ATIVA",
                table: "ALERTA_TEMPERATURA",
                type: "CHAR(1)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ST_ATIVA",
                table: "AGENDAMENTO",
                type: "CHAR(1)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "LOG_ERRO",
                columns: table => new
                {
                    ID_LOG = table.Column<long>(type: "NUMBER(19)", nullable: false, defaultValueSql: "SEQ_LOG_ERRO.NEXTVAL"),
                    DS_ENDPOINT = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    DS_MENSAGEM = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    DS_METODO = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    DS_STACK_TRACE = table.Column<string>(type: "CLOB", nullable: true),
                    DT_OCORRENCIA = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    NR_STATUS_CODE = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LOG_ERRO", x => x.ID_LOG);
                });
        }
    }
}
