using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.API.Endpoints;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CulinaryBlog.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using CulinaryBlog.Application.Features.Auth.Register;
using CulinaryBlog.Application.Common.Behaviors;
using CulinaryBlog.Application.Contracts.Security;
using CulinaryBlog.Infrastructure.Security;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using CulinaryBlog.Application.Features.Categories.GetCategories;
using CulinaryBlog.Application.Features.Categories.Queries.GetCategoryBySlug;
using CulinaryBlog.Application.Features.Recipes.GetRecipes;
using CulinaryBlog.Application.Features.Recipes.Commands.CreateRecipe;
using CulinaryBlog.Application.Features.Recipes.Queries.GetRecipeBySlug;
using MediatR;
using Npgsql;
using Scalar.AspNetCore;
using System.ComponentModel.DataAnnotations;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Infrastructure;
using CulinaryBlog.Infrastructure.Jobs;
using Hangfire;
using Microsoft.Extensions.Caching.Distributed;

if ((args.Contains("--apply") && !args.Contains("--storage-backfill")) ||
    (args.Contains("--storage-backfill") && args.Contains("--storage-inventory")))
    throw new ArgumentException("Use either --storage-backfill [--apply] or --storage-inventory.");
var builder = WebApplication.CreateBuilder(args.Where(arg => arg is not ("--storage-backfill" or "--storage-inventory" or "--apply")).ToArray());

if (args.Contains("--storage-backfill") || args.Contains("--storage-inventory"))
{
    // Maintenance mode starts neither HTTP endpoints nor background workers.
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
    builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
    builder.Services.AddInfrastructureServices(builder.Configuration);
    await using var maintenanceHost = builder.Build();
    using var maintenanceScope = maintenanceHost.Services.CreateScope();
    var maintenance = maintenanceScope.ServiceProvider.GetRequiredService<CulinaryBlog.Infrastructure.Services.StorageMaintenanceService>();
    if (args.Contains("--storage-backfill"))
        await maintenance.BackfillAsync(args.Contains("--apply"), Console.Out, CancellationToken.None);
    else await maintenance.InventoryAsync(Console.Out, CancellationToken.None);
    return;
}

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Configure Jwt:Key (at least 32 UTF-8 bytes) using User Secrets or Jwt__Key.");

// Console logging remains available when the Windows Event Log is not writable.
if (OperatingSystem.IsWindows())
    builder.Logging.AddFilter<Microsoft.Extensions.Logging.EventLog.EventLogLoggerProvider>((_, _) => false);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:3000", "http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddDataProtection();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AuthorPolicy", policy => policy.RequireRole("Author", "Admin"));
    options.AddPolicy("AdminPolicy", policy => policy.RequireRole("Admin"));
});
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IApplicationDbContext>(
    provider => provider.GetRequiredService<ApplicationDbContext>());

builder.Services.AddIdentityCore<User>(options =>
{
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.User.RequireUniqueEmail = true;
})
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddValidatorsFromAssembly(typeof(RegisterCommand).Assembly);
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(RegisterCommand).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = builder.Configuration["Cache:InstancePrefix"] ?? "CulinaryBlog:";
});
builder.Services.AddStackExchangeRedisOutputCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = (builder.Configuration["Cache:InstancePrefix"] ?? "CulinaryBlog:") + "output:";
});
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("RecipeDetail", builder => 
        builder.AddPolicy<CulinaryBlog.API.Infrastructure.RecipeDetailCachePolicy>());
});
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddProblemDetails();

// --- Bổ sung FR-OBS-001: Khởi tạo Health Checks ---
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>(name: "database");
// ---------------------------------------------------

