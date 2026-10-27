using CulinaryBlog.Application.Contracts.Infrastructure;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Contracts.Security;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Auth.Register;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Configurations;
using CulinaryBlog.Infrastructure.Jobs;
using CulinaryBlog.Infrastructure.Persistence;
using Hangfire;
using Hangfire.Common;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Hosting;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using Xunit;

namespace CulinaryBlog.Application.Tests;

public sealed class WelcomeEmailPostgresTests
{
    [PostgresFact]
    public async Task Registration_is_atomic_and_enqueue_failure_preserves_successful_registration()
    {
        await WithDatabase(async services =>
        {
            var queue = Mock.Get(services.GetRequiredService<IWelcomeEmailQueue>());
            queue.Setup(x => x.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("queue unavailable"));
            using (var scope = services.CreateScope())
            {
                var response = await scope.ServiceProvider.GetRequiredService<RegisterCommandHandler>().Handle(Request("success"), default);
                Assert.Equal("access", response.AccessToken);
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var message = await db.WelcomeEmailOutboxes.SingleAsync();
                Assert.Equal(WelcomeEmailStatus.Pending, message.Status);
                Assert.Equal("success@example.com", message.Email);
                Assert.True(await db.RefreshTokens.AnyAsync(x => x.UserId == message.UserId));
                Assert.True(await scope.ServiceProvider.GetRequiredService<UserManager<User>>().IsInRoleAsync(await db.Users.SingleAsync(), "Author"));
            }
            // Fail after Identity has already saved the user and role.
            using (var scope = services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE FUNCTION reject_welcome_email() RETURNS trigger LANGUAGE plpgsql AS $$
                    BEGIN RAISE EXCEPTION 'injected outbox failure'; END $$;
                    CREATE TRIGGER reject_welcome_email BEFORE INSERT ON "WelcomeEmailOutbox"
                    FOR EACH ROW EXECUTE FUNCTION reject_welcome_email();
                    """);
                await Assert.ThrowsAsync<DbUpdateException>(() => scope.ServiceProvider.GetRequiredService<RegisterCommandHandler>().Handle(Request("rollback"), default));
            }
            using (var scope = services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.False(await db.Users.AnyAsync(x => x.UserName == "rollback"));
                Assert.Equal(1, await db.RefreshTokens.CountAsync());
                Assert.Equal(1, await db.UserRoles.CountAsync());
                Assert.Equal(1, await db.WelcomeEmailOutboxes.CountAsync());
                await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_welcome_email ON \"WelcomeEmailOutbox\"; DROP FUNCTION reject_welcome_email();");
            }
            // A JWT error must also roll back the user, role, token and outbox.
            Mock.Get(services.GetRequiredService<IJwtService>()).Setup(x => x.GenerateAccessToken(It.IsAny<User>(), It.IsAny<IEnumerable<string>>())).Throws(new InvalidOperationException("token failed"));
            using (var scope = services.CreateScope())
                await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<RegisterCommandHandler>().Handle(Request("jwtfailure"), default));
            using (var scope = services.CreateScope())
                Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.CountAsync());
            // Role-assignment failure must roll back the just-created Identity user too.
            using (var scope = services.CreateScope())
            {
                var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
                Assert.True((await roles.DeleteAsync((await roles.FindByNameAsync("Author"))!)).Succeeded);
            }
            using (var scope = services.CreateScope())
                await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<RegisterCommandHandler>().Handle(Request("missingrole"), default));
            using (var scope = services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.Equal(1, await db.Users.CountAsync());
                Assert.Equal(1, await db.RefreshTokens.CountAsync());
                Assert.Equal(1, await db.WelcomeEmailOutboxes.CountAsync());
                await Assert.ThrowsAsync<CulinaryBlog.Application.Common.Exceptions.ConflictException>(() =>
                    scope.ServiceProvider.GetRequiredService<RegisterCommandHandler>().Handle(Request("success"), default));
            }
            queue.Verify(x => x.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        });
    }

    [PostgresFact]
    public async Task Dispatch_recovers_pending_messages_and_serializes_concurrent_dispatchers()
    {
        await WithDatabase(async services =>
        {
            var id = await SeedMessage(services);
            var dispatcher = new HangfireWelcomeEmailQueue(services.GetRequiredService<IServiceScopeFactory>());
            var jobs = Mock.Get(services.GetRequiredService<IBackgroundJobClient>());
            jobs.Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>())).Throws(new IOException("unavailable"));
            await Assert.ThrowsAsync<IOException>(() => dispatcher.EnqueueAsync(id, default));
            using (var scope = services.CreateScope())
                Assert.Equal(WelcomeEmailStatus.Pending, (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().WelcomeEmailOutboxes.SingleAsync()).Status);
            jobs.Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>())).Returns("job-1");
            await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => dispatcher.EnqueueAsync(id, default)));
            jobs.Verify(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Exactly(2)); // one failed + one successful enqueue
            using var verification = services.CreateScope();
            var db = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var message = await db.WelcomeEmailOutboxes.SingleAsync();
            Assert.Equal(WelcomeEmailStatus.Queued, message.Status);
            Assert.Equal("job-1", message.HangfireJobId);
            db.WelcomeEmailOutboxes.Add(new WelcomeEmailOutbox { UserId = message.UserId, Email = message.Email, FullName = message.FullName });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    [PostgresFact]
    public async Task Duplicate_jobs_send_once_and_smtp_failure_remains_retryable()
    {
        await WithDatabase(async services =>
        {
            var id = await SeedMessage(services);
            var sender = Mock.Get(services.GetRequiredService<IEmailSender>());
            sender.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("private SMTP response"));
            using (var scope = services.CreateScope())
            {
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<WelcomeEmailJob>().ExecuteAsync(id, default));
                Assert.DoesNotContain("private", error.ToString());
            }
            sender.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(async () => await Task.Delay(100));
            async Task Execute()
            {
                using var scope = services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<WelcomeEmailJob>().ExecuteAsync(id, default);
            }
            await Task.WhenAll(Execute(), Execute(), Execute());
            sender.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            using var verification = services.CreateScope();
            var message = await verification.ServiceProvider.GetRequiredService<ApplicationDbContext>().WelcomeEmailOutboxes.SingleAsync();
            Assert.Equal(WelcomeEmailStatus.Sent, message.Status);
            Assert.NotNull(message.SentAt);
        });
    }

    [PostgresFact]
    public async Task Reconciliation_recovers_pending_and_terminal_failure_without_requeueing_failed_email()
    {
        await WithDatabase(async services =>
        {
            var id = await SeedMessage(services);
            var storage = new Mock<JobStorage>();
            var connection = new Mock<IStorageConnection>();
            storage.Setup(x => x.GetConnection()).Returns(connection.Object);
            connection.Setup(x => x.GetStateData("job-1")).Returns(new StateData { Name = "Failed" });
            var dispatcher = new HangfireWelcomeEmailQueue(services.GetRequiredService<IServiceScopeFactory>());
            using (var scope = services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var reconcile = new WelcomeEmailReconciliationJob(db, dispatcher, storage.Object, NullLogger<WelcomeEmailReconciliationJob>.Instance);
                await reconcile.ExecuteAsync(default);
                await reconcile.ExecuteAsync(default);
                Assert.Equal(WelcomeEmailStatus.Failed, (await db.WelcomeEmailOutboxes.AsNoTracking().SingleAsync()).Status);
                Mock.Get(services.GetRequiredService<IBackgroundJobClient>()).Verify(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Once);
            }
            // Manual retry is permitted even when the durable record is Failed.
            using (var scope = services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<WelcomeEmailJob>().ExecuteAsync(id, default);
            using var verify = services.CreateScope();
            Assert.Equal(WelcomeEmailStatus.Sent, (await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().WelcomeEmailOutboxes.SingleAsync()).Status);
        });
    }

    [PostgresFact]
    public async Task Real_hangfire_worker_applies_retries_terminal_failure_and_manual_recovery()
    {
        await WithDatabase(async services =>
        {
            var id = await SeedMessage(services);
            using var scope = services.CreateScope();
            var connectionString = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.GetConnectionString()!;
            var sender = new Mock<IEmailSender>();
            sender.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("SMTP unavailable"));
            var runtime = new ServiceCollection();
            runtime.AddLogging();
            runtime.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
            runtime.AddSingleton(sender.Object);
            runtime.AddSingleton(Options.Create(new SmtpOptions { AppUrl = "https://example.com" }));
            runtime.AddScoped<WelcomeEmailJob>();
            runtime.AddSingleton<WelcomeEmailFailureFilter>();
            runtime.AddHangfire((sp, config) => config
                .UseFilter(sp.GetRequiredService<WelcomeEmailFailureFilter>())
                .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString),
                    new PostgreSqlStorageOptions { QueuePollInterval = TimeSpan.FromMilliseconds(100) }));
            runtime.AddHangfireServer(options => options.WorkerCount = 1);
            await using var workerServices = runtime.BuildServiceProvider();
            var hosted = workerServices.GetServices<IHostedService>().ToList();
            foreach (var service in hosted) await service.StartAsync(default);
            try
            {
                await new HangfireWelcomeEmailQueue(workerServices.GetRequiredService<IServiceScopeFactory>()).EnqueueAsync(id, default);
                var jobId = (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().WelcomeEmailOutboxes.AsNoTracking().SingleAsync()).HangfireJobId!;
                var storage = workerServices.GetRequiredService<JobStorage>();
                var client = workerServices.GetRequiredService<IBackgroundJobClient>();
                using var connection = storage.GetConnection();
                async Task<StateData> WaitForState(string expected)
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    while (true)
                    {
                        var state = connection.GetStateData(jobId);
                        if (state?.Name == expected) return state;
                        await Task.Delay(100, timeout.Token);
                    }
                }
                foreach (var (attempt, delay) in new[] { (1, 60), (2, 300), (3, 1800) })
                {
                    var state = await WaitForState("Scheduled");
                    Assert.Equal(attempt.ToString(), connection.GetJobParameter(jobId, "RetryCount"));
                    var interval = DateTime.Parse(state.Data["EnqueueAt"]).ToUniversalTime() - DateTime.Parse(state.Data["ScheduledAt"]).ToUniversalTime();
                    Assert.InRange(interval.TotalSeconds, delay - 1, delay + 1);
                    // Advance scheduled work explicitly instead of waiting 36 minutes.
                    Assert.True(client.ChangeState(jobId, new EnqueuedState(), "Scheduled"));
                }
                await WaitForState("Failed");
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.Equal(WelcomeEmailStatus.Failed, (await db.WelcomeEmailOutboxes.AsNoTracking().SingleAsync()).Status);
                sender.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
                Assert.True(client.ChangeState(jobId, new EnqueuedState(), "Failed"));
                await WaitForState("Succeeded");
                Assert.Equal(WelcomeEmailStatus.Sent, (await db.WelcomeEmailOutboxes.AsNoTracking().SingleAsync()).Status);
                sender.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(5));
            }
            finally
            {
                foreach (var service in hosted) await service.StopAsync(default);
            }
        });
    }

    private static RegisterCommand Request(string name) => new(new RegisterRequest
    {
        Username = name, Email = name + "@example.com", FullName = "Ng\u01b0\u1eddi d\u00f9ng th\u1eed", Password = "StrongPassword1!", ConfirmPassword = "StrongPassword1!", TermsAccepted = true
    });

    private static async Task<Guid> SeedMessage(ServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User { Id = Guid.NewGuid(), UserName = "test", Email = "test@example.com", FullName = "Test" };
        db.Users.Add(user);
        var message = new WelcomeEmailOutbox { UserId = user.Id, Email = user.Email, FullName = user.FullName };
        db.WelcomeEmailOutboxes.Add(message);
        await db.SaveChangesAsync();
        return message.Id;
    }

    private static async Task WithDatabase(Func<ServiceProvider, Task> test)
    {
        var name = "welcome_test_" + Guid.NewGuid().ToString("N");
        var connectionString = Environment.GetEnvironmentVariable("SEARCH_TEST_POSTGRES")!;
        await using var admin = new NpgsqlConnection(connectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {name}", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(connectionString) { Database = name, Pooling = false }.ConnectionString;
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(connection));
            services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
            services.AddIdentityCore<User>().AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddScoped<IRegistrationTransactionFactory, RegistrationTransactionFactory>();
            var jwt = new Mock<IJwtService>();
            jwt.Setup(x => x.GenerateRefreshToken()).Returns("refresh-token");
            jwt.Setup(x => x.GenerateAccessToken(It.IsAny<User>(), It.IsAny<IEnumerable<string>>())).Returns("access");
            services.AddSingleton(jwt.Object);
            services.AddSingleton(new Mock<IWelcomeEmailQueue>().Object);
            var jobs = new Mock<IBackgroundJobClient>();
            jobs.Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>())).Returns("job-1");
            services.AddSingleton(jobs.Object);
            services.AddSingleton(new Mock<IEmailSender>().Object);
            services.AddSingleton(Options.Create(new SmtpOptions { AppUrl = "https://example.com" }));
            services.AddScoped<RegisterCommandHandler>();
            services.AddScoped<WelcomeEmailJob>();
            await using var provider = services.BuildServiceProvider();
            using (var scope = provider.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
                Assert.True((await scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>().CreateAsync(new IdentityRole<Guid>("Author"))).Succeeded);
            }
            await test(provider);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {name} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
