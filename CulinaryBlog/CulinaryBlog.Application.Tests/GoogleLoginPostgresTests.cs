using System.Security.Cryptography;
using System.Text;
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

public class GoogleLoginPostgresTests
{
    [PostgresFact]
    public async Task Google_login_creates_author_links_provider_and_hashes_refresh_token()
    {
        var adminConnection = Environment.GetEnvironmentVariable("SEARCH_TEST_POSTGRES")!;
        var database = "google_login_test_" + Guid.NewGuid().ToString("N");
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
            services.AddIdentityCore<User>(options => options.User.RequireUniqueEmail = true)
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.MigrateAsync();

            var tokenVerifier = new TestGoogleIdTokenVerifier();
            var jwt = new TestJwtService();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var handler = new GoogleLoginCommandHandler(
                userManager, roleManager,
                context, jwt, tokenVerifier, new GoogleLoginTransactionFactory(context),
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Google:ClientId"] = "test-client"
                }).Build(),
                NullLogger<GoogleLoginCommandHandler>.Instance);

            var first = await handler.Handle(new GoogleLoginCommand("valid-token"), default);
            var second = await handler.Handle(new GoogleLoginCommand("valid-token"), default);

            Assert.Equal(first.Id, second.Id);
            Assert.NotEqual(first.RefreshToken, second.RefreshToken);
            Assert.Equal("Author", jwt.LastRoles);
            Assert.Equal(1, await context.Users.CountAsync(user => user.Email == "chef@gmail.com"));
            Assert.Equal(1, await context.UserLogins.CountAsync(login =>
                login.LoginProvider == "Google" && login.ProviderKey == "google-sub-1"));
            Assert.Equal(2, await context.RefreshTokens.CountAsync(token => token.UserId == first.Id));
            var expectedHashes = new[] { first.RefreshToken, second.RefreshToken }
                .Select(token => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)))
                    .ToLowerInvariant())
                .OrderBy(hash => hash).ToArray();
            var savedHashes = (await context.RefreshTokens.Select(token => token.TokenHash).ToListAsync())
                .OrderBy(hash => hash).ToArray();
            Assert.Equal(expectedHashes, savedHashes);
            Assert.Equal("test-client", tokenVerifier.LastClientId);

            foreach (var invalidToken in new[] { "invalid-signature", "expired", "wrong-audience", "wrong-issuer" })
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                    handler.Handle(new GoogleLoginCommand(invalidToken), default));

            tokenVerifier.Profile = tokenVerifier.Profile with { EmailVerified = false };
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                handler.Handle(new GoogleLoginCommand("unverified-email"), default));
            tokenVerifier.Profile = tokenVerifier.Profile with { EmailVerified = true, Email = null };
            await Assert.ThrowsAsync<GoogleProfileException>(() =>
                handler.Handle(new GoogleLoginCommand("missing-email"), default));
            tokenVerifier.Profile = tokenVerifier.Profile with { Email = "chef@gmail.com", Subject = null };
            await Assert.ThrowsAsync<GoogleProfileException>(() =>
                handler.Handle(new GoogleLoginCommand("missing-subject"), default));
            tokenVerifier.Profile = tokenVerifier.Profile with { Subject = "google-sub-1" };
            await Assert.ThrowsAsync<HttpRequestException>(() =>
                handler.Handle(new GoogleLoginCommand("certificate-service-down"), default));
            Assert.Equal(2, await context.RefreshTokens.CountAsync());

            Assert.True((await roleManager.CreateAsync(new IdentityRole<Guid>("Admin"))).Succeeded);
            var existing = new User { UserName = "existing", Email = "existing@gmail.com", FullName = "Existing" };
            Assert.True((await userManager.CreateAsync(existing, "Valid1!Pass")).Succeeded);
            Assert.True((await userManager.AddToRoleAsync(existing, "Admin")).Succeeded);
            tokenVerifier.Profile = tokenVerifier.Profile with { Email = existing.Email, Subject = "google-sub-2" };

            var linked = await handler.Handle(new GoogleLoginCommand("valid-existing-token"), default);
            Assert.Equal(existing.Id, linked.Id);
            Assert.Equal("Admin", jwt.LastRoles);
            Assert.Equal(1, await context.Users.CountAsync(user => user.Email == existing.Email));
            Assert.Equal(1, await context.UserLogins.CountAsync(login =>
                login.UserId == existing.Id && login.ProviderKey == "google-sub-2"));
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class TestJwtService : IJwtService
    {
        private int _refreshCount;
        public string LastRoles { get; private set; } = string.Empty;

        public string GenerateAccessToken(User user, IEnumerable<string>? roles = null)
        {
            LastRoles = string.Join(",", roles ?? []);
            return "test-access-token";
        }

        public string GenerateRefreshToken() => $"test-refresh-token-{Interlocked.Increment(ref _refreshCount)}";
    }

    private sealed class TestGoogleIdTokenVerifier : IGoogleIdTokenVerifier
    {
        public string? LastClientId { get; private set; }
        public GoogleIdTokenProfile Profile { get; set; } =
            new("google-sub-1", "chef@gmail.com", true, "Chef", "https://example.com/avatar.png");

        public Task<GoogleIdTokenProfile> VerifyAsync(
            string idToken, string clientId, CancellationToken cancellationToken)
        {
            LastClientId = clientId;
            if (idToken is "invalid-signature" or "expired" or "wrong-audience" or "wrong-issuer")
                throw new UnauthorizedAccessException("Google ID token is invalid or expired.");
            if (idToken == "certificate-service-down")
                throw new HttpRequestException("Certificate endpoint unavailable.");
            return Task.FromResult(Profile);
        }
    }
}
