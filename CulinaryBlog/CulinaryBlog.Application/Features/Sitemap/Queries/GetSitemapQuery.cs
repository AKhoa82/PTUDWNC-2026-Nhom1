using MediatR;

namespace CulinaryBlog.Application.Features.Sitemap.Queries;
public record GetSitemapQuery(string BaseUrl) : IRequest<string>;