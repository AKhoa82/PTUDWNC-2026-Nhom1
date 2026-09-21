using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Contracts.Persistence;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Category> Categories { get; }
    DbSet<Recipe> Recipes { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}