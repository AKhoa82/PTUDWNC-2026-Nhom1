using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipe;

public sealed class DeleteRecipeCommandHandler : IRequestHandler<DeleteRecipeCommand, Unit>
{
    private readonly IApplicationDbContext _context;
private readonly ILogger<DeleteRecipeCommandHandler> _logger;

    public DeleteRecipeCommandHandler(IApplicationDbContext context, ILogger<DeleteRecipeCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Unit> Handle(DeleteRecipeCommand request, CancellationToken cancellationToken)
    {
        var recipe = await _context.Recipes
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy công thức với ID: {request.Id}");

        // Chỉ Owner hoặc Admin mới được xóa
        if (!Guid.TryParse(request.CurrentUserId, out var parsedUserId))
            throw new UnauthorizedAccessException("Invalid User ID");
        if (recipe.AuthorId != parsedUserId && !request.IsAdmin)
        {
            throw new ForbiddenException("Bạn không có quyền xóa công thức này.");
        }

        // Soft delete
        recipe.SoftDelete();

        // Invalidate cache
        _context.RecipeCacheInvalidations.Add(new RecipeCacheInvalidation());

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Recipe {RecipeId} soft-deleted by user {UserId}. IsAdmin: {IsAdmin}",
            request.Id,
            request.CurrentUserId,
            request.IsAdmin);

        return Unit.Value;
    }
}