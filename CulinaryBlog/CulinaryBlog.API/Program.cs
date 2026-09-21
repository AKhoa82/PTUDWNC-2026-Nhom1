using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CulinaryBlog.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Auth.Register;
using CulinaryBlog.Application.Features.Categories.GetCategories;
using CulinaryBlog.Application.Features.Categories.Queries.GetCategoryBySlug;
using CulinaryBlog.Application.Features.Categories.Commands.CreateCategory;
using MediatR;
using Npgsql;
using Scalar.AspNetCore;
using System.ComponentModel.DataAnnotations;

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

// Dùng In-Memory Cache thay vì Redis ở môi trường Dev
builder.Services.AddDistributedMemoryCache();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");

// ==========================================
// 1. AUTH ENDPOINTS
// ==========================================

// Endpoint Register
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

// ==========================================
// 2. CATEGORIES ENDPOINTS
// ==========================================

// FR-CAT-001: Endpoint GET Danh sách Categories (v1 + CQRS)
app.MapGet("/api/v1/categories", async (
    IMediator mediator,
    CancellationToken cancellationToken) =>
{
    var categories = await mediator.Send(new GetCategoriesQuery(), cancellationToken);
    return Results.Ok(categories);
})
.WithName("GetCategoriesV1")
.WithSummary("Lấy danh sách danh mục (CQRS + Memory Cache)")
.AllowAnonymous();

// FR-CAT-002: Endpoint GET Chi tiết Category theo Slug
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

// FR-CAT-003: Endpoint POST Tạo danh mục mới [Admin]
app.MapPost("/api/v1/categories", async (
    CreateCategoryCommand command,
    IMediator mediator,
    CancellationToken cancellationToken) =>
{
    try
    {
        var categoryId = await mediator.Send(command, cancellationToken);

        return Results.Created($"/api/categories/{command.Slug}", new 
        { 
            id = categoryId, 
            message = "Tạo danh mục thành công." 
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { message = ex.Message });
    }
})
.WithName("CreateCategory")
.WithSummary("Tạo danh mục mới (Xóa cache categories:all)");

// ==========================================
// 3. MIGRATION & SEED DATA
// ==========================================
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    
    await dbContext.Database.MigrateAsync();

    if (!await dbContext.Categories.AnyAsync())
    {
        dbContext.Categories.AddRange(
            new Category 
            { 
                Id = Guid.NewGuid(), 
                Name = "Món Việt", 
                Slug = "mon-viet", 
                Description = "Các món ăn truyền thống Việt Nam" 
            },
            new Category 
            { 
                Id = Guid.NewGuid(), 
                Name = "Món Á", 
                Slug = "mon-a", 
                Description = "Ẩm thực các nước Châu Á" 
            },
            new Category 
            { 
                Id = Guid.NewGuid(), 
                Name = "Món Âu", 
                Slug = "mon-au", 
                Description = "Ẩm thực phong cách Châu Âu" 
            }
        );

        await dbContext.SaveChangesAsync();
    }
}

app.Run();