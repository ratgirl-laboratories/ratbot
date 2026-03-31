using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RatBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmojiUsageCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmojiUsageCounts",
                columns: table => new
                {
                    EmojiId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    UsageCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmojiUsageCounts", x => x.EmojiId);
                })
                .Annotation("MySQL:Charset", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmojiUsageCounts");
        }
    }
}
