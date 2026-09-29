using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Recipes.Commands.Ingredients;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Domain.Exceptions;

namespace CulinaryBlog.API.Endpoints;

public static class RecipeIngredientEndpoints
{
    public static void MapRecipeIngredientEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/recipes").WithTags("Recipe Ingredients");

        group.MapPost("/{id:guid}/ingredients", async (Guid id, AddIngredientRequest request, IMediator mediator, ClaimsPrincipal user) =>
        {
            try
            {
                var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier);
                var isAdmin = user.IsInRole("Admin");

                if (currentUserId == null)
                {
                    return Results.Unauthorized();
                }

                var command = new AddIngredientCommand(
                    id,
                    request.Name,
                    request.Quantity,
                    request.Unit,
                    request.Notes,
                    request.SortOrder,
                    currentUserId,
                    isAdmin
                );

                var result = await mediator.Send(command);
                return Results.Created($"/api/v1/recipes/{id}/ingredients/{result.Id}", result);
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
        .RequireAuthorization();

        group.MapPut("/{id:guid}/ingredients/{ingId:guid}", async (Guid id, Guid ingId, UpdateIngredientRequest request, IMediator mediator, ClaimsPrincipal user) =>
        {
            try
            {
                var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier);
                var isAdmin = user.IsInRole("Admin");

                if (currentUserId == null)
                {
                    return Results.Unauthorized();
                }

                var command = new UpdateIngredientCommand(
                    id,
                    ingId,
                    request.Name,
                    request.Quantity,
                    request.Unit,
                    request.Notes,
                    request.SortOrder,
                    currentUserId,
                    isAdmin
                );

                var result = await mediator.Send(command);
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
        .RequireAuthorization();

        group.MapDelete("/{id:guid}/ingredients/{ingId:guid}", async (Guid id, Guid ingId, IMediator mediator, ClaimsPrincipal user) =>
        {
            try
            {
                var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier);
                var isAdmin = user.IsInRole("Admin");

                if (currentUserId == null)
                {
                    return Results.Unauthorized();
                }

                var command = new DeleteIngredientCommand(
                    id,
                    ingId,
                    currentUserId,
                    isAdmin
                );

                await mediator.Send(command);
                return Results.NoContent();
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
        .RequireAuthorization();
    }
}

public class AddIngredientRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public string? Notes { get; set; }
    public int SortOrder { get; set; }
}

public class UpdateIngredientRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public string? Notes { get; set; }
    public int SortOrder { get; set; }
}
