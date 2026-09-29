using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Recipes.Commands.PublishRecipe;
using CulinaryBlog.Application.Features.Recipes.Commands.UnpublishRecipe;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;

using CulinaryBlog.Domain.Exceptions;
using CulinaryBlog.Application.Common.Exceptions;

namespace CulinaryBlog.API.Endpoints;

public static class RecipeEndpoints
{
    public static IEndpointRouteBuilder MapRecipeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/recipes");

        // PATCH /api/v1/recipes/{id}/publish - Xuất bản công thức
        group.MapPatch("/{id:guid}/publish", async (
            Guid id,
            ISender sender,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            try
            {
                var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
                var isAdmin = user.IsInRole("Admin");
                var result = await sender.Send(new PublishRecipeCommand(id, currentUserId, isAdmin), ct);
                return Results.Ok(result);
            }
            catch (DomainException ex)
            {
                return Results.UnprocessableEntity(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(statusCode: 403, detail: ex.Message);
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
        })
        .WithName("PublishRecipe")
        .WithSummary("Xuất bản công thức nấu ăn")
        .WithDescription("Chuyển trạng thái công thức từ Draft sang Published. Yêu cầu phải có ít nhất 1 bước thực hiện và 1 nguyên liệu.")
        .RequireAuthorization()
        .Produces<RecipeDetailDto>(200)
        .ProducesProblem(401)
        .ProducesProblem(403)
        .ProducesProblem(404)
        .ProducesProblem(422);

        // PATCH /api/v1/recipes/{id}/unpublish - Hủy xuất bản công thức
        group.MapPatch("/{id:guid}/unpublish", async (
            Guid id,
            ISender sender,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            try
            {
                var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
                var isAdmin = user.IsInRole("Admin");
                var result = await sender.Send(new UnpublishRecipeCommand(id, currentUserId, isAdmin), ct);
                return Results.Ok(result);
            }
            catch (DomainException ex)
            {
                return Results.UnprocessableEntity(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(statusCode: 403, detail: ex.Message);
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
        })
        .WithName("UnpublishRecipe")
        .WithSummary("Hủy xuất bản công thức nấu ăn")
        .WithDescription("Đưa công thức trở lại trạng thái Draft (chỉ tác giả hoặc admin mới thấy).")
        .RequireAuthorization()
        .Produces<RecipeDetailDto>(200)
        .ProducesProblem(401)
        .ProducesProblem(403)
        .ProducesProblem(404);

        return app;
    }
}