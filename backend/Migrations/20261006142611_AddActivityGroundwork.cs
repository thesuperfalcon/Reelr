using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityGroundwork : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MovieLists_UserId",
                table: "MovieLists");

            migrationBuilder.AddColumn<DateTime>(
                name: "LoggedAt",
                table: "DiaryEntries",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Before this column existed the watch date was always the moment of logging,
            // except for back-dated entries, where the watch date is the best guess there is.
            migrationBuilder.Sql("UPDATE DiaryEntries SET LoggedAt = WatchedAt;");

            // Existing users get the same default as new ones: WatchlistVisibility.Followers.
            // The model sets no database default on purpose: with one, EF would skip saving Public (0)
            // because it equals the enum's CLR default, and the database would store Followers instead.
            migrationBuilder.AddColumn<int>(
                name: "WatchlistVisibility",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistItems_UserId_AddedAt",
                table: "WatchlistItems",
                columns: new[] { "UserId", "AddedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_CreatedAt",
                table: "Reviews",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_UserId_CreatedAt",
                table: "Reviews",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MovieLists_IsPublic_CreatedAt",
                table: "MovieLists",
                columns: new[] { "IsPublic", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MovieLists_UserId_CreatedAt",
                table: "MovieLists",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MovieListItems_AddedAt",
                table: "MovieListItems",
                column: "AddedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntries_LoggedAt",
                table: "DiaryEntries",
                column: "LoggedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntries_UserId_LoggedAt",
                table: "DiaryEntries",
                columns: new[] { "UserId", "LoggedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WatchlistItems_UserId_AddedAt",
                table: "WatchlistItems");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_CreatedAt",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_UserId_CreatedAt",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_MovieLists_IsPublic_CreatedAt",
                table: "MovieLists");

            migrationBuilder.DropIndex(
                name: "IX_MovieLists_UserId_CreatedAt",
                table: "MovieLists");

            migrationBuilder.DropIndex(
                name: "IX_MovieListItems_AddedAt",
                table: "MovieListItems");

            migrationBuilder.DropIndex(
                name: "IX_DiaryEntries_LoggedAt",
                table: "DiaryEntries");

            migrationBuilder.DropIndex(
                name: "IX_DiaryEntries_UserId_LoggedAt",
                table: "DiaryEntries");

            migrationBuilder.DropColumn(
                name: "LoggedAt",
                table: "DiaryEntries");

            migrationBuilder.DropColumn(
                name: "WatchlistVisibility",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "IX_MovieLists_UserId",
                table: "MovieLists",
                column: "UserId");
        }
    }
}
