using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kura.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Task33_InviteTokenCanonicalString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "NR_TOKEN",
                table: "INVITE_TUTOR",
                type: "NVARCHAR2(36)",
                maxLength: 36,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "RAW(16)",
                oldMaxLength: 36);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "NR_TOKEN",
                table: "INVITE_TUTOR",
                type: "RAW(16)",
                maxLength: 36,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(36)",
                oldMaxLength: 36);
        }
    }
}
