using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddDiaryEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiaryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    MovieId = table.Column<int>(type: "int", nullable: false),
                    WatchedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Rating = table.Column<decimal>(type: "decimal(2,1)", precision: 2, scale: 1, nullable: true),
                    Liked = table.Column<bool>(type: "bit", nullable: true),
                    Rewatched = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiaryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiaryEntries_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DiaryEntries_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntries_MovieId",
                table: "DiaryEntries",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntries_UserId_WatchedAt",
                table: "DiaryEntries",
                columns: new[] { "UserId", "WatchedAt" });

            // Each existing watched film becomes the first entry of the new diary.
            migrationBuilder.Sql(@"
INSERT INTO DiaryEntries (UserId, MovieId, WatchedAt, Rating, Liked, Rewatched)
SELECT w.UserId, w.MovieId, w.WatchedAt, r.Score, w.Liked, w.Rewatched
FROM WatchedMovies w
LEFT JOIN Ratings r ON r.UserId = w.UserId AND r.MovieId = w.MovieId;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiaryEntries");
        }
    }
}
