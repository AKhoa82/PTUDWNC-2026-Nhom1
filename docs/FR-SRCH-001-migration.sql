START TRANSACTION;
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE EXTENSION IF NOT EXISTS unaccent;

ALTER TABLE "Recipes" ADD "SearchVector" tsvector;

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

CREATE INDEX "IDX_Recipe_Search" ON "Recipes" USING GIN ("SearchVector");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260930122846_AddRecipeFullTextSearch', '10.0.12');

COMMIT;
