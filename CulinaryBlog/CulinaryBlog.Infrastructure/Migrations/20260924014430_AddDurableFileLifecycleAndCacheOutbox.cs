using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CulinaryBlog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDurableFileLifecycleAndCacheOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BucketName",
                table: "StoredFiles",
                type: "character varying(63)",
                maxLength: 63,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObjectKey",
                table: "StoredFiles",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "StoredFiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UploadExpiresAt",
                table: "StoredFiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RecipeCacheInvalidations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeCacheInvalidations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_BucketName_ObjectKey",
                table: "StoredFiles",
                columns: new[] { "BucketName", "ObjectKey" },
                unique: true,
                filter: "\"BucketName\" IS NOT NULL AND \"ObjectKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_UploadExpiresAt",
                table: "StoredFiles",
                column: "UploadExpiresAt",
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeCacheInvalidations_CreatedAt",
                table: "RecipeCacheInvalidations",
                column: "CreatedAt",
                filter: "\"ProcessedAt\" IS NULL");

            migrationBuilder.Sql("""
                UPDATE "StoredFiles"
                SET "Status" = CASE WHEN "DeletedAt" IS NOT NULL THEN 3
                    WHEN "DeletionRequestedAt" IS NOT NULL THEN 2 ELSE 1 END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeCacheInvalidations");

            migrationBuilder.DropIndex(
                name: "IX_StoredFiles_BucketName_ObjectKey",
                table: "StoredFiles");

            migrationBuilder.DropIndex(
                name: "IX_StoredFiles_UploadExpiresAt",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "BucketName",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "ObjectKey",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "UploadExpiresAt",
                table: "StoredFiles");
        }
    }
}
