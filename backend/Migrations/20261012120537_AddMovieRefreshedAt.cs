using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddMovieRefreshedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Films stored before this get the oldest possible date, so the refresher copies them first.
            migrationBuilder.AddColumn<DateTime>(
                name: "RefreshedAt",
                table: "Movies",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_Movies_RefreshedAt",
                table: "Movies",
                column: "RefreshedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Movies_RefreshedAt",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "RefreshedAt",
                table: "Movies");
        }
    }
}
