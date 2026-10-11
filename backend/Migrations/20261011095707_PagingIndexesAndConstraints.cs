using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class PagingIndexesAndConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_CreatedAt",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_MovieId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_DiaryEntries_LoggedAt",
                table: "DiaryEntries");

            migrationBuilder.AddColumn<DateTime>(
                name: "ActivityAt",
                table: "Reviews",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // A review shows up in the feed when the diary entry that wrote it was logged, otherwise when it was written.
            migrationBuilder.Sql("""
                UPDATE r SET ActivityAt = COALESCE(d.LoggedAt, r.CreatedAt)
                FROM Reviews r LEFT JOIN DiaryEntries d ON d.Id = r.DiaryEntryId;
                """);

            // Rows from before the new limits are brought inside them, so the columns can shrink and the
            // checks can be added. Text is cut; an image path that does not fit is dropped, since a cut one
            // would point at the wrong image. Scores outside the star range are removed and others rounded
            // to the nearest half star.
            migrationBuilder.Sql("""
                UPDATE Movies SET Title = LEFT(Title, 500) WHERE LEN(Title) > 500;
                UPDATE Movies SET Description = LEFT(Description, 4000) WHERE LEN(Description) > 4000;
                UPDATE Movies SET PosterUrl = NULL WHERE LEN(PosterUrl) > 300;
                UPDATE Movies SET BackdropUrl = NULL WHERE LEN(BackdropUrl) > 300;
                UPDATE MovieLists SET Name = LEFT(Name, 100) WHERE LEN(Name) > 100;
                UPDATE MovieLists SET Description = LEFT(Description, 1000) WHERE LEN(Description) > 1000;
                UPDATE AspNetUsers SET ProfileImageUrl = NULL WHERE LEN(ProfileImageUrl) > 200;
                UPDATE Reviews SET Text = LEFT(Text, 5000) WHERE LEN(Text) > 5000;

                DELETE FROM Ratings WHERE Score < 0.5 OR Score > 5;
                UPDATE Ratings SET Score = ROUND(Score * 2, 0) / 2 WHERE Score * 2 <> FLOOR(Score * 2);
                UPDATE DiaryEntries SET Rating = NULL WHERE Rating < 0.5 OR Rating > 5;
                UPDATE DiaryEntries SET Rating = ROUND(Rating * 2, 0) / 2 WHERE Rating * 2 <> FLOOR(Rating * 2);

                DELETE FROM Follows WHERE FollowerId = FollowedId;
                DELETE FROM Blocks WHERE BlockerId = BlockedId;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Movies",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "PosterUrl",
                table: "Movies",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Movies",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BackdropUrl",
                table: "Movies",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "MovieLists",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "MovieLists",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ProfileImageUrl",
                table: "AspNetUsers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBackdated",
                table: "DiaryEntries",
                type: "bit",
                nullable: false,
                computedColumnSql: "CAST(CASE WHEN [LoggedAt] > DATEADD(day, 14, [WatchedAt]) THEN 1 ELSE 0 END AS bit)",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ActivityAt",
                table: "Reviews",
                column: "ActivityAt");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_MovieId_CreatedAt",
                table: "Reviews",
                columns: new[] { "MovieId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_UserId_ActivityAt",
                table: "Reviews",
                columns: new[] { "UserId", "ActivityAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLikes_ReviewId_CreatedAt",
                table: "ReviewLikes",
                columns: new[] { "ReviewId", "CreatedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Ratings_Score",
                table: "Ratings",
                sql: "[Score] >= 0.5 AND [Score] <= 5 AND [Score] * 2 = FLOOR([Score] * 2)");

            migrationBuilder.CreateIndex(
                name: "IX_MovieListItems_MovieListId_AddedAt",
                table: "MovieListItems",
                columns: new[] { "MovieListId", "AddedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Follows_NotSelf",
                table: "Follows",
                sql: "[FollowerId] <> [FollowedId]");

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntries_IsBackdated_LoggedAt",
                table: "DiaryEntries",
                columns: new[] { "IsBackdated", "LoggedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_DiaryEntries_Rating",
                table: "DiaryEntries",
                sql: "[Rating] IS NULL OR ([Rating] >= 0.5 AND [Rating] <= 5 AND [Rating] * 2 = FLOOR([Rating] * 2))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Blocks_NotSelf",
                table: "Blocks",
                sql: "[BlockerId] <> [BlockedId]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reviews_Text",
                table: "Reviews",
                sql: "LEN([Text]) <= 5000");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_ActivityAt",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_MovieId_CreatedAt",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_UserId_ActivityAt",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_ReviewLikes_ReviewId_CreatedAt",
                table: "ReviewLikes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Ratings_Score",
                table: "Ratings");

            migrationBuilder.DropIndex(
                name: "IX_MovieListItems_MovieListId_AddedAt",
                table: "MovieListItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Follows_NotSelf",
                table: "Follows");

            migrationBuilder.DropIndex(
                name: "IX_DiaryEntries_IsBackdated_LoggedAt",
                table: "DiaryEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DiaryEntries_Rating",
                table: "DiaryEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Blocks_NotSelf",
                table: "Blocks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reviews_Text",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "IsBackdated",
                table: "DiaryEntries");

            migrationBuilder.DropColumn(
                name: "ActivityAt",
                table: "Reviews");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Movies",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "PosterUrl",
                table: "Movies",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Movies",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BackdropUrl",
                table: "Movies",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "MovieLists",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "MovieLists",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ProfileImageUrl",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_CreatedAt",
                table: "Reviews",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_MovieId",
                table: "Reviews",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntries_LoggedAt",
                table: "DiaryEntries",
                column: "LoggedAt");
        }
    }
}
