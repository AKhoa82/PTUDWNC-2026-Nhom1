using CulinaryBlog.Application.Features.Recipes.Commands.Steps;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CulinaryBlog.API.Endpoints;

public static class RecipeStepEndpoints
{
    public static void MapRecipeStepEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/recipes/{id}/steps")
            .RequireAuthorization();

        group.MapPost("/", async (Guid id, [FromBody] AddRecipeStepCommand command, System.Security.Claims.ClaimsPrincipal user, IMediator mediator) =>
        {
            command.RecipeId = id;
            if (user?.Identity?.IsAuthenticated == true)
            {
                command.AuthorId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                command.IsAdmin = user.IsInRole("Admin");
            }
            var result = await mediator.Send(command);
            return Results.Created($"/api/v1/recipes/{id}/steps/{result.Id}", result);
        });

        group.MapPut("/{stepId}", async (Guid id, Guid stepId, [FromBody] UpdateRecipeStepCommand command, System.Security.Claims.ClaimsPrincipal user, IMediator mediator) =>
        {
            command.RecipeId = id;
            command.StepId = stepId;
            if (user?.Identity?.IsAuthenticated == true)
            {
                command.AuthorId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                command.IsAdmin = user.IsInRole("Admin");
            }
            var result = await mediator.Send(command);
            return Results.Ok(result);
        });

        group.MapDelete("/{stepId}", async (Guid id, Guid stepId, System.Security.Claims.ClaimsPrincipal user, IMediator mediator) =>
        {
            var command = new DeleteRecipeStepCommand { RecipeId = id, StepId = stepId };
            if (user?.Identity?.IsAuthenticated == true)
            {
                command.AuthorId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                command.IsAdmin = user.IsInRole("Admin");
            }
            await mediator.Send(command);
            return Results.NoContent();
        });
    }
}
