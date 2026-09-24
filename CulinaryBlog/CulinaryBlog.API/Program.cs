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
using CulinaryBlog.Infrastructure;
using CulinaryBlog.Infrastructure.Jobs;
using Hangfire;

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
        builder.Expire(TimeSpan.FromMinutes(60)).Tag("recipes"));
});


builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddRecipeImageFeature();
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

        document.SecurityRequirements.Add(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
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

        return Task.CompletedTask;
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

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

if (builder.Configuration.GetValue<bool>("Hangfire:DashboardEnabled"))
{
    app.MapHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = []
    }).RequireAuthorization("AdminPolicy");
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

app.MapGet("/api/categories/{slug}", async (
    string slug,
    IMediator mediator,
    CancellationToken cancellationToken) =>
{
    var result = await mediator.Send(new GetCategoryBySlugQuery(slug), cancellationToken);

    return result is not null
        ? Results.Ok(result)
        : Results.NotFound(new { message = $"Không tìm thấy danh mục với slug: '{slug}'" });
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
    string sort = "-createdAt") =>
{
    if (page < 1 || pageSize < 1 || pageSize > 50)
    {
        return Results.Problem(
            detail: "page phải >= 1, pageSize phải trong khoảng [1, 50].",
            statusCode: 422,
            title: "Tham số không hợp lệ.");
    }

    RecipeDifficulty? parsedDifficulty = null;
    if (!string.IsNullOrEmpty(difficulty))
    {
        if (!Enum.TryParse<RecipeDifficulty>(difficulty, ignoreCase: true, out var diffEnum))
        {
            return Results.Problem(
                detail: "difficulty phải là một trong: Easy, Medium, Hard, Expert.",
                statusCode: 422,
                title: "Tham số không hợp lệ.");
        }
        parsedDifficulty = diffEnum;
    }

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
        Difficulty: parsedDifficulty,
        MaxCookTime: maxCookTime,
        Sort: sort,
        CurrentUserId: currentUserId,
        IsAdmin: isAdmin
    );

    var result = await mediator.Send(query, cancellationToken);
    return Results.Ok(result);
})
.WithName("GetRecipesV1")
.WithSummary("FR-RCP-001 – Danh sách công thức (phân trang, lọc, sắp xếp, Redis Cache TTL 15 phút)")
.AllowAnonymous();

app.MapGet("/api/v1/recipes/{slug}", async (
    string slug,
    IMediator mediator,
    System.Security.Claims.ClaimsPrincipal user,
    CancellationToken cancellationToken) =>
{
    try
    {
        string? currentUserId = null;
        bool isAdmin = false;
        if (user?.Identity?.IsAuthenticated == true)
        {
            currentUserId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            isAdmin = user.IsInRole("Admin");
        }

        var result = await mediator.Send(new GetRecipeBySlugQuery(slug, currentUserId, isAdmin), cancellationToken);

        return result is not null 
            ? Results.Ok(result) 
            : Results.NotFound(new { message = "Không tìm thấy công thức này." });
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.Problem(statusCode: 403, detail: ex.Message);
    }
})
.WithName("GetRecipeBySlugV1")
.WithSummary("FR-RCP-002 – Xem chi tiết công thức")
.CacheOutput("RecipeDetail")
.AllowAnonymous();

app.MapPost("/api/v1/recipes", async (
    CreateRecipeRequest request,
    IMediator mediator,
    System.Security.Claims.ClaimsPrincipal user,
    RecipeImageCacheInvalidator cacheStore,
    CancellationToken cancellationToken) =>
{
    var validationResults = new List<ValidationResult>();
    var validationContext = new ValidationContext(request);

    if (!Validator.TryValidateObject(
        request,
        validationContext,
        validationResults,
        validateAllProperties: true))
    {
        var errors = validationResults
            .GroupBy(
                result => result.MemberNames.FirstOrDefault() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(result => result.ErrorMessage ?? "Giá trị không hợp lệ.").ToArray(),
                StringComparer.OrdinalIgnoreCase);
            
        return Results.ValidationProblem(errors, statusCode: 422);
    }
    
    try
    {
        string? authorId = null;
        if (user?.Identity?.IsAuthenticated == true)
        {
            authorId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        }

        var recipeId = await mediator.Send(
            new CreateRecipeCommand(request, authorId),
            cancellationToken);

        await cacheStore.InvalidateAsync();

        return Results.Created($"/api/v1/recipes/{recipeId}", new
        {
            message = "Tạo công thức thành công.",
            id = recipeId
        });
    }
    catch (UnauthorizedAccessException) { return Results.Forbid(); }
    catch (FluentValidation.ValidationException ex)
    {
        return Results.ValidationProblem(ex.Errors.GroupBy(x => x.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(x => x.ErrorMessage).ToArray()), statusCode: 422);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
})
.WithName("CreateRecipeV1")
.RequireAuthorization()
.WithSummary("FR-RCP-003 – Tạo công thức nấu ăn mới");

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    await dbContext.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    foreach (var roleName in new[] { "Author", "Admin" })
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            var result = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            if (!result.Succeeded && !await roleManager.RoleExistsAsync(roleName))
                throw new InvalidOperationException($"Could not initialize role {roleName}.");
        }
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

app.Run();
