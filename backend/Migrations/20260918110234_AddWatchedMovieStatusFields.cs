using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddWatchedMovieStatusFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Liked",
                table: "WatchedMovies",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Rewatched",
                table: "WatchedMovies",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Liked",
                table: "WatchedMovies");

            migrationBuilder.DropColumn(
                name: "Rewatched",
                table: "WatchedMovies");
        }
    }
}
