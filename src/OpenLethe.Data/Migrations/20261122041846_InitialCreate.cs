using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenLethe.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    IngameId = table.Column<int>(type: "INTEGER", nullable: false),
                    DiscordId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Formations = table.Column<string>(type: "TEXT", nullable: false),
                    Egos = table.Column<string>(type: "TEXT", nullable: false),
                    Personalities = table.Column<string>(type: "TEXT", nullable: false),
                    Announcers = table.Column<string>(type: "TEXT", nullable: false),
                    UserInfo = table.Column<string>(type: "TEXT", nullable: false),
                    CustomTheme = table.Column<string>(type: "TEXT", nullable: false),
                    CustomIdentities = table.Column<string>(type: "TEXT", nullable: false),
                    CustomEgos = table.Column<string>(type: "TEXT", nullable: false),
                    CustomEgoGifts = table.Column<string>(type: "TEXT", nullable: false),
                    CustomEgoGiftDropPools = table.Column<string>(type: "TEXT", nullable: false),
                    CustomMdBattles = table.Column<string>(type: "TEXT", nullable: false),
                    CustomMdCommonData = table.Column<string>(type: "TEXT", nullable: false),
                    CustomMdRevision = table.Column<long>(type: "INTEGER", nullable: false),
                    MdSaveInfo = table.Column<string>(type: "TEXT", nullable: false),
                    StorySaveInfo = table.Column<string>(type: "TEXT", nullable: false),
                    StoryMdSaveInfo = table.Column<string>(type: "TEXT", nullable: false),
                    RailwaySaveInfo = table.Column<string>(type: "TEXT", nullable: false),
                    ChapterState = table.Column<string>(type: "TEXT", nullable: false),
                    Chapter10RpgSaveInfo = table.Column<string>(type: "TEXT", nullable: false),
                    BossRaidSaveInfo = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "counters",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_counters", x => x.Key);
                });

            // Seed the IngameId allocator so all concurrent signups update one row
            // instead of racing to insert it.
            migrationBuilder.InsertData(
                table: "counters",
                columns: new[] { "Key", "Value" },
                values: new object[] { "IngameId", 0L });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_DiscordId",
                table: "accounts",
                column: "DiscordId",
                unique: true,
                filter: "\"DiscordId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_IngameId",
                table: "accounts",
                column: "IngameId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_Username",
                table: "accounts",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropTable(
                name: "counters");
        }
    }
}
