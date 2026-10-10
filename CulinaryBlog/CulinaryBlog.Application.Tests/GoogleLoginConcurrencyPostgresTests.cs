using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Contracts.Security;
using CulinaryBlog.Application.Features.Auth.GoogleLogin;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public class GoogleLoginConcurrencyPostgresTests
{
    [PostgresFact]
    public async Task Simultaneous_first_logins_share_one_author_and_each_get_a_refresh_token()
    {
        await WithDatabaseAsync(async provider =>
        {
            var bothVerified = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var verificationCount = 0;
            var verifier = new DelegateVerifier(async cancellationToken =>
            {
                if (Interlocked.Increment(ref verificationCount) == 2)
                    bothVerified.TrySetResult();
                await bothVerified.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
                return new GoogleIdTokenProfile("same-google-sub", "chef@example.com", true, "Chef", null);
            });

            async Task<CulinaryBlog.Application.DTOs.AuthResponseDto> LoginAsync()
            {
                using var scope = provider.CreateScope();
                return await NewHandler(scope.ServiceProvider, verifier, new TestJwtService())
                    .Handle(new GoogleLoginCommand("valid-token"), default);
            }

            var responses = await Task.WhenAll(LoginAsync(), LoginAsync())
                .WaitAsync(TimeSpan.FromSeconds(20));

            Assert.Equal(responses[0].Id, responses[1].Id);
            Assert.NotEqual(responses[0].RefreshToken, responses[1].RefreshToken);
            Assert.Equal(2, verificationCount);

            using var checkScope = provider.CreateScope();
            var context = checkScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(1, await context.Users.CountAsync(user => user.Email == "chef@example.com"));
            Assert.Equal(1, await context.UserLogins.CountAsync(login =>
                login.LoginProvider == "Google" && login.ProviderKey == "same-google-sub"));
            Assert.Equal(2, await context.RefreshTokens.CountAsync(token => token.UserId == responses[0].Id));
            var userManager = checkScope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = await userManager.FindByIdAsync(responses[0].Id.ToString());
            Assert.NotNull(user);
            Assert.True(await userManager.IsInRoleAsync(user, "Author"));
        });
    }

    [PostgresFact]
    public async Task Failure_after_creating_author_rolls_back_user_role_and_google_link()
    {
        await WithDatabaseAsync(async provider =>
        {
            using (var scope = provider.CreateScope())
            {
                var verifier = new DelegateVerifier(_ => Task.FromResult(
                    new GoogleIdTokenProfile("new-google-sub", "new@example.com", true, "New", null)));
                var handler = NewHandler(scope.ServiceProvider, verifier, new TestJwtService(failOnRefresh: true));
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    handler.Handle(new GoogleLoginCommand("valid-token"), default));
            }

            using var checkScope = provider.CreateScope();
            var context = checkScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(0, await context.Users.CountAsync());
            Assert.Equal(0, await context.Roles.CountAsync());
            Assert.Equal(0, await context.UserLogins.CountAsync());
            Assert.Equal(0, await context.RefreshTokens.CountAsync());
        });
    }

    [PostgresFact]
    public async Task Failure_while_linking_existing_email_keeps_existing_account_unlinked()
    {
        await WithDatabaseAsync(async provider =>
        {
            Guid existingId;
            using (var setupScope = provider.CreateScope())
            {
                var userManager = setupScope.ServiceProvider.GetRequiredService<UserManager<User>>();
                var user = new User { UserName = "existing", Email = "existing@example.com", FullName = "Existing" };
                Assert.True((await userManager.CreateAsync(user, "Valid1!Pass")).Succeeded);
                existingId = user.Id;
            }

            using (var scope = provider.CreateScope())
            {
                var verifier = new DelegateVerifier(_ => Task.FromResult(
                    new GoogleIdTokenProfile("link-google-sub", "existing@example.com", true, "Existing", null)));
                var handler = NewHandler(scope.ServiceProvider, verifier, new TestJwtService(failOnRefresh: true));
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    handler.Handle(new GoogleLoginCommand("valid-token"), default));
            }

            using var checkScope = provider.CreateScope();
            var context = checkScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(1, await context.Users.CountAsync(user => user.Id == existingId));
            Assert.Equal(0, await context.UserLogins.CountAsync());
            Assert.Equal(0, await context.RefreshTokens.CountAsync());
        });
    }

    private static GoogleLoginCommandHandler NewHandler(
        IServiceProvider services, IGoogleIdTokenVerifier verifier, IJwtService jwt) => new(
        services.GetRequiredService<UserManager<User>>(),
        services.GetRequiredService<RoleManager<IdentityRole<Guid>>>(),
        services.GetRequiredService<IApplicationDbContext>(),
        jwt,
        verifier,
        services.GetRequiredService<IGoogleLoginTransactionFactory>(),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Google:ClientId"] = "test-client"
        }).Build(),
        NullLogger<GoogleLoginCommandHandler>.Instance);

    private static async Task WithDatabaseAsync(Func<IServiceProvider, Task> test)
    {
        var adminConnection = Environment.GetEnvironmentVariable("SEARCH_TEST_POSTGRES")!;
        var database = "google_concurrency_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin))
            await create.ExecuteNonQueryAsync();

        try
        {
            var connection = new NpgsqlConnectionStringBuilder(adminConnection)
            {
                Database = database,
                Pooling = false
            }.ConnectionString;
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connection));
            services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
            services.AddScoped<IGoogleLoginTransactionFactory, GoogleLoginTransactionFactory>();
            services.AddIdentityCore<User>(options => options.User.RequireUniqueEmail = true)
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            using var provider = services.BuildServiceProvider();
            using (var setupScope = provider.CreateScope())
                await setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
            await test(provider);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class DelegateVerifier(Func<CancellationToken, Task<GoogleIdTokenProfile>> verify) : IGoogleIdTokenVerifier
    {
        public Task<GoogleIdTokenProfile> VerifyAsync(string idToken, string clientId, CancellationToken cancellationToken)
        {
            Assert.Equal("test-client", clientId);
            return verify(cancellationToken);
        }
    }

    private sealed class TestJwtService(bool failOnRefresh = false) : IJwtService
    {
        public string GenerateAccessToken(User user, IEnumerable<string>? roles = null) => "access-token";

        public string GenerateRefreshToken() => failOnRefresh
            ? throw new InvalidOperationException("Injected failure after Google account linking.")
            : Guid.NewGuid().ToString("N");
    }
}
