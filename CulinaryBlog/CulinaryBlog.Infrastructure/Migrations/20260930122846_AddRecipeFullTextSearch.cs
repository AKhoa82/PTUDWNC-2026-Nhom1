using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace CulinaryBlog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeFullTextSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:unaccent", ",,");

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Recipes",
                type: "tsvector",
                nullable: true);

            migrationBuilder.Sql("""
                CREATE TEXT SEARCH CONFIGURATION public.vietnamese (COPY = pg_catalog.simple);
                ALTER TEXT SEARCH CONFIGURATION public.vietnamese
                    ALTER MAPPING FOR hword, hword_part, word WITH public.unaccent, pg_catalog.simple;

                CREATE FUNCTION public.update_recipe_search_vector() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    NEW."SearchVector" :=
                        setweight(to_tsvector('public.vietnamese', coalesce(NEW."Title", '')), 'A') ||
                        setweight(to_tsvector('public.vietnamese', coalesce(NEW."Description", '')), 'B');
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER recipe_search_vector_update
                    BEFORE INSERT OR UPDATE OF "Title", "Description" ON "Recipes"
                    FOR EACH ROW EXECUTE FUNCTION public.update_recipe_search_vector();

                UPDATE "Recipes" SET "SearchVector" =
                    setweight(to_tsvector('public.vietnamese', coalesce("Title", '')), 'A') ||
                    setweight(to_tsvector('public.vietnamese', coalesce("Description", '')), 'B');
                """);

            migrationBuilder.CreateIndex(
                name: "IDX_Recipe_Search",
                table: "Recipes",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER recipe_search_vector_update ON "Recipes";
                DROP FUNCTION public.update_recipe_search_vector();
                DROP TEXT SEARCH CONFIGURATION public.vietnamese;
                """);
            migrationBuilder.DropIndex(
                name: "IDX_Recipe_Search",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "Recipes");

            // Extensions may be shared with other features; leave them installed on rollback.
        }
    }
}
