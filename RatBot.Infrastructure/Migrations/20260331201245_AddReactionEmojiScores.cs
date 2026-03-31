using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RatBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReactionEmojiScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReactionEmojiScores",
                columns: table => new
                {
                    EmojiId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReactionEmojiScores", x => x.EmojiId);
                })
                .Annotation("MySQL:Charset", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReactionEmojiScores");
        }
    }
}
