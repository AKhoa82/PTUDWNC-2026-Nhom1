using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Npgsql;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public class RecipeStartupPostgresTests
{
    [PostgresFact]
    public async Task Startup_seeds_recipes_repairs_content_and_does_not_reseed_soft_deleted_recipes()
    {
        var database = "recipe_startup_test_" + Guid.NewGuid().ToString("N");
        var adminConnection = Environment.GetEnvironmentVariable("SEARCH_TEST_POSTGRES")!;
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin))
            await create.ExecuteNonQueryAsync();

        try
        {
            var databaseConnection = new NpgsqlConnectionStringBuilder(adminConnection)
            {
                Database = database,
                Pooling = false
            }.ConnectionString;
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            var apiProject = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../CulinaryBlog.API"));
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = apiProject,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            start.ArgumentList.Add("run");
            start.ArgumentList.Add("--no-build");
            start.ArgumentList.Add("--no-launch-profile");
            start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            start.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
            start.Environment["ConnectionStrings__DefaultConnection"] = databaseConnection;
            start.Environment["Hangfire__DashboardEnabled"] = "false";
            start.Environment["Logging__LogLevel__Microsoft.EntityFrameworkCore"] = "Error";
            start.Environment["Logging__LogLevel__Microsoft.AspNetCore.DataProtection"] = "None";

            async Task StartAndVerifyAsync(long expectedDeletedRecipes = 0)
            {
                using var api = Process.Start(start)!;
                var output = api.StandardOutput.ReadToEndAsync();
                var errors = api.StandardError.ReadToEndAsync();
                try
                {
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                    var ready = false;
                    for (var attempt = 0; attempt < 120 && !api.HasExited; attempt++)
                    {
                        try
                        {
                            using var response = await client.GetAsync($"http://127.0.0.1:{port}/");
                            if (response.StatusCode == HttpStatusCode.NotFound) { ready = true; break; }
                        }
                        catch (HttpRequestException) { }
                        catch (TaskCanceledException) { }
                        await Task.Delay(500);
                    }
                    Assert.True(ready, "API did not start successfully with sample recipes.");

                    await using var db = new NpgsqlConnection(databaseConnection);
                    await db.OpenAsync();
                    await using var verifyRecipeCounts = new NpgsqlCommand("""
                        SELECT count(*), count(*) FILTER (WHERE "IsDeleted") FROM "Recipes"
                        """, db);
                    await using (var recipeCounts = await verifyRecipeCounts.ExecuteReaderAsync())
                    {
                        Assert.True(await recipeCounts.ReadAsync());
                        Assert.Equal(6L, recipeCounts.GetInt64(0));
                        Assert.Equal(expectedDeletedRecipes, recipeCounts.GetInt64(1));
                    }

                    await using var verify = new NpgsqlCommand("""
                        SELECT count(*)
                        FROM "Recipes" r
                        JOIN "AspNetUsers" u ON u."Id" = r."AuthorId"
                        JOIN "AspNetUserRoles" ur ON ur."UserId" = u."Id"
                        JOIN "AspNetRoles" role ON role."Id" = ur."RoleId"
                        WHERE u."Username" = 'sample-recipe-author' AND role."Name" = 'Author'
                          AND r."Status" = 1
                          AND EXISTS (SELECT 1 FROM "RecipeIngredients" i WHERE i."RecipeId" = r."Id")
                          AND EXISTS (SELECT 1 FROM "RecipeSteps" s WHERE s."RecipeId" = r."Id")
                        """, db);
                    Assert.Equal(6L, (long)(await verify.ExecuteScalarAsync())!);

                    await using var verifyPho = new NpgsqlCommand("""
                        SELECT
                            (SELECT count(*) FROM "RecipeIngredients" i JOIN "Recipes" r ON r."Id" = i."RecipeId" WHERE r."Slug" = 'pho-bo-ha-noi'),
                            (SELECT count(*) FROM "RecipeSteps" s JOIN "Recipes" r ON r."Id" = s."RecipeId" WHERE r."Slug" = 'pho-bo-ha-noi')
                        """, db);
                    await using var counts = await verifyPho.ExecuteReaderAsync();
                    Assert.True(await counts.ReadAsync());
                    Assert.Equal(3L, counts.GetInt64(0));
                    Assert.Equal(2L, counts.GetInt64(1));
                }
                catch
                {
                    if (!api.HasExited) api.Kill(entireProcessTree: true);
                    await api.WaitForExitAsync();
                    Console.WriteLine((await output) + (await errors));
                    throw;
                }
                finally
                {
                    if (!api.HasExited) api.Kill(entireProcessTree: true);
                    await api.WaitForExitAsync();
                    await output;
                    await errors;
                }
            }

            await StartAndVerifyAsync();

            await using var existingDb = new NpgsqlConnection(databaseConnection);
            await existingDb.OpenAsync();
            await using (var deleteIngredients = new NpgsqlCommand("""
                DELETE FROM "RecipeIngredients" WHERE "RecipeId" =
                    (SELECT "Id" FROM "Recipes" WHERE "Slug" = 'pho-bo-ha-noi')
                """, existingDb))
                Assert.Equal(3, await deleteIngredients.ExecuteNonQueryAsync());
            await using (var deleteSteps = new NpgsqlCommand("""
                DELETE FROM "RecipeSteps" WHERE "RecipeId" =
                    (SELECT "Id" FROM "Recipes" WHERE "Slug" = 'pho-bo-ha-noi')
                """, existingDb))
                Assert.Equal(2, await deleteSteps.ExecuteNonQueryAsync());

            await StartAndVerifyAsync();

            await using (var softDeleteRecipes = new NpgsqlCommand("""
                UPDATE "Recipes" SET "IsDeleted" = true WHERE NOT "IsDeleted"
                """, existingDb))
                Assert.Equal(6, await softDeleteRecipes.ExecuteNonQueryAsync());

            await StartAndVerifyAsync(expectedDeletedRecipes: 6);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