builder.Services.AddExceptionHandler<CulinaryBlog.API.Infrastructure.GlobalExceptionHandler>();
builder.Services.AddRecipeImageFeature();
builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Components ??= new Microsoft.OpenApi.Models.OpenApiComponents();
        document.Components.SecuritySchemes.Add("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Nhập JWT Token vào đây"
        });

        return Task.CompletedTask;
    });

    options.AddOperationTransformer((operation, context, cancellationToken) =>
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var allowAnonymous = metadata.OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>().Any();
        var authorize = metadata.OfType<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Any();

        if (authorize && !allowAnonymous)
        {
            operation.Security.Add(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
            {
                {
                    new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                    {
                        Reference = new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        }
        return Task.CompletedTask;
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseRecipeImageRequestLimits();
app.UseOutputCache();

app.MapAuthEndpoints();
app.MapRecipeImageEndpoints();
app.MapRecipeIngredientEndpoints();
app.MapRecipeStepEndpoints();
app.MapRecipeEndpoints();

if (builder.Configuration.GetValue<bool>("Hangfire:DashboardEnabled"))
{
    app.MapHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = []
    }); // Đã bỏ RequireAuthorization để test không bị lỗi 401
}

app.MapGet("/api/v1/categories", async (
    IMediator mediator,
    CancellationToken cancellationToken) =>
{
    var categories = await mediator.Send(new GetCategoriesQuery(), cancellationToken);
    return Results.Ok(categories);
})
.WithName("GetCategoriesV1")
.WithSummary("Lấy danh sách danh mục (CQRS + Redis Cache, TTL 60 phút)")
.AllowAnonymous();

app.MapGet("/api/v1/categories/{slug}", async (
    string slug,
    IMediator mediator,
    CancellationToken cancellationToken) =>
{
    var result = await mediator.Send(new GetCategoryBySlugQuery(slug), cancellationToken);
    return Results.Ok(result);
})
.WithName("GetCategoryBySlug")
.WithSummary("Lấy thông tin chi tiết danh mục và danh sách công thức thuộc danh mục");

// FR-RCP-001: Danh sách công thức (phân trang, lọc, sắp xếp)
app.MapGet("/api/v1/recipes", async (
    IMediator mediator,
    System.Security.Claims.ClaimsPrincipal user,
    CancellationToken cancellationToken,
    int page = 1,
    int pageSize = 12,
    string? keyword = null,
    Guid? categoryId = null,
    string? difficulty = null,
    int? maxCookTime = null,
    int? minServings = null,
    string sort = "-createdAt") =>
{
    string? currentUserId = null;
    bool isAdmin = false;
    
    if (user?.Identity?.IsAuthenticated == true)
    {
        currentUserId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        isAdmin = user.IsInRole("Admin");
    }

    var query = new GetRecipesQuery(
        Page: page,
        PageSize: pageSize,
        Keyword: keyword?.Trim(),
        CategoryId: categoryId,
        DifficultyRaw: difficulty,
        MaxCookTime: maxCookTime,
        MinServings: minServings,
        Sort: sort,
        CurrentUserId: currentUserId,
        IsAdmin: isAdmin
    );

    var result = await mediator.Send(query, cancellationToken);
    return Results.Ok(result);
})
.WithName("GetRecipesV1")
.WithDescription("FR-SRCH-003: sort=createdAt|title|cookTime|publishedAt; thêm tiền tố '-' để giảm dần. Mặc định -createdAt.")
.WithSummary("Danh sách công thức")
.Produces<PagedResult<RecipeSummaryDto>>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status422UnprocessableEntity)
.AllowAnonymous();

app.MapGet("/api/v1/recipes/search", async (
    IMediator mediator, CancellationToken cancellationToken,
    string? q = null, int page = 1, int pageSize = 12,
    Guid? categoryId = null, string? difficulty = null,
    int? maxCookTime = null, int? minServings = null, string? sort = null) =>
{
    return Results.Ok(await mediator.Send(
        new CulinaryBlog.Application.Features.Recipes.SearchRecipes.SearchRecipesQuery(
            q, page, pageSize, categoryId, difficulty, maxCookTime, minServings, sort),
        cancellationToken));
})
.WithName("SearchRecipesV1")
.WithSummary("Tìm kiếm toàn văn")
.Produces<PagedResult<RecipeSummaryDto>>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status422UnprocessableEntity)
.AllowAnonymous();

app.MapGet("/api/v1/recipes/{slug}", async (
    string slug,
    IMediator mediator,
    System.Security.Claims.ClaimsPrincipal user,
    CancellationToken cancellationToken) =>
{
    string? currentUserId = null;
    bool isAdmin = false;
    if (user?.Identity?.IsAuthenticated == true)
    {
        currentUserId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        isAdmin = user.IsInRole("Admin");
    }

    var result = await mediator.Send(new GetRecipeBySlugQuery(slug, currentUserId, isAdmin), cancellationToken);

    return Results.Ok(result);
})
.WithName("GetRecipeBySlugV1")
.WithSummary("Xem chi tiết công thức")
.Produces<RecipeDetailDto>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status403Forbidden)
.ProducesProblem(StatusCodes.Status404NotFound)
.CacheOutput("RecipeDetail")
.AllowAnonymous();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    await dbContext.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    foreach (var roleName in new[] { "Author", "Admin" })
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
        }
    }

    // Tạo sẵn User Admin bằng DbContext trực tiếp để tránh lỗi Identity Validation khi Seed
    var adminId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    var defaultAdmin = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == "admin@culinaryblog.com");
    if (defaultAdmin == null)
    {
        defaultAdmin = new User
        {
            Id = adminId,
            UserName = "admin",
            NormalizedUserName = "ADMIN",
            Email = "admin@culinaryblog.com",
            NormalizedEmail = "ADMIN@CULINARYBLOG.COM",
            EmailConfirmed = true,
            FullName = "System Admin",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };
        dbContext.Users.Add(defaultAdmin);
        await dbContext.SaveChangesAsync();
    }

    if (!await dbContext.Categories.AnyAsync())
    {
        var monViet = new Category { Id = Guid.Parse("11111111-0000-0000-0000-000000000001"), Name = "Món Việt", Slug = "mon-viet", Description = "Các món ăn truyền thống Việt Nam" };
        var monA    = new Category { Id = Guid.Parse("11111111-0000-0000-0000-000000000002"), Name = "Món Á",    Slug = "mon-a",    Description = "Ẩm thực các nước Châu Á" };
        var monAu   = new Category { Id = Guid.Parse("11111111-0000-0000-0000-000000000003"), Name = "Món Âu",   Slug = "mon-au",   Description = "Ẩm thực phong cách Châu Âu" };

        dbContext.Categories.AddRange(monViet, monA, monAu);
        await dbContext.SaveChangesAsync();
    }

    if (!await dbContext.Recipes.AnyAsync())
    {
        var monVietId = Guid.Parse("11111111-0000-0000-0000-000000000001");
        var monAId    = Guid.Parse("11111111-0000-0000-0000-000000000002");
        var monAuId   = Guid.Parse("11111111-0000-0000-0000-000000000003");

        dbContext.Recipes.AddRange(
            new Recipe
            {
                Title              = "Phở Bò Hà Nội",
                Slug               = "pho-bo-ha-noi",
                Description        = "Phở bò truyền thống Hà Nội với nước dùng trong vắt, thơm mùi quế hồi.",
                ImageUrl           = "https://images.unsplash.com/photo-1582878826629-29b7ad1cdc43?w=800",
                PrepTimeMinutes    = 30,
                CookingTimeMinutes = 180,
                Servings           = 4,
                Difficulty         = RecipeDifficulty.Hard,
                Status             = RecipeStatus.Published,
                CategoryId         = monVietId,
                AuthorId           = defaultAdmin.Id,
                PublishedAt        = DateTime.UtcNow
            },
            new Recipe
            {
                Title              = "Bún Bò Huế",
                Slug               = "bun-bo-hue",
                Description        = "Bún bò Huế cay nồng đặc trưng miền Trung, nước dùng đậm đà.",
                ImageUrl           = "https://images.unsplash.com/photo-1569050467447-ce54b3bbc37d?w=800",
                PrepTimeMinutes    = 20,
                CookingTimeMinutes = 120,
                Servings           = 4,
                Difficulty         = RecipeDifficulty.Medium,
                Status             = RecipeStatus.Published,
                CategoryId         = monVietId,
                AuthorId           = defaultAdmin.Id,
                PublishedAt        = DateTime.UtcNow
            },
            new Recipe
            {
                Title              = "Cơm Chiên Dương Châu",
                Slug               = "com-chien-duong-chau",
                Description        = "Cơm chiên kiểu Dương Châu với tôm, trứng và rau củ đầy màu sắc.",
                ImageUrl           = "https://images.unsplash.com/photo-1603133872878-684f208fb84b?w=800",
                PrepTimeMinutes    = 15,
                CookingTimeMinutes = 20,
                Servings           = 2,
                Difficulty         = RecipeDifficulty.Easy,
                Status             = RecipeStatus.Published,
                CategoryId         = monAId,
                AuthorId           = defaultAdmin.Id,
                PublishedAt        = DateTime.UtcNow
            },
            new Recipe
            {
                Title              = "Mì Ramen Nhật Bản",
                Slug               = "mi-ramen-nhat-ban",
                Description        = "Ramen tonkotsu nước dùng xương heo hầm 12 tiếng, chashu mềm tan.",
                ImageUrl           = "https://images.unsplash.com/photo-1569718212165-3a8278d5f624?w=800",
                PrepTimeMinutes    = 60,
                CookingTimeMinutes = 720,
                Servings           = 2,
                Difficulty         = RecipeDifficulty.Expert,
                Status             = RecipeStatus.Published,
                CategoryId         = monAId,
                AuthorId           = defaultAdmin.Id,
                PublishedAt        = DateTime.UtcNow
            },
            new Recipe
            {
                Title              = "Pasta Carbonara",
                Slug               = "pasta-carbonara",
                Description        = "Pasta kiểu Ý cổ điển với trứng, pecorino romano và guanciale giòn rụm.",
                ImageUrl           = "https://images.unsplash.com/photo-1621996346565-e3dbc646d9a9?w=800",
                PrepTimeMinutes    = 10,
                CookingTimeMinutes = 20,
                Servings           = 2,
                Difficulty         = RecipeDifficulty.Medium,
                Status             = RecipeStatus.Published,
                CategoryId         = monAuId,
                AuthorId           = defaultAdmin.Id,
                PublishedAt        = DateTime.UtcNow
            },
            new Recipe
            {
                Title              = "Beef Steak Bơ Tỏi",
                Slug               = "beef-steak-bo-toi",
                Description        = "Bít tết bò thăn áp chảo áo bơ tỏi thơm lừng, chín tái hoàn hảo.",
                ImageUrl           = "https://images.unsplash.com/photo-1546833999-b9f581a1996d?w=800",
                PrepTimeMinutes    = 10,
                CookingTimeMinutes = 15,
                Servings           = 1,
                Difficulty         = RecipeDifficulty.Medium,
                Status             = RecipeStatus.Published,
                CategoryId         = monAuId,
                AuthorId           = defaultAdmin.Id,
                PublishedAt        = DateTime.UtcNow
            }
        );

        await dbContext.SaveChangesAsync();
    }
}

app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<FileDeletionReconciliationJob>(
    "reconcile-file-deletions", job => job.ExecuteAsync(CancellationToken.None), "*/5 * * * *");
app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<RecipeImageCacheInvalidator>(
    "reconcile-recipe-cache", job => job.ProcessAsync(CancellationToken.None), "* * * * *");
app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<CulinaryBlog.Infrastructure.Jobs.GenerateSitemapJob>(
    "generate-sitemap", job => job.ExecuteAsync(CancellationToken.None), "0 2 * * *");

app.MapGet("/sitemap.xml", async (IDistributedCache cache) =>
{
    var xml = await cache.GetStringAsync("sitemap_xml");
    
    if (string.IsNullOrEmpty(xml))
    {
        return Results.NotFound("Sitemap đang được tạo tự động. Vui lòng quay lại sau ít phút.");
    }
    
    return Results.Text(xml, "application/xml");
})
.WithName("GetSitemap")
.WithSummary("Lấy file sitemap.xml cho SEO")
.AllowAnonymous();

app.MapHealthChecks("/api/health")
    .WithName("HealthCheck")
    .WithSummary("Kiểm tra trạng thái hoạt động của hệ thống và Database")
    .AllowAnonymous();

app.MapControllers();

app.Run();