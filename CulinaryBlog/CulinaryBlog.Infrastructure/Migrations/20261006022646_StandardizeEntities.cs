using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CulinaryBlog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StandardizeEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "RecipeSteps",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "RecipeSteps",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "RecipeIngredients",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "RecipeIngredients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "RecipeImages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "RecipeImages",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "RecipeSteps");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "RecipeSteps");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "RecipeIngredients");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "RecipeIngredients");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "RecipeImages");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "RecipeImages");
        }
    }
}
