using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RatBot.Infrastructure.Data;

#nullable disable

namespace RatBot.Infrastructure.Migrations;

/// <inheritdoc />
[DbContext(typeof(BotDbContext))]
[Migration("20260404000100_InitialPostgres")]
public partial class InitialPostgres : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EmojiUsageCounts",
            columns: table => new
            {
                EmojiId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                UsageCount = table.Column<int>(type: "integer", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmojiUsageCounts", x => x.EmojiId);
            }
        );

        migrationBuilder.CreateTable(
            name: "GuildConfigs",
            columns: table => new
            {
                GuildId = table.Column<ulong>(type: "numeric(20,0)", nullable: false),
                Prefix = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GuildConfigs", x => x.GuildId);
            }
        );

        migrationBuilder.CreateTable(
            name: "QuorumScopeConfigs",
            columns: table => new
            {
                GuildId = table.Column<ulong>(type: "numeric(20,0)", nullable: false),
                ScopeType = table.Column<int>(type: "integer", nullable: false),
                ScopeId = table.Column<ulong>(type: "numeric(20,0)", nullable: false),
                RoleIds = table.Column<string>(type: "jsonb", nullable: false),
                QuorumProportion = table.Column<double>(type: "double precision", precision: 6, scale: 4, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_QuorumScopeConfigs", x => new { x.GuildId, x.ScopeType, x.ScopeId });
            }
        );

        migrationBuilder.CreateTable(
            name: "UserScores",
            columns: table => new
            {
                UserId = table.Column<ulong>(type: "numeric(20,0)", nullable: false),
                Score = table.Column<int>(type: "integer", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserScores", x => x.UserId);
            }
        );

        migrationBuilder.CreateIndex(name: "IX_QuorumScopeConfigs_GuildId", table: "QuorumScopeConfigs", column: "GuildId");

        migrationBuilder.CreateIndex(
            name: "IX_QuorumScopeConfigs_GuildId_ScopeType",
            table: "QuorumScopeConfigs",
            columns: new[] { "GuildId", "ScopeType" }
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "EmojiUsageCounts");
        migrationBuilder.DropTable(name: "GuildConfigs");
        migrationBuilder.DropTable(name: "QuorumScopeConfigs");
        migrationBuilder.DropTable(name: "UserScores");
    }
}
