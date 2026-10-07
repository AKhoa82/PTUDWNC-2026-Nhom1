using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Npgsql;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public class RecipeStartupPostgresTests
{
    [PostgresFact]
    public async Task Fresh_database_starts_api_and_assigns_author_to_seed_recipes()
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
                Assert.True(ready, "API did not start successfully on a fresh database.");

                await using var db = new NpgsqlConnection(databaseConnection);
                await db.OpenAsync();
                await using var verify = new NpgsqlCommand("""
                    SELECT count(*)
                    FROM "Recipes" r
                    JOIN "AspNetUsers" u ON u."Id" = r."AuthorId"
                    JOIN "AspNetUserRoles" ur ON ur."UserId" = u."Id"
                    JOIN "AspNetRoles" role ON role."Id" = ur."RoleId"
                    WHERE u."Username" = 'sample-recipe-author' AND role."Name" = 'Author'
                    """, db);
                Assert.Equal(6L, (long)(await verify.ExecuteScalarAsync())!);
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
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
