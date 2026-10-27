using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CulinaryBlog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeImageResizeDispatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResizeJobId",
                table: "RecipeImages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeImages_CreatedAt",
                table: "RecipeImages",
                column: "CreatedAt",
                filter: "\"ResizeJobId\" IS NULL AND \"StoredFileId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RecipeImages_CreatedAt",
                table: "RecipeImages");

            migrationBuilder.DropColumn(
                name: "ResizeJobId",
                table: "RecipeImages");
        }
    }
}