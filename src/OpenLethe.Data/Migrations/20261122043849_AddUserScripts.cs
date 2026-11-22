using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenLethe.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserScripts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "script_grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ScriptId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GranteeAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Capabilities = table.Column<string>(type: "TEXT", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_script_grants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_scripts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Revision = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_scripts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_script_grants_ScriptId_GranteeAccountId",
                table: "script_grants",
                columns: new[] { "ScriptId", "GranteeAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_scripts_AccountId_Name",
                table: "user_scripts",
                columns: new[] { "AccountId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "script_grants");

            migrationBuilder.DropTable(
                name: "user_scripts");
        }
    }
}
