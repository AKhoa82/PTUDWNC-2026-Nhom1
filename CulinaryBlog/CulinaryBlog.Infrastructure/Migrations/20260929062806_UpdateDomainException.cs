using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CulinaryBlog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDomainException : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Nutrition_Calories",
                table: "Recipes",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Nutrition_Carbs",
                table: "Recipes",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Nutrition_Fat",
                table: "Recipes",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Nutrition_Fiber",
                table: "Recipes",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Nutrition_Protein",
                table: "Recipes",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Nutrition_Sodium",
                table: "Recipes",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Recipes",
                type: "bytea",
                rowVersion: true,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Nutrition_Calories",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Nutrition_Carbs",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Nutrition_Fat",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Nutrition_Fiber",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Nutrition_Protein",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Nutrition_Sodium",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Recipes");
        }
    }
}
