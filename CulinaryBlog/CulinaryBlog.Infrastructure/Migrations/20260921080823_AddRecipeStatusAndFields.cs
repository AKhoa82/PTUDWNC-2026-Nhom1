using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CulinaryBlog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeStatusAndFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert Difficulty từ string sang integer trước khi đổi kiểu cột
            migrationBuilder.Sql("""
                ALTER TABLE "Recipes" ADD COLUMN "Difficulty_new" integer NOT NULL DEFAULT 1;
                UPDATE "Recipes" SET "Difficulty_new" = CASE
                    WHEN "Difficulty" = 'Easy'   THEN 1
                    WHEN "Difficulty" = 'Medium'  THEN 2
                    WHEN "Difficulty" = 'Hard'    THEN 3
                    WHEN "Difficulty" = 'Expert'  THEN 4
                    ELSE 1
                END;
                ALTER TABLE "Recipes" DROP COLUMN "Difficulty";
                ALTER TABLE "Recipes" RENAME COLUMN "Difficulty_new" TO "Difficulty";
            """);

            migrationBuilder.AddColumn<string>(
                name: "AuthorId",
                table: "Recipes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrepTimeMinutes",
                table: "Recipes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "Recipes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Servings",
                table: "Recipes",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Recipes",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Recipes",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthorId",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "PrepTimeMinutes",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Servings",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Recipes");

            migrationBuilder.AlterColumn<string>(
                name: "Difficulty",
                table: "Recipes",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
