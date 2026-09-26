using CulinaryBlog.Application.Contracts.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Categories.Commands.UpdateCategory;

public class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateCategoryCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (category == null)
        {
            throw new Exception($"Không tìm thấy danh mục với Id: {request.Id}");
        }

        var isDuplicateName = await _context.Categories
            .AnyAsync(c => c.Name.ToLower() == request.Name.ToLower() && c.Id != request.Id, cancellationToken);

        if (isDuplicateName)
        {
            throw new Exception("Tên danh mục này đã trùng với một danh mục khác.");
        }

        var slug = request.Name.ToLower().Trim().Replace(" ", "-");
        category.Update(request.Name, slug, request.Description);

        await _context.SaveChangesAsync(cancellationToken);
    }
}