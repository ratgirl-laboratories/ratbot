using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RatBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVirtueReactionLocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VirtueReactionLocks",
                columns: table => new
                {
                    MessageId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    ReactorUserId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    TargetUserId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    EmojiId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    VirtueDelta = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtueReactionLocks", x => new { x.MessageId, x.ReactorUserId });
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_VirtueReactionLocks_TargetUserId",
                table: "VirtueReactionLocks",
                column: "TargetUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VirtueReactionLocks");
        }
    }
}
