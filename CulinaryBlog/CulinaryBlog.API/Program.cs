using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CulinaryBlog.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Auth.Register;
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
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");

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
        return Results.Conflict(new
        {
            message = ex.Message
        });
    }
    catch (DbUpdateException ex) when (
        ex.InnerException is PostgresException { SqlState: "23505" })
    {
        return Results.Conflict(new
        {
            message = "Email đã được sử dụng."
        });
    }
});

// Endpoint GET Categories
app.MapGet("/api/categories", async (
    IApplicationDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var categories = await dbContext.Categories
        .AsNoTracking()
        .ToListAsync(cancellationToken);

    return Results.Ok(categories);
})
.WithName("GetCategories")
.WithSummary("Lấy danh sách danh mục món ăn/bài viết");

// Tự động Migration và Seed dữ liệu mẫu
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    
    // Tự động áp dụng Migration để tạo bảng Categories trong PostgreSQL
    await dbContext.Database.MigrateAsync();

    // Thêm dữ liệu mẫu nếu bảng Categories đang trống
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