using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CulinaryBlog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeImagesAndFileDeletionQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeletionJobId",
                table: "StoredFiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionRequestedAt",
                table: "StoredFiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RecipeImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoredFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MediumUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AltText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeImages_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecipeImages_StoredFiles_StoredFileId",
                        column: x => x.StoredFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_DeletionRequestedAt",
                table: "StoredFiles",
                column: "DeletionRequestedAt",
                filter: "\"DeletedAt\" IS NULL AND \"DeletionRequestedAt\" IS NOT NULL AND \"DeletionJobId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeImages_RecipeId",
                table: "RecipeImages",
                column: "RecipeId",
                unique: true,
                filter: "\"IsPrimary\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeImages_RecipeId_OrderIndex",
                table: "RecipeImages",
                columns: new[] { "RecipeId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeImages_StoredFileId",
                table: "RecipeImages",
                column: "StoredFileId",
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO "RecipeImages" ("Id", "RecipeId", "OriginalUrl", "IsPrimary", "OrderIndex")
                SELECT gen_random_uuid(), "Id", "ImageUrl", TRUE, 0
                FROM "Recipes"
                WHERE "ImageUrl" IS NOT NULL AND btrim("ImageUrl") <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeImages");

            migrationBuilder.DropIndex(
                name: "IX_StoredFiles_DeletionRequestedAt",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "DeletionJobId",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "DeletionRequestedAt",
                table: "StoredFiles");
        }
    }
}
