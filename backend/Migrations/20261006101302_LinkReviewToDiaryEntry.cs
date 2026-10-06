using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class LinkReviewToDiaryEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiaryEntryId",
                table: "Reviews",
                type: "int",
                nullable: true);

            // Existing reviews were saved with the user's latest log of the film, so link them to that entry.
            // Reviews of films without diary entries stay unlinked.
            migrationBuilder.Sql("""
                UPDATE r
                SET r.DiaryEntryId = (
                    SELECT TOP 1 d.Id
                    FROM DiaryEntries d
                    WHERE d.UserId = r.UserId AND d.MovieId = r.MovieId
                    ORDER BY d.WatchedAt DESC, d.Id DESC)
                FROM Reviews r;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_DiaryEntryId",
                table: "Reviews",
                column: "DiaryEntryId",
                unique: true,
                filter: "[DiaryEntryId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_DiaryEntries_DiaryEntryId",
                table: "Reviews",
                column: "DiaryEntryId",
                principalTable: "DiaryEntries",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_DiaryEntries_DiaryEntryId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_DiaryEntryId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "DiaryEntryId",
                table: "Reviews");
        }
    }
}
