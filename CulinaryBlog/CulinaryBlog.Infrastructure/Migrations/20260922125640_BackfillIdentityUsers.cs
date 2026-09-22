using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CulinaryBlog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillIdentityUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "AspNetUsers"
                SET "NormalizedEmail" = UPPER("Email"),
                    "NormalizedUserName" = UPPER("Username"),
                    "LockoutEnabled" = TRUE,
                    "SecurityStamp" = COALESCE("SecurityStamp", md5(random()::text || clock_timestamp()::text))
                WHERE "NormalizedEmail" IS NULL
                   OR "NormalizedUserName" IS NULL
                   OR "LockoutEnabled" = FALSE
                   OR "SecurityStamp" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "AspNetUsers"
                SET "NormalizedEmail" = NULL,
                    "NormalizedUserName" = NULL,
                    "LockoutEnabled" = FALSE;
                """);
        }
    }
}
