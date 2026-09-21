using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CulinaryBlog.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Auth.Register;
using CulinaryBlog.Application.Features.Categories.GetCategories;
using CulinaryBlog.Application.Features.Categories.Queries.GetCategoryBySlug;
using CulinaryBlog.Application.Features.Recipes.GetRecipes;
using CulinaryBlog.Application.Features.Recipes.Commands.CreateRecipe;
using MediatR;
using Npgsql;
using Scalar.AspNetCore;
using System.ComponentModel.DataAnnotations;
using CulinaryBlog.Infrastructure;
using CulinaryBlog.API.Endpoints;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IApplicationDbContext>(
    provider => provider.GetRequiredService<ApplicationDbContext>());

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(RegisterCommand).Assembly));

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "CulinaryBlog:";
});
builder.Services.AddOpenApi();
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");

app.MapPost("/api/auth/register", async (
    RegisterRequest request,
    IMediator mediator,
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
                group => group
                    .Select(result => result.ErrorMessage ?? "Giá trị không hợp lệ.")
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        return Results.ValidationProblem(errors);
    }

    try
    {
        var userId = await mediator.Send(
            new RegisterCommand(request),
            cancellationToken);

        return Results.Ok(new
        {
            message = "Đăng ký thành công.",
            userId
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { message = ex.Message });
    }
    catch (DbUpdateException ex) when (
        ex.InnerException is PostgresException { SqlState: "23505" })
    {
        return Results.Conflict(new { message = "Email đã được sử dụng." });
    }
});

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
    CancellationToken cancellationToken,
    int page = 1,
    int pageSize = 12,
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

    var query = new GetRecipesQuery(
        Page: page,
        PageSize: pageSize,
        CategoryId: categoryId,
        Difficulty: parsedDifficulty,
        MaxCookTime: maxCookTime,
        Sort: sort
    );

    var result = await mediator.Send(query, cancellationToken);
    return Results.Ok(result);
})
.WithName("GetRecipesV1")
.WithSummary("FR-RCP-001 – Danh sách công thức (phân trang, lọc, sắp xếp, Redis Cache TTL 15 phút)")
.AllowAnonymous();

app.MapPost("/api/v1/recipes", async (
    CreateRecipeRequest request,
    IMediator mediator,
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
            
        return Results.ValidationProblem(errors);
    }
    
    try
    {
        // Lấy AuthorId từ Token (Giả lập tạm thời nếu FR-AUTH chưa gắn)
        string? authorId = null;

        var recipeId = await mediator.Send(
            new CreateRecipeCommand(request, authorId),
            cancellationToken);

        return Results.Created($"/api/v1/recipes/{recipeId}", new
        {
            message = "Tạo công thức thành công.",
            id = recipeId
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
})
.WithName("CreateRecipeV1")
.WithSummary("FR-RCP-003 – Tạo công thức nấu ăn mới");

// Module Quản lý Tệp tin (FR-FILE-001 & FR-FILE-002)
app.MapFileEndpoints();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    await dbContext.Database.MigrateAsync();

    // Khởi tạo và seed dữ liệu: ít nhất 20 Categories và 100 Recipes (mỗi recipe >= 10 nguyên liệu, >= 5 bước chế biến)
    await DatabaseSeeder.SeedAsync(dbContext);
}

app.Run();